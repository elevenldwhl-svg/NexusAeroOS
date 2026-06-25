using System.Collections.Concurrent;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class FleetRegistryService
{
    // 使用线程安全的字典来管理多机状态
    private readonly ConcurrentDictionary<string, DroneState> _fleet = new();

    public FleetRegistryService()
    {
        // 系统初始化时，预置 5 台测试无人机
        _fleet.TryAdd("UAV-001", new DroneState { DroneId = "UAV-001", Status = DroneStatus.Idle, Coordinates = "120.15, 30.28" });
        _fleet.TryAdd("UAV-002", new DroneState { DroneId = "UAV-002", Status = DroneStatus.Idle, Coordinates = "120.16, 30.29" });
        _fleet.TryAdd("UAV-003", new DroneState { DroneId = "UAV-003", Status = DroneStatus.Flying, CurrentMission = "常规快件派送", Coordinates = "120.20, 30.30" });
        // UAV-X900 也就是我们之前一直模拟的那台可能随时出故障的飞机
        _fleet.TryAdd("UAV-X900", new DroneState { DroneId = "UAV-X900", Status = DroneStatus.Flying, CurrentMission = "特种医疗载荷", Coordinates = "120.10, 30.25" });
        _fleet.TryAdd("UAV-X901", new DroneState { DroneId = "UAV-X901", Status = DroneStatus.Maintenance, CurrentMission = "返厂检修", Coordinates = "基地" });
    }

    // 获取整个机队的快照视图
    public IEnumerable<DroneState> GetFleetSnapshot()
    {
        return _fleet.Values;
    }

    // 更新单台无人机的状态与任务（供 AI 调度时调用）
    public bool UpdateDroneMission(string droneId, DroneStatus newStatus, string newMission)
    {
        if (_fleet.TryGetValue(droneId, out var drone))
        {
            drone.Status = newStatus;
            drone.CurrentMission = newMission;
            return true;
        }
        return false;
    }
}