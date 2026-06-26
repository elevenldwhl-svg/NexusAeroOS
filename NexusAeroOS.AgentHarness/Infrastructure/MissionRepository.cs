using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class MissionLogEntity
{
    public string TaskId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string DroneId { get; set; } = string.Empty;
    public string PayloadType { get; set; } = string.Empty;
    public double WeightKg { get; set; }
    public string StartCoords { get; set; } = string.Empty;
    public string DestCoords { get; set; } = string.Empty;
    public string Waypoints { get; set; } = string.Empty;
    public string AiReasoning { get; set; } = string.Empty;
    public string Status { get; set; } = "EXECUTING"; // EXECUTING, COMPLETED, ABORTED
}

public class MissionRepository
{
    private readonly string _connectionString;
    public MissionRepository(string connectionString) => _connectionString = connectionString;

    public async Task EnsureSchemaAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS sys_mission_ledger (
                task_id VARCHAR(64) PRIMARY KEY,
                order_id VARCHAR(64),
                drone_id VARCHAR(32),
                payload_type VARCHAR(64),
                weight_kg DOUBLE PRECISION,
                start_coords VARCHAR(64),
                dest_coords VARCHAR(64),
                waypoints TEXT,
                ai_reasoning TEXT,   -- 💥 用于存储大模型生成的漂亮 Markdown 最终总结报告
                status VARCHAR(32),
                created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                completed_at TIMESTAMP WITH TIME ZONE
            );
        """);
    }

    public async Task UpdateFinalDecisionReportAsync(string orderId, string markdownReport)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        // 根据订单号反向把大模型写出的帅气最终 Markdown 协定直接焊进硬盘！
        await conn.ExecuteAsync("UPDATE sys_mission_ledger SET ai_reasoning = @report WHERE order_id = @orderId", new { report = markdownReport, orderId = orderId });
    }

    // 1. 点火起飞入库
    public async Task RecordStartAsync(MissionLogEntity log)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("""
            INSERT INTO sys_mission_ledger (task_id, order_id, drone_id, payload_type, weight_kg, start_coords, dest_coords, waypoints, ai_reasoning, status)
            VALUES (@TaskId, @OrderId, @DroneId, @PayloadType, @WeightKg, @StartCoords, @DestCoords, @Waypoints, @AiReasoning, @Status)
        """, log);
    }

    // 2. 终点吃豆完结入库
    public async Task MarkCompletedAsync(string? taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("UPDATE sys_mission_ledger SET status = 'COMPLETED', completed_at = NOW() WHERE task_id = @id", new { id = taskId });
    }

    // 3. 半空触发地理围栏主动刹停(ABORT)入库
    public async Task MarkAbortedAsync(string? taskId, string abortReason)
    {
        if (string.IsNullOrEmpty(taskId)) return;
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("UPDATE sys_mission_ledger SET status = @st, ai_reasoning = ai_reasoning || @rs, completed_at = NOW() WHERE task_id = @id",
            new { st = "ABORTED", rs = $"\n【硬件紧急熔断】{abortReason}", id = taskId });
    }

    // 新增：读取所有未完成的任务（用于系统重启后的现场复原）
    public async Task<IEnumerable<MissionLogEntity>> GetActiveMissionsAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryAsync<MissionLogEntity>(
            "SELECT * FROM sys_mission_ledger WHERE status = 'EXECUTING'");
    }
}