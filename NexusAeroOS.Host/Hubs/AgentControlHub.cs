using Microsoft.AspNetCore.SignalR;

namespace NexusAeroOS.Host.Hubs;

// 供前端监控大屏连接的 WebSocket 枢纽
public class AgentControlHub : Hub
{
    // 前端连接成功后可以在这里处理逻辑，目前保持为空即可
}