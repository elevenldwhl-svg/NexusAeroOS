using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NexusAeroOS.AgentHarness.Infrastructure;
using NexusAeroOS.AgentHarness.Plugins;

namespace NexusAeroOS.AgentHarness.Agents;

public class DroneOnboardAgent
{
    private readonly IAgentEventBroadcaster _broadcaster;

    public DroneOnboardAgent(IAgentEventBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    // 核心逻辑：执行被下发的子任务
    public async Task<string> ExecuteMissionAsync(Kernel centralKernel, string myDroneId, string mission)
    {
        // 💥 架构师神技：从中央大脑克隆出一个干净的子核心（Kernel）
        var localKernel = centralKernel.Clone();

        // 1. 拔掉中央大脑的所有工具（机载小脑无权调度别人，也无权查阅公司SOP大本营）
        localKernel.Plugins.Clear();
        // 2. 插入机载专属工具
        localKernel.Plugins.AddFromType<FlightControlPlugin>("EdgeHardware");

        // 3. 替换神经探针，将思考主体改为当前无人机的 ID，这样大屏上就能区分是谁在思考！
        localKernel.FunctionInvocationFilters.Clear();
        localKernel.FunctionInvocationFilters.Add(new AgentObservabilityFilter(_broadcaster, myDroneId));

        var chatService = localKernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        // 💥 为机载小脑设定独立的人格与指令
        history.AddSystemMessage($"""
        你是编号为 {myDroneId} 的【机载边缘自治 AI】。
        你刚刚接到了中央塔台下发的紧急任务："{mission}"。
        
        你的执行逻辑：
        1. 必须调用 `EdgeHardware.LockTargetAndPlotRoute` 工具锁定目标坐标 (假设目标在 120.10, 30.25)。
        2. 工具执行完毕后，向中央塔台汇报你的行动结果，用精干、专业的机组通讯口吻。
        """);

        // 广播小脑启动的日志（作为提示）
        await _broadcaster.BroadcastThoughtAsync(myDroneId, "EdgeAI", "BootSequence", "边缘自治节点激活，正在接管并解析中央塔台指令...");

        var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

        // 激活机载小脑进行独立思考与执行！
        var result = await chatService.GetChatMessageContentAsync(history, settings, localKernel);

        return result.Content ?? "行动完成";
    }
}