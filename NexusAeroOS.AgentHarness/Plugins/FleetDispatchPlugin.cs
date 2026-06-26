// 替换文件开头的 using 与构造函数
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using NexusAeroOS.AgentHarness.Agents;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.AgentHarness.Plugins;

public class FleetDispatchPlugin
{
    private readonly FleetRegistryService _registry;
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly HitlApprovalService _hitlService;
    private readonly DroneOnboardAgent _onboardAgent; // 引入机载Agent
    private readonly Kernel _centralKernel; // 需要将母体传过去以便克隆
    private readonly AirspaceService _airspace;
    private  readonly MissionRepository? _missionRepo; // 注入仓储

    public FleetDispatchPlugin(
            FleetRegistryService registry, IAgentEventBroadcaster broadcaster,
            HitlApprovalService hitlService, DroneOnboardAgent onboardAgent,
            Kernel centralKernel, AirspaceService airspace,
            MissionRepository? missionRepo = null) // 💥 神技在此
    {
        _registry = registry; _broadcaster = broadcaster; _hitlService = hitlService;
        _onboardAgent = onboardAgent; _centralKernel = centralKernel; _airspace = airspace;
        _missionRepo = missionRepo;
    }

    [KernelFunction("CheckFleetStatus")]
    public string CheckFleetStatus()
    {
        // 💥 内存初筛：只把 Idle 且电量 > 50% 的飞机简报推给 AI！
        // 把发给大模型的 Token 负载从 2000 个词强行抽脂到 120 个词！
        var avail = _registry.GetFleetSnapshot()
            .Where(d => d.Status == DroneStatus.Idle && d.BatteryLevel > 50)
            .Select(d => $"{d.DroneId}[载重:{d.MaxPayload}kg, 坐标:{d.Coordinates}]");

        return $"可用备勤资产(已剔除故障及远程机)：{string.Join(" | ", avail)}";
    }

    [KernelFunction("DispatchDrone")]
    [Description("向指定的空闲无人机下发多航点协同调度折线指令。")]
    public async Task<string> DispatchDrone(
            [Description("被选中的可用无人机ID")] string targetDroneId,
            [Description("分号隔离的复杂避障折线点集字符串，例如 '113.9310,22.5450; 113.9220,22.5400; 113.9250,22.5300'")] string waypointsRoute,
            [Description("具体的任务意图描述")] string mission)
    {
        var startNode = _registry.GetFleetSnapshot().FirstOrDefault(d => d.DroneId == targetDroneId);
        string startCoords = startNode?.Coordinates ?? "113.9365, 22.5412";

        var (isSafe, report) = _airspace.TestRouteSafety(startCoords, waypointsRoute);
        if (!isSafe)
        {
            // 绝不弹窗打扰人类！直接把报错文字当做函数返回值退给大模型！
            return $"【调度被空管系统硬件驳回】你规划的折线撞墙了！{report}。请调取南山区坐标，重新偏离经纬度再次生成 waypointsRoute 发单！";
        }

        string taskId = Guid.NewGuid().ToString();
        await _broadcaster.BroadcastHitlRequestAsync(taskId, targetDroneId, $"拟指派折线航线[{waypointsRoute}]。意图：{mission}");

        var (isApproved, feedback) = await _hitlService.WaitForApprovalAsync(taskId);

        if (isApproved)
        {
            // 💥 直接将大模型绞尽脑汁规划出来的避障航线送入时空引擎！
            _registry.LaunchDroneMotion(targetDroneId, waypointsRoute, mission);
            await _broadcaster.BroadcastFleetStatusAsync(_registry.GetFleetSnapshot());

            string edgeReport = await _onboardAgent.ExecuteMissionAsync(_centralKernel, targetDroneId, mission);
            return $"【调度成功】避障航线已成功锁定。{targetDroneId} 边缘引擎点火，正切入多航点自治绕飞模式。机载小脑上报：{edgeReport}";
        }
        else
        {
            return $"【调度被拦截】理由：{feedback}";
        }
    }

    [KernelFunction("SilentAutoDispatch")]
    [Description("全自主静默直飞两段串联总线。")]
    public async Task<string> SilentAutoDispatch(
        string targetDroneId, string waypointsRoute, string deliveryDest, string orderId, string payloadType, double weightKg, string aiReasoningSummary)
    {
        var node = _registry.GetFleetSnapshot().FirstOrDefault(d => string.Equals(d.DroneId, targetDroneId.Trim(), StringComparison.OrdinalIgnoreCase));
        string startCoords = node?.Coordinates ?? "113.9365, 22.5412";

        var safety = _airspace.TestRouteSafety(startCoords, waypointsRoute);
        if (!safety.IsSafe) waypointsRoute = _airspace.GetGuaranteedSafeCorridor(startCoords, waypointsRoute.Split(';').Last().Trim());

        string taskId = Guid.NewGuid().ToString();
        if (node != null)
        {
            node.CurrentTaskId = taskId;
            node.CurrentOrderId = orderId;
            node.CurrentPayload = weightKg;
            // 💥 注入核心终点锚点！供一阶段接货落地后，时空引擎自动掉头
            node.DeliveryDestinationCoords = deliveryDest.Trim();
        }

        if (_missionRepo != null)
        {
            await _missionRepo.RecordStartAsync(new MissionLogEntity
            {
                TaskId = taskId,
                OrderId = orderId,
                DroneId = targetDroneId,
                PayloadType = payloadType,
                WeightKg = weightKg,
                StartCoords = startCoords,
                DestCoords = deliveryDest.Trim(),
                Waypoints = waypointsRoute,
                AiReasoning = aiReasoningSummary
            });
        }

        // 发射！状态机会自动接管后面的一阶段接货、二阶段送货、三阶段回巢！
        _registry.LaunchDroneMotion(targetDroneId, waypointsRoute, $"[单号:{orderId}] 前往取货点");
        await _broadcaster.BroadcastFleetStatusAsync(_registry.GetFleetSnapshot());
        return $"【低空串联点火成功】资产 {targetDroneId} 已起飞，进入接货航道。最终卸货目标：{deliveryDest}";
    }
}