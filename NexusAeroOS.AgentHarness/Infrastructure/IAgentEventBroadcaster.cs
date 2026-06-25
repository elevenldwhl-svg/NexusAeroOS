using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public interface IAgentEventBroadcaster
{
    // 广播 AI 的思考过程（准备调用工具）
    Task BroadcastThoughtAsync(string droneId, string pluginName, string functionName, string arguments);

    // 广播 AI 的执行结果（工具调用完毕）
    Task BroadcastActionCompleteAsync(string droneId, string functionName);

    // 广播全盘机队最新状态矩阵
    Task BroadcastFleetStatusAsync(IEnumerable<DroneState> fleet);

    // 广播需要人工介入的授权请求
    Task BroadcastHitlRequestAsync(string taskId, string targetDroneId, string mission);
}