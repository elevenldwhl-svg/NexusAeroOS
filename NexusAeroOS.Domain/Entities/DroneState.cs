using System.Collections.Generic;

namespace NexusAeroOS.Domain.Entities;

// 💥 升级：精细化低空物流全生命周期状态机
public enum DroneStatus
{
    Idle,             // 0: 原籍基地待命慢充中
    FlyingToPickup,   // 1: 正在空载飞往发货枢纽点
    FlyingToDeliver,  // 2: 正在满载飞往送货终点
    ReturningHome,    // 3: 任务终结，正在低电量自主回巢补电中
    Emergency,        // 4: AEB 底盘硬熔断锁死
    Maintenance       // 5: 返厂例检
}

public class DroneState
{
    public string DroneId { get; set; } = string.Empty;
    public DroneStatus Status { get; set; }
    public string CurrentMission { get; set; } = "驻停备勤";
    public string Coordinates { get; set; } = "113.9365, 22.5412";

    // 💥 核心：锁定原籍基地。无论飞到天涯海角，卸完货必须滚回这个坐标补电！
    public string HomeBaseCoordinates { get; set; } = "113.9365, 22.5412";

    public double BatteryLevel { get; set; } = 100.0;
    public double CoreTemperature { get; set; } = 36.5;
    public double MaxPayload { get; set; } = 30.0;
    public double CurrentPayload { get; set; } = 0.0;
    public string DroneModel { get; set; } = "星河·丰翼X100";

    public string? CurrentTaskId { get; set; }
    public string? CurrentOrderId { get; set; }

    // 暂存业务终点，供一阶段送货完结后掉头使用
    public string? DeliveryDestinationCoords { get; set; }

    public Queue<string> WaypointQueue { get; set; } = new();
    public string? CurrentTargetWaypoint { get; set; }
    public bool IsInMotion { get; set; } = false;
}