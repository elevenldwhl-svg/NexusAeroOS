using Dapper;
using Npgsql;
using NexusAeroOS.Domain.Entities;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class AuditLogRepository
{
    private readonly string _connectionString;

    // 注入数据库连接字符串
    public AuditLogRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    // 💥 自动建表：在系统启动时确保审计表存在
    public async Task EnsureSchemaAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var sql = """
            CREATE TABLE IF NOT EXISTS flight_audit_logs (
                id UUID PRIMARY KEY,
                drone_id VARCHAR(100) NOT NULL,
                action_type VARCHAR(100) NOT NULL,
                user_input TEXT,
                ai_reasoning TEXT,
                tool_executed VARCHAR(255),
                is_critical BOOLEAN NOT NULL,
                timestamp TIMESTAMP NOT NULL
            );
        """;
        await connection.ExecuteAsync(sql);
    }

    // 💥 Dapper 极速插入：性能逼近原生 SQL，远超 EF Core
    public async Task SaveLogAsync(FlightAuditLog log)
    {
        using var connection = new NpgsqlConnection(_connectionString);

        var sql = """
            INSERT INTO flight_audit_logs 
            (id, drone_id, action_type, user_input, ai_reasoning, tool_executed, is_critical, timestamp) 
            VALUES 
            (@Id, @DroneId, @ActionType, @UserInput, @AiReasoning, @ToolExecuted, @IsCritical, @Timestamp);
        """;

        // Dapper 会自动将 log 对象的属性（如 Id, DroneId）与 SQL 语句中的 @参数 进行精准匹配
        await connection.ExecuteAsync(sql, log);
    }
}