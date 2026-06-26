using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class DroneRepository
{
    private readonly string _connectionString;
    public DroneRepository(string connectionString) => _connectionString = connectionString;

    public async Task EnsureSchemaAndSeedAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);

        // 1. 建立无人机物理状态常态化硬盘表
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS sys_drone_assets (
                drone_id VARCHAR(32) PRIMARY KEY,
                status INT,
                current_mission VARCHAR(256),
                coordinates VARCHAR(64),
                battery_level DOUBLE PRECISION,
                core_temperature DOUBLE PRECISION,
                max_payload DOUBLE PRECISION,
                current_payload DOUBLE PRECISION,
                drone_model VARCHAR(64),
                updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            );
        """);

        // 2. 检查表里有没有数据，如果没有，把 20 架大兵团瞬间初始化灌入硬盘（种子数据）
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys_drone_assets");
        if (count == 0)
        {
            Console.WriteLine("💾 [资产库自检] 发现硬盘空虚，正在执行20机大兵团全参数初始化灌库...");
            var seedDrones = new List<DroneState>
            {
                new() { DroneId = "UAV-001", Status = DroneStatus.Idle, Coordinates = "113.9365, 22.5412", BatteryLevel = 98.5, CoreTemperature = 36.2, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-002", Status = DroneStatus.Idle, Coordinates = "113.9425, 22.5380", BatteryLevel = 100.0, CoreTemperature = 35.1, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-003", Status = DroneStatus.Idle, Coordinates = "113.9280, 22.5510", BatteryLevel = 84.2, CoreTemperature = 38.8, MaxPayload = 30.0, DroneModel = "星河·重载双桨V2", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-004", Status = DroneStatus.Idle, Coordinates = "113.9490, 22.5310", BatteryLevel = 88.0, CoreTemperature = 37.0, MaxPayload = 15.0, DroneModel = "大疆·FlyCart30", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-005", Status = DroneStatus.Idle, Coordinates = "113.9210, 22.5360", BatteryLevel = 95.1, CoreTemperature = 36.5, MaxPayload = 15.0, DroneModel = "大疆·FlyCart30", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-006", Status = DroneStatus.Idle, Coordinates = "114.0550, 22.5460", BatteryLevel = 92.0, CoreTemperature = 38.0, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-007", Status = DroneStatus.Idle, Coordinates = "114.0410, 22.5390", BatteryLevel = 91.5, CoreTemperature = 36.2, MaxPayload = 30.0, DroneModel = "星河·重载双桨V2", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-008", Status = DroneStatus.Idle, Coordinates = "114.0680, 22.5330", BatteryLevel = 95.5, CoreTemperature = 36.8, MaxPayload = 15.0, DroneModel = "大疆·FlyCart30", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-009", Status = DroneStatus.Idle, Coordinates = "114.0320, 22.5520", BatteryLevel = 82.0, CoreTemperature = 37.0, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                
                // 💥 纠偏硬核动作：原位置 114.0510, 22.5280 惨遭福田CBD红区中央锁死。
                // 架构师一纸调令：将其出生点向南物理横移 2.5 公里，挪至深圳湾口岸开阔安全空域 (114.0510, 22.5020)！
                new() { DroneId = "UAV-010", Status = DroneStatus.Idle, Coordinates = "114.0510, 22.5020", BatteryLevel = 99.0, CoreTemperature = 35.5, MaxPayload = 50.0, DroneModel = "顺丰·方舟重载轮", CurrentMission="备勤驻停" },

                new() { DroneId = "UAV-011", Status = DroneStatus.Idle, Coordinates = "113.8650, 22.5850", BatteryLevel = 100.0, CoreTemperature = 34.2, MaxPayload = 50.0, DroneModel = "顺丰·方舟重载轮", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-012", Status = DroneStatus.Idle, Coordinates = "113.8820, 22.5710", BatteryLevel = 93.8, CoreTemperature = 35.0, MaxPayload = 50.0, DroneModel = "顺丰·方舟重载轮", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-013", Status = DroneStatus.Idle, Coordinates = "113.8450, 22.6020", BatteryLevel = 87.0, CoreTemperature = 36.1, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-014", Status = DroneStatus.Idle, Coordinates = "113.8990, 22.5620", BatteryLevel = 82.1, CoreTemperature = 39.4, MaxPayload = 15.0, DroneModel = "大疆·FlyCart30", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-015", Status = DroneStatus.Idle, Coordinates = "114.0280, 22.6090", BatteryLevel = 91.0, CoreTemperature = 36.9, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-016", Status = DroneStatus.Idle, Coordinates = "114.0350, 22.6210", BatteryLevel = 88.5, CoreTemperature = 38.2, MaxPayload = 30.0, DroneModel = "星河·重载双桨V2", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-017", Status = DroneStatus.Idle, Coordinates = "114.1120, 22.5480", BatteryLevel = 96.0, CoreTemperature = 35.8, MaxPayload = 15.0, DroneModel = "大疆·FlyCart30", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-018", Status = DroneStatus.Idle, Coordinates = "114.1250, 22.5550", BatteryLevel = 90.0, CoreTemperature = 34.5, MaxPayload = 30.0, DroneModel = "星河·丰翼X100", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-X900", Status = DroneStatus.Idle, Coordinates = "113.9250, 22.5300", BatteryLevel = 98.2, CoreTemperature = 38.5, MaxPayload = 30.0, DroneModel = "星河·医疗救援专机", CurrentMission="备勤驻停" },
                new() { DroneId = "UAV-X901", Status = DroneStatus.Idle, Coordinates = "113.9480, 22.5430", BatteryLevel = 95.0, CoreTemperature = 36.1, MaxPayload = 15.0, DroneModel = "大疆·FlyCart30", CurrentMission="备勤驻停" }
            };

            foreach (var drone in seedDrones)
            {
                await SaveDroneStateToDatabaseAsync(drone);
            }
        }
    }

    // 💥 高频原子级 UPSERT 写入器：同步内存数据至 Postgres 硬盘
    public async Task SaveDroneStateToDatabaseAsync(DroneState drone)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        var sql = """
            INSERT INTO sys_drone_assets (drone_id, status, current_mission, coordinates, battery_level, core_temperature, max_payload, current_payload, drone_model, updated_at)
            VALUES (@DroneId, @Status, @CurrentMission, @Coordinates, @BatteryLevel, @CoreTemperature, @MaxPayload, @CurrentPayload, @DroneModel, NOW())
            ON CONFLICT (drone_id) DO UPDATE SET
                status = EXCLUDED.status,
                current_mission = EXCLUDED.current_mission,
                coordinates = EXCLUDED.coordinates,
                battery_level = EXCLUDED.battery_level,
                core_temperature = EXCLUDED.core_temperature,
                current_payload = EXCLUDED.current_payload,
                updated_at = NOW();
        """;
        await conn.ExecuteAsync(sql, drone);
    }
}