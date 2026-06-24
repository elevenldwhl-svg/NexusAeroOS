namespace NexusAeroOS.Domain.Entities;

/// <summary>
/// 无人机飞行器实时物理遥测载荷（大模型意图解析的终极归宿）
/// </summary>
public record UavTelemetry(
    string DroneId,          // 机载识别码，如 "UAV-8848"
    double Latitude,         // 实时经度
    double Longitude,        // 实时纬度
    double BatteryTemp,      // 电池核心温度（工作红线：-10℃ ~ 65℃）
    double RemainingPower,   // 剩余电量百分比（0.00 ~ 1.00）
    string CurrentPayload,   // 当前货舱载荷类型，如 "Standard_Parcel", "Urgent_Medical_Plasma"
    string FlightStatus      // 飞行器机载状态，如 "Cruising", "Hovering", "Emergency_Warning"
);

/// <summary>
/// 智能体中台向WMS/TMS下达的强类型物理干涉指令
/// </summary>
public record EmergencyDispatchOrder(
    string SourceDroneId,        // 涉事机ID
    string TargetWarehouseId,    // 划定的接驳仓ID
    string AllocatedBackupSku,   // 强行锁定的物资SKU（如备用电池）
    string ActionReason,         // 大模型给出的判决理由说明书
    DateTime DispatchedAt
);