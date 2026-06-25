using Microsoft.SemanticKernel;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class AgentObservabilityFilter : IFunctionInvocationFilter
{
    private readonly IAgentEventBroadcaster _broadcaster;
    private readonly string _droneId;

    // 💥 构造函数注入广播器和当前无人机 ID
    public AgentObservabilityFilter(IAgentEventBroadcaster broadcaster, string droneId)
    {
        _broadcaster = broadcaster;
        _droneId = droneId;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        var parameters = string.Join(", ", context.Arguments.Select(a => $"{a.Key} = '{a.Value}'"));

        // 1. 控制台打印与实时大屏广播（思考切面）
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n[🔍 思考切面] 准备调用: 【{context.Function.PluginName}.{context.Function.Name}】");
        Console.ResetColor();

        await _broadcaster.BroadcastThoughtAsync(_droneId, context.Function.PluginName, context.Function.Name, parameters);

        // 2. 放行执行
        await next(context);

        // 3. 控制台打印与实时大屏广播（执行切面）
        Console.ForegroundColor = ConsoleColor.DarkMagenta;
        Console.WriteLine($"[✅ 执行切面] 工具 【{context.Function.Name}】 调用完毕。");
        Console.ResetColor();

        await _broadcaster.BroadcastActionCompleteAsync(_droneId, context.Function.Name);
    }
}