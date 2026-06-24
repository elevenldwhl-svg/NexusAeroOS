using System.Data;
using Dapper;
using Npgsql;
using Pgvector.Npgsql;

namespace NexusAeroOS.AgentHarness.Infrastructure;

// ====================================================================
// 补充：Dapper 官方标准的 Vector 类型解析代理
// ====================================================================
public class DapperVectorTypeHandler : SqlMapper.TypeHandler<Pgvector.Vector>
{
    public override void SetValue(IDbDataParameter parameter, Pgvector.Vector value)
    {
        parameter.Value = value;
    }

    public override Pgvector.Vector Parse(object value)
    {
        return (Pgvector.Vector)value;
    }
}

public class VectorKnowledgeRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public VectorKnowledgeRepository(string connectionString)
    {
        // 💥 关键修复：显式告知 Dapper，遇到 Pgvector.Vector 时直接向下透传给 ADO.NET
        SqlMapper.AddTypeHandler(new DapperVectorTypeHandler());

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        _dataSource = dataSourceBuilder.Build();
    }

    public async Task EnsureSchemaAsync()
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        string ddl = """
        -- 1. 激活向量扩展
        CREATE EXTENSION IF NOT EXISTS vector;

        -- 💥 修复核心：强行物理删除旧维度的表，防止 1536 维度的脏结构残留
        DROP TABLE IF EXISTS uav_sop_knowledge_base CASCADE;

        -- 2. 重新创建 1024 维度的标准表（完美适配 BAAI/bge-m3 模型）
        CREATE TABLE uav_sop_knowledge_base (
            id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
            chunk_text TEXT NOT NULL,
            embedding vector(1024)
        );

        -- 3. 重建 HNSW 索引
        CREATE INDEX idx_uav_sop_hnsw 
        ON uav_sop_knowledge_base USING hnsw (embedding vector_cosine_ops);
    """;

        await conn.ExecuteAsync(ddl);
    }

    public async Task InsertChunkAsync(string chunkText, ReadOnlyMemory<float> embedding)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        string sql = """
            INSERT INTO uav_sop_knowledge_base (chunk_text, embedding) 
            VALUES (@Text, @Embedding)
        """;

        await conn.ExecuteAsync(sql, new { Text = chunkText, Embedding = new Pgvector.Vector(embedding) });
    }

    /// <summary>
    /// 依据输入的向量，通过 HNSW 索引执行 Top-K 余弦相似度召回
    /// </summary>
    public async Task<IEnumerable<string>> SearchClosestAsync(ReadOnlyMemory<float> queryEmbedding, int topK = 2)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        // 运算符 <=> 代表计算余弦距离（Cosine Distance）
        // 距离越小（越接近 0），说明语义相似度越高
        string sql = """
            SELECT chunk_text 
            FROM uav_sop_knowledge_base 
            ORDER BY embedding <=> @Vector 
            LIMIT @TopK
        """;

        return await conn.QueryAsync<string>(sql, new
        {
            Vector = new Pgvector.Vector(queryEmbedding),
            TopK = topK
        });
    }
}