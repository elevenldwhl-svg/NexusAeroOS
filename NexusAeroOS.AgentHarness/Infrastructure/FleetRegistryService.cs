using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class FleetRegistryService
{
    private readonly ConcurrentDictionary<string, DroneState> _fleet = new(StringComparer.OrdinalIgnoreCase);

    public void LoadFleetFromDatabase(IEnumerable<DroneState> dbDrones)
    {
        _fleet.Clear();
        foreach (var d in dbDrones) _fleet.TryAdd(d.DroneId, d);
    }

    public IEnumerable<DroneState> GetFleetSnapshot() => _fleet.Values;

    // 💥 统一收口发车接口：直接让插件调用，默认强制切入【阶段一：空载前往发货点】
    public bool LaunchDroneMotion(string id, string route, string mi)
    {
        if (_fleet.TryGetValue(id.Trim(), out var d))
        {
            d.Status = DroneStatus.FlyingToPickup; // 1: 取货
            d.CurrentMission = mi;
            d.WaypointQueue.Clear();
            foreach (var p in route.Split(';', StringSplitOptions.RemoveEmptyEntries)) d.WaypointQueue.Enqueue(p.Trim());
            if (d.WaypointQueue.TryDequeue(out var fp)) { d.CurrentTargetWaypoint = fp; d.IsInMotion = true; }
            return true;
        }
        return false;
    }

    public bool UpdateDroneMission(string id, DroneStatus st, string mi)
    {
        if (_fleet.TryGetValue(id.Trim(), out var d)) { d.Status = st; d.CurrentMission = mi; return true; }
        return false;
    }

    public async Task<bool> TickAllDronesMotionAsync(AirspaceService airspace, MissionRepository missionRepo, DroneRepository droneRepo)
    {
        bool moved = false; double step = 0.0004;

        // 💥 修复1：绝对不能加 .Where() 过滤！必须遍历全网20架飞机，否则呆在家里的飞机充不了电！
        foreach (var d in _fleet.Values)
        {
            // 💥 修复2：补回丢失的数字停机坪慢充回血逻辑
            if (d.Status == DroneStatus.Idle && !d.IsInMotion)
            {
                if (d.BatteryLevel < 100.0 || d.CoreTemperature > 36.5)
                {
                    d.BatteryLevel = Math.Min(100.0, Math.Round(d.BatteryLevel + 3.5, 1));
                    d.CoreTemperature = Math.Max(36.5, Math.Round(d.CoreTemperature - 2.1, 1));
                    await droneRepo.SaveDroneStateToDatabaseAsync(d);
                    moved = true;
                }
                continue; // 充完电，这架飞机本帧计算结束，直接跳过
            }

            if (d.IsInMotion && !string.IsNullOrEmpty(d.CurrentTargetWaypoint))
            {
                var cur = d.Coordinates.Split(','); var tgt = d.CurrentTargetWaypoint!.Split(',');
                double cLng = double.Parse(cur[0]); double cLat = double.Parse(cur[1]);
                double tLng = double.Parse(tgt[0]); double tLat = double.Parse(tgt[1]);
                double dist = Math.Sqrt((tLng - cLng) * (tLng - cLng) + (tLat - cLat) * (tLat - cLat));

                if (dist <= step)
                {
                    d.Coordinates = d.CurrentTargetWaypoint;

                    // 扣减仿真耗电与核心升温
                    d.BatteryLevel = Math.Max(0, Math.Round(d.BatteryLevel - 0.6, 1));
                    d.CoreTemperature = Math.Min(95.0, Math.Round(d.CoreTemperature + 0.4, 1));

                    if (d.WaypointQueue.TryDequeue(out var np))
                    {
                        d.CurrentTargetWaypoint = np;
                    }
                    else
                    {
                        // 💥 修复3：当前航段跑完了，绝对不能强行变回 Idle！
                        // 必须把它丢给生命周期引擎，让它自己判断是要装货掉头、卸货回娘家、还是彻底休眠！
                        await HandleLifecycleTransitionAsync(d, airspace, missionRepo);
                    }
                }
                else
                {
                    double r = step / dist;
                    double nLng = cLng + (tLng - cLng) * r; double nLat = cLat + (tLat - cLat) * r;
                    if (airspace.IsPointInAnyNoFlyZone(nLng, nLat))
                    {
                        d.Status = DroneStatus.Emergency; d.CurrentMission = "【AEB防撞悬停】前方静态红线阻断";
                        d.IsInMotion = false; d.WaypointQueue.Clear();
                        await missionRepo.MarkAbortedAsync(d.CurrentTaskId, "空中触碰禁飞多边形，AEB强行制动");
                        d.CurrentTaskId = null; d.CurrentOrderId = null;
                    }
                    else
                    {
                        d.Coordinates = $"{nLng:F4}, {nLat:F4}";
                    }
                }

                // 时空引擎精髓：当前帧物理姿态落库
                await droneRepo.SaveDroneStateToDatabaseAsync(d);
                moved = true;
            }
        }
        return moved;
    }

    private async Task HandleLifecycleTransitionAsync(DroneState d, AirspaceService airspace, MissionRepository missionRepo)
    {
        // 1. 若刚刚执行完【空载接货】阶段 ➔ 自动装货，自动掉头切换进【满载送货】阶段
        if (d.Status == DroneStatus.FlyingToPickup && !string.IsNullOrEmpty(d.DeliveryDestinationCoords))
        {
            d.Status = DroneStatus.FlyingToDeliver;
            d.CurrentMission = $"【全自动串联】已在发货枢纽装货，正开辟天路运往最终目的地。";

            string safeRoute = airspace.GetGuaranteedSafeCorridor(d.Coordinates, d.DeliveryDestinationCoords);
            d.WaypointQueue.Clear();
            foreach (var p in safeRoute.Split(';', StringSplitOptions.RemoveEmptyEntries)) d.WaypointQueue.Enqueue(p.Trim());

            if (d.WaypointQueue.TryDequeue(out var firstWp)) d.CurrentTargetWaypoint = firstWp;
            d.DeliveryDestinationCoords = null;
            return;
        }

        // 2. 若刚刚执行完【满载送货】阶段 ➔ 顺利送达！自动结算入库，自动切入【全自主回巢补电】阶段
        if (d.Status == DroneStatus.FlyingToDeliver)
        {
            await missionRepo.MarkCompletedAsync(d.CurrentTaskId);
            d.CurrentTaskId = null;

            d.Status = DroneStatus.ReturningHome;
            d.CurrentMission = $"【送达卸货成功】完成业务交付。电量余 {d.BatteryLevel}%，正自航返回数字停机坪补电。";
            d.CurrentPayload = 0.0;

            string homeRoute = airspace.GetGuaranteedSafeCorridor(d.Coordinates, d.HomeBaseCoordinates);
            d.WaypointQueue.Clear();
            foreach (var p in homeRoute.Split(';', StringSplitOptions.RemoveEmptyEntries)) d.WaypointQueue.Enqueue(p.Trim());

            if (d.WaypointQueue.TryDequeue(out var firstWp)) d.CurrentTargetWaypoint = firstWp;
            return;
        }

        // 3. 若刚刚执行完【全自主回巢】阶段 ➔ 顺利停入原籍充电桩！断开动力，降落，进入 Idle 状态
        if (d.Status == DroneStatus.ReturningHome)
        {
            d.CurrentTargetWaypoint = null;
            d.IsInMotion = false;
            d.Status = DroneStatus.Idle;
            d.CurrentMission = "🔌 充电桩已安全对接，进入高压慢充回血状态...";
            d.CurrentOrderId = null;
            return;
        }
    }
}