using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NexusAeroOS.Domain.Entities; // 引用纯洁的 Domain 实体
using NexusAeroOS.AgentHarness.Infrastructure;

namespace NexusAeroOS.AgentHarness.Agents;

public class TelemetryParsingAgent
{
    private readonly Kernel _kernel;

    public TelemetryParsingAgent(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<UavTelemetry?> ExtractFromPilotVoiceAsync(string pilotVoiceContent)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        string prompt = $$"""
            你是一个嵌入在波音低空飞控中台里的【语义-物理流转化单元】。
            请根据以下地面飞控员的慌乱语音转白话文本，精准提取出无人机的物理遥测参数。

            【飞控语音输入】
            "{{pilotVoiceContent}}"

            【输出契约】
            1. 严格输出符合下述字段定义的 JSON 结构。
            2. 绝对不允许在 JSON 前后吐出任何一个字的问候语、解释或 Markdown 标记！

            { 
               "DroneId": "STRING (机号)",
               "Latitude": DOUBLE (纬度),
               "Longitude": DOUBLE (经度),
               "BatteryTemp": DOUBLE (核心温),
               "RemainingPower": DOUBLE (剩余电量百分比，如 0.12),
               "CurrentPayload": "STRING (载荷类型描述)",
               "FlightStatus": "Cruising 或者 Hovering 或者 Emergency_Warning"
            }
            """;

        var response = await chatService.GetChatMessageContentAsync(prompt);
        string rawAiContent = response.Content ?? "";

        return RobustJsonParser.IncinerateAndParse<UavTelemetry>(rawAiContent);
    }
}