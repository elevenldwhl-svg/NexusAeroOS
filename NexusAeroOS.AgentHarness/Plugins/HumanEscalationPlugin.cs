using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace NexusAeroOS.AgentHarness.Plugins;

public class HumanEscalationPlugin
{
    [KernelFunction("EscalateToHuman")]
    [Description("当现场情况超出系统已知预案，或者丢失关键遥测数据导致无法进行自动化物理干涉时，立刻调用此工具将控制权移交至人工专家席位。")]
    public async Task<string> EscalateToHumanAsync(
        [Description("对当前紧急情况的简要总结摘要")] string situationSummary,
        [Description("当前缺失的关键数据是什么（如无缺失填无）")] string missingData)
    {
        // 模拟告警推送至人工指挥中心的大屏
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n🚨 [监控大屏红色警报触发] 接收到 AI 中枢的求救信号！");
        Console.WriteLine($"   ➔ [警报摘要]: {situationSummary}");
        Console.WriteLine($"   ➔ [缺失数据]: {missingData}");
        Console.WriteLine($"   ➔ [系统状态]: 人工专家已强行接入通讯频道...");
        Console.ResetColor();

        // 将系统状态的改变，作为结果返回给 AI 大脑
        return "已成功接通星河物流人工指挥部，专家正在接入。请向飞行员广播这一消息，并安抚其情绪。";
    }
}