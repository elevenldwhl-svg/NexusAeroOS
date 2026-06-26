using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using NexusAeroOS.AgentHarness.Agents;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.AgentHarness.Plugins;
using NexusAeroOS.Domain.Entities;
using NexusAeroOS.Host.Hubs;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
string apiKey = "sk-fbioafzjlizupncpgjoonszbeheoxhiqndybsakwlfvwdycq";
string pgConnectionString = "Host=localhost;Port=5432;Database=nexus_aero_os;Username=nexus_root;Password=AeroOS_Password2026!";

// 1. 底层基础服务
builder.Services.AddSingleton<HttpClient>(sp => {
    var client = new HttpClient(new SiliconFlowInterceptor());
    client.Timeout = TimeSpan.FromMinutes(5);
    return client;
});

// 2. 语义内核
builder.Services.AddTransient<Kernel>(sp => {
    var httpClient = sp.GetRequiredService<HttpClient>();
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion("deepseek-ai/DeepSeek-V3.2", apiKey, httpClient: httpClient);
    kernelBuilder.AddOpenAITextEmbeddingGeneration("BAAI/bge-m3", apiKey, httpClient: httpClient);
    return kernelBuilder.Build();
});

builder.Services.AddTransient<ITextEmbeddingGenerationService>(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<ITextEmbeddingGenerationService>();
});

// 3. 核心仓储注册 (顺序很重要，依赖方需在被依赖方之后)
builder.Services.AddSingleton(new MissionRepository(pgConnectionString));
builder.Services.AddSingleton(new VectorKnowledgeRepository(pgConnectionString));
builder.Services.AddSingleton(new DroneRepository(pgConnectionString));
builder.Services.AddSingleton(new AuditLogRepository(pgConnectionString));
builder.Services.AddSingleton(new ThoughtLogRepository(pgConnectionString));
builder.Services.AddSingleton<FleetRegistryService>();
builder.Services.AddSingleton<AirspaceService>();
builder.Services.AddSingleton<IAgentEventBroadcaster, SignalRAgentBroadcaster>();
builder.Services.AddSingleton<HitlApprovalService>();

// 4. 业务代理与引擎
builder.Services.AddTransient<KnowledgeIngestionService>();
builder.Services.AddTransient<KnowledgeRetrievalService>();
builder.Services.AddTransient<TelemetryParsingAgent>();
builder.Services.AddTransient<DroneOnboardAgent>();
builder.Services.AddSignalR();

// 5. 挂载后台服务 (托管引擎)
builder.Services.AddHostedService<NexusAeroOS.Host.Services.FleetPhysicsEngine>();
builder.Services.AddHostedService<NexusAeroOS.Host.Services.OrderPumpEngine>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});

var app = builder.Build();

// ====================================================================
// 数据自愈与启动协议
// ====================================================================
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var droneRepo = sp.GetRequiredService<DroneRepository>();
    var missionRepo = sp.GetRequiredService<MissionRepository>();
    var thoughtRepo = sp.GetRequiredService<ThoughtLogRepository>();
    var repo = sp.GetRequiredService<VectorKnowledgeRepository>();
    var registry = sp.GetRequiredService<FleetRegistryService>();
    var ingestion = sp.GetRequiredService<KnowledgeIngestionService>();

    Console.WriteLine("🚀 [系统自检] 正在同步数据架构与资产状态...");
    await droneRepo.EnsureSchemaAndSeedAsync();
    await missionRepo.EnsureSchemaAsync();
    await thoughtRepo.EnsureSchemaAsync();
    await repo.EnsureSchemaAsync();

    // 加载机队资产
    using var conn = new NpgsqlConnection(pgConnectionString);
    var dbDrones = await conn.QueryAsync<DroneState>("SELECT * FROM sys_drone_assets");
    registry.LoadFleetFromDatabase(dbDrones);

    // 💥 守望者协议：断点续传
    var ongoing = await missionRepo.GetActiveMissionsAsync();
    foreach (var m in ongoing)
    {
        var drone = registry.GetFleetSnapshot().FirstOrDefault(d => d.DroneId == m.DroneId);
        if (drone != null)
        {
            drone.CurrentTaskId = m.TaskId;
            drone.CurrentOrderId = m.OrderId;
            drone.WaypointQueue.Clear();
            foreach (var wp in m.Waypoints.Split(';')) drone.WaypointQueue.Enqueue(wp.Trim());
            if (drone.WaypointQueue.TryDequeue(out var firstWp))
            {
                drone.CurrentTargetWaypoint = firstWp;
                drone.IsInMotion = true;
                drone.Status = DroneStatus.FlyingToDeliver;
                Console.WriteLine($"♻️ [断点复原] 无人机 {drone.DroneId} 续接任务 {m.OrderId}");
            }
        }
    }

    await ingestion.IngestDocumentAsync("# 核心章程...");
}

app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<AgentControlHub>("/agent-hub");
app.UseCors("AllowFrontend");
app.MapControllers();

// 数据分析 API 终端
app.MapGet("/api/telemetry/missions/history", async ([FromServices] MissionRepository repo) => {
    using var conn = new NpgsqlConnection(pgConnectionString);
    return Results.Ok(await conn.QueryAsync("SELECT * FROM sys_mission_ledger ORDER BY created_at DESC LIMIT 50"));
});
app.MapGet("/api/telemetry/missions/drone/{droneId}", async (string droneId, [FromServices] MissionRepository repo) => {
    using var conn = new NpgsqlConnection(pgConnectionString);
    return Results.Ok(await conn.QueryAsync("SELECT * FROM sys_mission_ledger WHERE drone_id = @dId ORDER BY created_at DESC LIMIT 5", new { dId = droneId }));
});
app.MapGet("/api/telemetry/thoughts/analytics", async ([FromServices] ThoughtLogRepository repo) => {
    using var conn = new NpgsqlConnection(pgConnectionString);
    return Results.Ok(await conn.QueryAsync("SELECT * FROM sys_brain_thought_ledger ORDER BY created_at DESC LIMIT 100"));
});

Console.WriteLine("🚀 服务运行中...");
app.Run();

// ====================================================================
// 底层网络拦截器
// ====================================================================
public class SiliconFlowInterceptor : DelegatingHandler
{
    public SiliconFlowInterceptor()
    {
        // 💥 补回被我精简掉的底层发包器（InnerHandler）！
        InnerHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.Host.Contains("openai.com") == true)
            request.RequestUri = new Uri($"https://api.siliconflow.cn{request.RequestUri.PathAndQuery}");
        return await base.SendAsync(request, cancellationToken);
    }
}