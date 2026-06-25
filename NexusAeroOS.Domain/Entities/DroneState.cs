namespace NexusAeroOS.Domain.Entities;

// 无人机的物理状态枚举
public enum DroneStatus
{
    Idle,       // 空闲/待命
    Flying,     // 正常飞行/任务中
    Emergency,  // 紧急情况/遇险
    Maintenance // 维护中
}

// 无人机状态实体
public class DroneState
{
    public string DroneId { get; set; } = string.Empty;
    public DroneStatus Status { get; set; }
    public string CurrentMission { get; set; } = "无任务";
    public string Coordinates { get; set; } = "0,0"; // 简化的坐标
}