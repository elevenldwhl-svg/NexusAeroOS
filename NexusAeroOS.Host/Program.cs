using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using NexusAeroOS.AgentHarness.Agents;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.AgentHarness.Plugins;

var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// 阶段一：底座构建期
// ====================================================================
string apiKey = "sk-fbioafzjlizupncpgjoonszbeheoxhiqndybsakwlfvwdycq";
string pgConnectionString = "Host=localhost;Port=5432;Database=nexus_aero_os;Username=nexus_root;Password=AeroOS_Password2026!";

// 实例化我们刚才手写的底层拦截器客户端
var customHttpClient = new HttpClient(new SiliconFlowInterceptor());

var kernelBuilder = Kernel.CreateBuilder();

// 注册 Chat 引擎，注入拦截客户端（SDK 无论怎么发向 OpenAI，都会被底层强行劫持到硅基流动）
kernelBuilder.AddOpenAIChatCompletion(
    modelId: "deepseek-ai/DeepSeek-V3.2",
    apiKey: apiKey,
    httpClient: customHttpClient
);

// 注册 Embedding 引擎，注入同一个拦截客户端
kernelBuilder.AddOpenAITextEmbeddingGeneration(
    modelId: "BAAI/bge-m3",
    apiKey: apiKey,
    httpClient: customHttpClient
);

var kernel = kernelBuilder.Build();

// ====================================================================
// 阶段二：数据预热期
// ====================================================================
Console.WriteLine("\n[系统自检] 正在校验 PostgreSQL 存储引擎与 HNSW 向量索引...");
var vectorRepo = new VectorKnowledgeRepository(pgConnectionString);
await vectorRepo.EnsureSchemaAsync();

var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
var ingestionService = new KnowledgeIngestionService(embeddingService, vectorRepo);

string corporateSopDocument = """
    # 星河物流低空飞控核心章程 v2.6
    ## 第一章：动力失控红线与紧急处置机制
    凡属于低空货运序列的无人机飞行器，在执行任务期间，机载传感器触发【BatteryTemp > 65℃】核心热失控红线时，主控系统必须在一秒钟内强制剥离自主导航状态，由脑干中枢强行接管飞行权限。
    ## 第二章：特种医疗载荷调度规则
    若涉事飞行器的 CurrentPayload 字段标记为【RH阴性活体血浆】或【紧急特种药品】，该运单的生存优先级自动提升为 A1 级（最高级别）。调度智能体被赋予最高物理干涉主权，允许静默调用下游仓储管理系统（WMS）锁死安全冗余物资，无需人工二次审批。
    ## 第三章：备用接驳仓寻址规范
    当触发一级应急干涉时，系统寻址机制应优先锁定距离当前坐标最近的特种接驳仓。其中，“深圳-南山1号特种接驳仓”作为核心医疗枢纽，全天候预留 SKU-BAT-999（低空高倍率石墨烯固态电池）作为战略热冗余储备。
    """;

Console.WriteLine("[系统自检] 正在执行常识规章的智能切片与 Embedding 向量化沉淀...");
await ingestionService.IngestDocumentAsync(corporateSopDocument);
Console.WriteLine("[系统自检] 知识库同步完毕，RAG 记忆体已上线。\n");

// ====================================================================
// 阶段三：业务运行时 (💥 核心重构区：动态 RAG 激活)
// ====================================================================
Console.WriteLine("🚀 Nexus-AeroOS 低空物流全息中台...点火就绪！\n");

var parsingAgent = new TelemetryParsingAgent(kernel);
string dirtyVoice = "呼叫塔台！操，南山4号接驳机刚才在飞过大南山的时候被雷电擦了一下，现在经度113.93，纬度22.54。我干，看大屏它电池核心温直接飙到69度了！电量只剩百分之十二！它货舱里装的是南山医院急要的RH阴性活体血浆，随时会炸机，快评估！";

Console.WriteLine($"[地面飞控语音捕获]: \"{dirtyVoice}\"");
var telemetry = await parsingAgent.ExtractFromPilotVoiceAsync(dirtyVoice);

if (telemetry != null && telemetry.BatteryTemp > 65)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"🚨 [脑干中枢审查]: 涉事机 {telemetry.DroneId} 核心温 {telemetry.BatteryTemp}℃ 突破红线！静默接管飞行控制权！");
    Console.ResetColor();

    // 1. 初始化实时检索服务
    var retrievalService = new KnowledgeRetrievalService(embeddingService, vectorRepo);

    // 2. 提取当前事故的业务意图，并在本地进行高性能向量召回
    string searchQuery = $"无人机电池温度达到 {telemetry.BatteryTemp} 度，货舱载荷为 {telemetry.CurrentPayload} 的特种应急预案与电池更换申请";
    Console.WriteLine($"\n[RAG 路由激活] 正在根据遥测意图检索 HNSW 知识库...");
    string ragContext = await retrievalService.GetRagContextAsync(searchQuery);

    // 打印召回证据查看
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(ragContext);
    Console.ResetColor();

    // 3. 将装配好的动态上下文，注入 AI 军师的决策流中
    kernel.Plugins.AddFromType<UavEmergencyPlugin>("EmergencyOps");
    var chatService = kernel.GetRequiredService<IChatCompletionService>();
    var history = new ChatHistory();

    string dynamicTriagePrompt = $"""
    你是一个低空物流中台的【中央决策军师】。
    请阅读下述实时机载物理遥测事实，并结合从公司知识库中动态召回的安全行动依据，自主抉择是否调用物理干涉工具。

    【当前机载物理遥测事实】
    机号：{telemetry.DroneId}
    坐标：({telemetry.Longitude}, {telemetry.Latitude})
    当前电池温：{telemetry.BatteryTemp} ℃
    货舱载荷：{telemetry.CurrentPayload}
    飞行器状态：{telemetry.FlightStatus}

    {ragContext}

    【核心指令】
    请严格基于上述召回的依据进行逻辑判定。如果满足动作红线，请调用工具。
    成功调用工具并拿到物理派工单后，请用军工塔台口吻向全网播报最终的物理干涉回执，并在广播中明确指出你所引用的公司章程章节。
    """;

    history.AddUserMessage(dynamicTriagePrompt);
    var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

    Console.WriteLine("[中央军师 DeepSeek-V3.2 正在研判事实（基于动态常识库），静默寻址干涉工具...]");
    var finalResult = await chatService.GetChatMessageContentAsync(history, settings, kernel);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n🎙️ [地面指挥部 全网广播]:\n{finalResult.Content}\n");
    Console.ResetColor();
}

return;




// 💥 网络底层拦截器（必须放在 Program.cs 文件最底部）
public class SiliconFlowInterceptor : DelegatingHandler
{
    public SiliconFlowInterceptor()
    {
        // 作用 1：无条件放行 SSL 证书，解决本地代理软件引发的 TLS 握手失败
        InnerHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 作用 2：动态路由重写
        // 拦截底层发往 openai.com 的所有请求，强行将 Host 篡改为硅基流动的服务器
        if (request.RequestUri != null && request.RequestUri.Host.Contains("openai.com"))
        {
            request.RequestUri = new Uri($"https://api.siliconflow.cn{request.RequestUri.PathAndQuery}");
        }
        return await base.SendAsync(request, cancellationToken);
    }
}