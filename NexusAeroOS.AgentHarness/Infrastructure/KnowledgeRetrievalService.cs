using Microsoft.SemanticKernel.Embeddings;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class KnowledgeRetrievalService
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly VectorKnowledgeRepository _repository;

    public KnowledgeRetrievalService(
        ITextEmbeddingGenerationService embeddingService,
        VectorKnowledgeRepository repository)
    {
        _embeddingService = embeddingService;
        _repository = repository;
    }

    /// <summary>
    /// 根据业务意图，实时从物理向量库中捞取支撑事实上下文
    /// </summary>
    public async Task<string> GetRagContextAsync(string queryText)
    {
        // 1. 将人类的查询口语实时降维/转化为 1024 维度的数学向量
        var queryVector = await _embeddingService.GenerateEmbeddingAsync(queryText);

        // 2. 向 Postgres 发起 HNSW 空间近邻检索，捞取最相关的 2 条规章
        var matchedChunks = await _repository.SearchClosestAsync(queryVector, topK: 2);

        // 3. 将检索到的物理切片进行结构化包装
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("【从企业本地向量硬盘检索到的实时法律/安全规章依据】");

        int index = 1;
        foreach (var chunk in matchedChunks)
        {
            contextBuilder.AppendLine($"[依据 {index++}]: {chunk}");
        }

        return contextBuilder.ToString();
    }
}