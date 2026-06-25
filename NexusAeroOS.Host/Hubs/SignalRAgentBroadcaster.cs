using Microsoft.AspNetCore.SignalR;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.Host.Hubs;

public class SignalRAgentBroadcaster : IAgentEventBroadcaster
{
    private readonly IHubContext<AgentControlHub> _hubContext;

    public SignalRAgentBroadcaster(IHubContext<AgentControlHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastThoughtAsync(string droneId, string pluginName, string functionName, string arguments)
    {
        // 向所有连接到大屏的前端客户端发送 "ReceiveAgentThought" 事件
        await _hubContext.Clients.All.SendAsync("ReceiveAgentThought", new
        {
            DroneId = droneId,
            Plugin = pluginName,
            Function = functionName,
            Arguments = arguments,
            Timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastActionCompleteAsync(string droneId, string functionName)
    {
        // 向大屏发送 "ReceiveAgentActionComplete" 事件
        await _hubContext.Clients.All.SendAsync("ReceiveAgentActionComplete", new
        {
            DroneId = droneId,
            Function = functionName,
            Timestamp = DateTime.UtcNow
        });
    }

    // 将整个机队的状态矩阵推送到前端 WebSocket 监听端 "ReceiveFleetUpdate"
    public async Task BroadcastFleetStatusAsync(IEnumerable<DroneState> fleet)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveFleetUpdate", fleet);
    }

    public async Task BroadcastHitlRequestAsync(string taskId, string targetDroneId, string mission)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveHitlRequest", new
        {
            TaskId = taskId,
            TargetDroneId = targetDroneId,
            Mission = mission
        });
    }
}