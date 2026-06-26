using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class ThoughtLogEntity
{
    public string LogId { get; set; } = Guid.NewGuid().ToString();
    public string DroneId { get; set; } = "System";
    public string AgentRole { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public class ThoughtLogRepository
{
    private readonly string _connectionString;
    public ThoughtLogRepository(string connectionString) => _connectionString = connectionString;

    public async Task EnsureSchemaAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS sys_brain_thought_ledger (
                log_id VARCHAR(64) PRIMARY KEY,
                drone_id VARCHAR(32),
                agent_role VARCHAR(64),
                function_name VARCHAR(128),
                arguments TEXT,
                created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS idx_thought_time ON sys_brain_thought_ledger(created_at DESC);
        """);
    }

    public async Task RecordThoughtAsync(string droneId, string role, string funcName, string args)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync("""
            INSERT INTO sys_brain_thought_ledger (log_id, drone_id, agent_role, function_name, arguments)
            VALUES (@LogId, @DroneId, @AgentRole, @FunctionName, @Arguments)
        """, new ThoughtLogEntity { DroneId = droneId, AgentRole = role, FunctionName = funcName, Arguments = args });
    }
}