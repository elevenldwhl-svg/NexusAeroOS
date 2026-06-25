using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using NexusAeroOS.AgentHarness.Agents;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.AgentHarness.Plugins;
using NexusAeroOS.Host.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// 阶段一：服务注册与依赖注入 (Dependency Injection)
// ====================================================================
string apiKey = "sk-fbioafzjlizupncpgjoonszbeheoxhiqndybsakwlfvwdycq";
string pgConnectionString = "Host=localhost;Port=5432;Database=nexus_aero_os;Username=nexus_root;Password=AeroOS_Password2026!";

// 1. 注册底层 HttpClient (单例)
builder.Services.AddSingleton<HttpClient>(sp =>
{
    var client = new HttpClient(new SiliconFlowInterceptor());
    client.Timeout = TimeSpan.FromMinutes(5); // 显式设置超时时间
    return client;
});

// 2. 注册 Semantic Kernel (Transient: 每次请求创建新实例，避免并发 Plugin 污染)
builder.Services.AddTransient<Kernel>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion("deepseek-ai/DeepSeek-V4-Pro", apiKey, httpClient: httpClient);
    kernelBuilder.AddOpenAITextEmbeddingGeneration("BAAI/bge-m3", apiKey, httpClient: httpClient);
    return kernelBuilder.Build();
});

// 显式将 Kernel 内部的 Embedding 服务暴露给 ASP.NET Core 外部容器
builder.Services.AddTransient<ITextEmbeddingGenerationService>(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<ITextEmbeddingGenerationService>();
});

// 3. 注册仓储与业务服务
builder.Services.AddSingleton(new VectorKnowledgeRepository(pgConnectionString));

// 💥 修改：注册基于 Dapper 的 Postgres 审计仓储，并注入连接字符串
builder.Services.AddSingleton(new AuditLogRepository(pgConnectionString));
// 💥 新增：注册机队状态机 (全局单例，保证大模型每次查询的都是同一个数字停机坪)
builder.Services.AddSingleton<FleetRegistryService>();
builder.Services.AddTransient<KnowledgeIngestionService>();
builder.Services.AddTransient<KnowledgeRetrievalService>();
builder.Services.AddTransient<TelemetryParsingAgent>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IAgentEventBroadcaster, SignalRAgentBroadcaster>();
builder.Services.AddSingleton<NexusAeroOS.AgentHarness.Infrastructure.HitlApprovalService>();
builder.Services.AddTransient<NexusAeroOS.AgentHarness.Agents.DroneOnboardAgent>();
// 新增：向容器注册控制器服务
builder.Services.AddControllers();

// 新增：注册 Swagger 核心服务与 API 资源管理器
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // React 运行地址
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // SignalR 必须允许凭证
    });
});

var app = builder.Build();

// ====================================================================
// 阶段二：应用启动期数据预热 (App Startup Initialization)
// ====================================================================
using (var scope = app.Services.CreateScope())
{
    var repo = scope.ServiceProvider.GetRequiredService<VectorKnowledgeRepository>();
    var ingestionService = scope.ServiceProvider.GetRequiredService<KnowledgeIngestionService>();

    // 💥 新增：从容器中获取审计仓储实例
    var auditRepo = scope.ServiceProvider.GetRequiredService<AuditLogRepository>();

    Console.WriteLine("[系统自检] 正在校验 PostgreSQL 存储引擎与 DDL Schema...");

    // 校验向量知识库表
    await repo.EnsureSchemaAsync();

    // 💥 新增：执行审计日志表的自动创建
    await auditRepo.EnsureSchemaAsync();

    string corporateSopDocument = """
        # 星河物流低空飞控核心章程 v2.6
        ## 第一章：动力失控红线与紧急处置机制
        凡属于低空货运序列的无人机飞行器，在执行任务期间，机载传感器触发【BatteryTemp > 65℃】核心热失控红线时，主控系统必须在一秒钟内强制剥离自主导航状态，由脑干中枢强行接管飞行权限。
        ## 第二章：特种医疗载荷调度规则
        若涉事飞行器的 CurrentPayload 字段标记为【RH阴性活体血浆】或【紧急特种药品】，该运单的生存优先级自动提升为 A1 级（最高级别）。调度智能体被赋予最高物理干涉主权，允许静默调用下游仓储管理系统（WMS）锁死安全冗余物资，无需人工二次审批。
        """;

    Console.WriteLine("[系统自检] 正在执行常识规章的智能切片与 Embedding 向量化沉淀...");
    await ingestionService.IngestDocumentAsync(corporateSopDocument);
}

// ====================================================================
// 阶段三：API 路由映射 (Endpoint Routing)
// ====================================================================

// 在 HTTP 请求管道中启用 Swagger 与可视化 UI
app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<AgentControlHub>("/agent-hub");
app.UseCors("AllowFrontend");
// 启用控制器路由体系
app.MapControllers();

Console.WriteLine("🚀 Nexus-AeroOS API 监听服务启动...");
app.Run();

// ====================================================================
// 阶段四：底层网络拦截器 (必须保留在最底部)
// ====================================================================
public class SiliconFlowInterceptor : DelegatingHandler
{
    public SiliconFlowInterceptor()
    {
        InnerHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri != null && request.RequestUri.Host.Contains("openai.com"))
        {
            request.RequestUri = new Uri($"https://api.siliconflow.cn{request.RequestUri.PathAndQuery}");
        }
        return await base.SendAsync(request, cancellationToken);
    }
}