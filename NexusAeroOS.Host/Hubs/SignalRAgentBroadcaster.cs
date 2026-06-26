using Microsoft.AspNetCore.SignalR;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.Host.Hubs;

public class SignalRAgentBroadcaster : IAgentEventBroadcaster
{
    private readonly IHubContext<AgentControlHub> _hubContext;
    private readonly ThoughtLogRepository _thoughtRepo; // 💥 追加注入思维记录仓储

    public SignalRAgentBroadcaster(IHubContext<AgentControlHub> hubContext, ThoughtLogRepository thoughtRepo)
    {
        _hubContext = hubContext;
        _thoughtRepo = thoughtRepo;
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

        // 💥 核心解耦：后台开辟独立弃置线程强行写盘！绝不让硬盘 IO 拖慢前端毫秒级推流！
        _ = _thoughtRepo.RecordThoughtAsync(droneId, pluginName, functionName, arguments);
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

        // 💥 同步将物理动作生效的信号切片也记入 PostgreSQL 硬盘
        _ = _thoughtRepo.RecordThoughtAsync(droneId, "System", $"[物理动作生效] {functionName}", "Success");
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