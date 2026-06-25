using System.Collections.Concurrent;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class HitlApprovalService
{
    // 存放挂起任务的字典
    private readonly ConcurrentDictionary<string, TaskCompletionSource<(bool IsApproved, string Feedback)>> _pendingApprovals = new();

    // 工具调用此方法，AI 线程将会在这里暂停等待 (await)
    public Task<(bool IsApproved, string Feedback)> WaitForApprovalAsync(string taskId)
    {
        var tcs = new TaskCompletionSource<(bool, string)>();
        _pendingApprovals.TryAdd(taskId, tcs);
        return tcs.Task; // 返回 Task，让外层 await
    }

    // 前端点击后调用此方法，唤醒对应的 AI 线程
    public bool ResolveApproval(string taskId, bool isApproved, string feedback)
    {
        if (_pendingApprovals.TryRemove(taskId, out var tcs))
        {
            tcs.SetResult((isApproved, feedback)); // 设置结果，唤醒线程！
            return true;
        }
        return false;
    }
}