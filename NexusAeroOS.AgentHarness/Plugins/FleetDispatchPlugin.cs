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
    private readonly DroneOnboardAgent _onboardAgent; // 💥 引入机载Agent
    private readonly Kernel _centralKernel; // 需要将母体传过去以便克隆

    public FleetDispatchPlugin(
        FleetRegistryService registry,
        IAgentEventBroadcaster broadcaster,
        HitlApprovalService hitlService,
        DroneOnboardAgent onboardAgent,
        Kernel centralKernel)
    {
        _registry = registry;
        _broadcaster = broadcaster;
        _hitlService = hitlService;
        _onboardAgent = onboardAgent;
        _centralKernel = centralKernel;
    }

    [KernelFunction("CheckFleetStatus")]
    [Description("获取当前全局所有无人机的实时状态列表。")]
    public string CheckFleetStatus()
    {
        return JsonSerializer.Serialize(_registry.GetFleetSnapshot(), new JsonSerializerOptions { WriteIndented = true });
    }

    [KernelFunction("DispatchDrone")]
    [Description("向指定的空闲无人机下发调度指令。")]
    public async Task<string> DispatchDrone(
        [Description("被选中的可用无人机ID")] string targetDroneId,
        [Description("具体的任务指令")] string mission)
    {
        string taskId = Guid.NewGuid().ToString();
        await _broadcaster.BroadcastHitlRequestAsync(taskId, targetDroneId, mission);

        var (isApproved, feedback) = await _hitlService.WaitForApprovalAsync(taskId);

        if (isApproved)
        {
            _registry.UpdateDroneMission(targetDroneId, DroneStatus.Flying, mission);
            await _broadcaster.BroadcastFleetStatusAsync(_registry.GetFleetSnapshot());

            // 💥 魔法时刻：人类授权后，中央大脑不是直接返回，而是唤醒小脑执行！
            string edgeReport = await _onboardAgent.ExecuteMissionAsync(_centralKernel, targetDroneId, mission);

            // 将小脑的行动结果层层上报，返回给中央大脑
            return $"【调度成功】人类已放行。并且 {targetDroneId} 的机载AI已接管任务并传回报告：{edgeReport}";
        }
        else
        {
            return $"【调度被拦截】人类指挥官拒绝了此申请。理由：{feedback}。";
        }
    }
}