namespace NexusAeroOS.Domain.Entities;

public record FlightAuditLog
(
    Guid Id,
    /// <summary>
    /// 无人机ID
    /// </summary>
    string DroneId,
    /// <summary>
    /// 决策类型（调用工具、安抚、警报）
    /// </summary>
    string ActionType,
    /// <summary>
    /// 原始输入
    /// </summary>
    string UserInput,
    /// <summary>
    /// AI 的思考链 (Thought)
    /// </summary>
    string AiReasoning,
    /// <summary>
    /// 实际调用的工具
    /// </summary>
    string ToolExecuted,
    /// <summary>
    /// 是否为人机协同(HITL)警报
    /// </summary>
    bool IsCritical,

    DateTime Timestamp
)
{
    public FlightAuditLog() : this(Guid.NewGuid(), string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false, DateTime.UtcNow) { }
}