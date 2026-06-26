using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NexusAeroOS.AgentHarness.Agents;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.AgentHarness.Plugins;
using NexusAeroOS.Domain.Entities;
using NexusAeroOS.Host.Models;

namespace NexusAeroOS.Host.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly TelemetryParsingAgent _parsingAgent;
    private readonly KnowledgeRetrievalService _retrievalService;
    private readonly Kernel _kernel;
    private readonly AuditLogRepository _auditRepository;
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly FleetRegistryService _fleetRegistry;
    private readonly HitlApprovalService _hitlService;
    private readonly DroneOnboardAgent _onboardAgent;
    private readonly AirspaceService _airspaceService;

    public TelemetryController(
        TelemetryParsingAgent parsingAgent,
        KnowledgeRetrievalService retrievalService,
        Kernel kernel,
        AuditLogRepository auditRepository,
        IAgentEventBroadcaster broadcaster,
        FleetRegistryService fleetRegistry,
        HitlApprovalService hitlService,
        DroneOnboardAgent onboardAgent,
        AirspaceService airspaceService)
    {
        _parsingAgent = parsingAgent;
        _retrievalService = retrievalService;
        _kernel = kernel;
        _auditRepository = auditRepository;
        _broadcaster = broadcaster;
        _fleetRegistry = fleetRegistry;
        _hitlService = hitlService;
        _onboardAgent = onboardAgent;
        _airspaceService = airspaceService;
    }

    [HttpGet("airspace")]
    public IActionResult GetAirspace() => Ok(_airspaceService.GetActiveZones());

    [HttpPost("notam/trigger")]
    public async Task<IActionResult> TriggerNotam(CancellationToken cancellationToken)
    {
        // 1. 在物理世界划定禁飞区
        var zone = _airspaceService.IssueNotamRestriction();
        await _broadcaster.BroadcastThoughtAsync("System", "ATC", "NOTAM_Issued", $"空管局下发紧急净空令: [{zone.ZoneId}]");

        // 2. 唤醒中央大脑，盘点谁在天上飞！
        string currentDroneId = "UAV-003";
        var uav003 = _fleetRegistry.GetFleetSnapshot().First(d => d.DroneId == currentDroneId);

        var chatService = _kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
        var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();

        history.AddSystemMessage($"""
        你是中央塔台大脑。空管局刚刚突发颁布了禁飞令："{zone.Reason}"。
        正在执行任务的 {currentDroneId} 当前坐标为：{uav003.Coordinates}。
        它原定终点在 113.9500, 22.5500。

        【你的避障决策链】：
        1. 你必须先调用 `AirspaceOps.VerifyRouteSafety` 工具，测试它直飞终点 "113.9500, 22.5500" 是否安全。
        2. 如果工具报错说穿过了禁飞区，你绝对不能坐视它撞毁！
           你必须在脑中把航点往北偏离（例如往北绕行点集："113.9310,22.5490; 113.9420,22.5490; 113.9500,22.5500"），
           **再次调用 `VerifyRouteSafety` 验算**！直到工具返回 SAFE！
        3. 算出一组安全的折线后，立刻调用 `FleetOps.DispatchDrone` 将新航点下发给 {currentDroneId} 接管！
        """);

        history.AddUserMessage("通告已生效，请立刻核查全网飞行资产安全！");
        var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        _kernel.FunctionInvocationFilters.Clear();
        _kernel.FunctionInvocationFilters.Add(new AgentObservabilityFilter(_broadcaster, "System"));

        var res = await chatService.GetChatMessageContentAsync(history, settings, _kernel, cancellationToken);
        await _broadcaster.BroadcastThoughtAsync(currentDroneId, "System", "FinalDecision", res.Content ?? "已改航");
        return Ok();
    }

    // 供前端大屏首次加载时拉取机队初始状态
    [HttpGet("fleet")]
    public IActionResult GetFleetSnapshot()
    {
        return Ok(_fleetRegistry.GetFleetSnapshot());
    }

    [HttpPost("report")]
    public async Task<IActionResult> Report([FromBody] TelemetryReportRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"\n[API 接收] 收到一线急呼: {request.PilotVoice}");
        string currentDroneId = "UAV-X900";

        // ====================================================================
        // 💥 核心联动：报警一响，立刻将 UAV-X900 状态切为紧急并广播给大屏
        // ====================================================================
        _fleetRegistry.UpdateDroneMission(currentDroneId, DroneStatus.Emergency, $"发生特情：{request.PilotVoice}");
        await _broadcaster.BroadcastFleetStatusAsync(_fleetRegistry.GetFleetSnapshot());

        // 挂载升级后的多机调度插件
        _kernel.Plugins.AddFromObject(new FleetDispatchPlugin(_fleetRegistry, _broadcaster, _hitlService, _onboardAgent, _kernel, _airspaceService), "FleetOps");
        _kernel.Plugins.AddFromType<UavEmergencyPlugin>("EmergencyOps");
        _kernel.Plugins.AddFromType<HumanEscalationPlugin>("HumanFallback");

        var ragPlugin = KernelPluginFactory.CreateFromFunctions("KnowledgeBase", new[]
        {
            _kernel.CreateFunctionFromMethod(
                method: async (string query) => await _retrievalService.GetRagContextAsync(query),
                functionName: "SearchCompanySOP",
                description: "当遇到未知险情、设备异常、参数缺失或不知道如何处置时，搜索公司特种应急预案与规章制度获取行动依据。"
            )
        });
        _kernel.Plugins.Add(ragPlugin);

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        string masterPrompt = $"""
        你是星河物流的【中央应急指挥大脑】。
        当前向你发出紧急呼叫的无人机编号为：{currentDroneId}。

        【你的最高裁决与调度逻辑】
        1. 意图研判：阅读 {currentDroneId} 的呼叫，判断危机程度。如果需要规章依据，调用 `KnowledgeBase.SearchCompanySOP`。
        2. 物理干涉：对于 {currentDroneId} 本身，如果需要安抚或停机，调用 `EmergencyOps` 工具。
        3. 多机协同救援：如果这是一次严重的“炸机”或“屏幕全黑”事件，你必须主动发起协同救援：
        a) 立刻调用 `FleetOps.CheckFleetStatus` 获取全局无人机状态。
        b) 寻找一台状态为 `Idle` 的空闲无人机（例如 UAV-001）。
        c) 💥【空间避障规约】：由于深圳南山区有大量高层摩天大楼（如华润春笋大厦、腾讯滨海大厦等），为了防止救援撞楼，你绝对不能下发两点一线的直线指令！
           你必须根据无人机当前的起点坐标，规划出一条折线航线（至少包含 3 个转弯航点，最终到达遇险机的坐标 113.9250, 22.5300）。

           调用 `FleetOps.DispatchDrone` 时，`waypointsRoute` 参数格式必须是用分号隔离的多个经纬度字符串。
           例如：若起点在 113.9365, 22.5412，你可以规划绕行点：“113.9310,22.5450; 113.9220,22.5400; 113.9250,22.5300”。
        4. 最终答复：用沉稳的塔台口吻回复，告知它你已经派了哪一架友军无人机正在赶来。
        """;

        history.AddSystemMessage(masterPrompt);
        history.AddUserMessage(request.PilotVoice);

        var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
        _kernel.FunctionInvocationFilters.Add(new AgentObservabilityFilter(_broadcaster, currentDroneId));

        Console.WriteLine("\n[中央大脑] 正在进行多机调度意图研判...");
        var finalResult = await chatService.GetChatMessageContentAsync(history, settings, _kernel, cancellationToken);

        // 💥 核心联动：将大模型的最终塔台通告直接广播给大屏
        await _broadcaster.BroadcastThoughtAsync(currentDroneId, "System", "FinalDecision", finalResult.Content ?? "调度完成");

        // 审计落盘保持不变
        var auditLog = new FlightAuditLog(Guid.NewGuid(), currentDroneId, "Agent-Routing-Decision", request.PilotVoice, "思考链已通过大屏实时广播", "Auto-Routed", true, DateTime.UtcNow);
        await _auditRepository.SaveLogAsync(auditLog);

        return Ok(new TelemetryReportResponse(true, finalResult.Content ?? "干涉已执行", null));
    }


    public class HitlResolutionRequest { public string TaskId { get; set; } public bool IsApproved { get; set; } public string Feedback { get; set; } }

    [HttpPost("hitl/resolve")]
    public IActionResult ResolveHitl([FromBody] HitlResolutionRequest request)
    {
        bool success = _hitlService.ResolveApproval(request.TaskId, request.IsApproved, request.Feedback);
        return success ? Ok() : NotFound("任务过期");
    }
}