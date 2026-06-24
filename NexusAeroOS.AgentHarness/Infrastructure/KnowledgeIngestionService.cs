using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Text;

namespace NexusAeroOS.AgentHarness.Infrastructure;

public class KnowledgeIngestionService
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly VectorKnowledgeRepository _repository;

    public KnowledgeIngestionService(
        ITextEmbeddingGenerationService embeddingService,
        VectorKnowledgeRepository repository)
    {
        _embeddingService = embeddingService;
        _repository = repository;
    }

    /// <summary>
    /// 将企业白话文规章文档执行智能切片，并行转化为向量入库
    /// </summary>
    public async Task IngestDocumentAsync(string rawMarkdownText)
    {
        // 💥 关键修复：定义适合中文环境的 Token 计数器
        // 此处将字符长度映射为 Token 权重。若生产环境需极致精准，可在此处调用 SharpToken 库
        TextChunker.TokenCounter chineseTokenCounter = (text) => text.Length;

        // 1. 传入自定义计数器进行行切分（限制单行最大 40 个字）
        var lines = TextChunker.SplitMarkDownLines(
            rawMarkdownText,
            maxTokensPerLine: 40,
            tokenCounter: chineseTokenCounter);

        // 2. 传入自定义计数器进行段落切分
        // 由于改用字符数计重，同步将分块阈值调小（例如：每个 Chunk 最多 120 个字，重叠 20 个字）
        var paragraphs = TextChunker.SplitMarkdownParagraphs(
            lines,
            maxTokensPerParagraph: 120,
            overlapTokens: 20,
            tokenCounter: chineseTokenCounter);

        Console.WriteLine($"\n[知识切片器] 规章文本分析完毕。已自动切分为 {paragraphs.Count} 个独立语义分块(Chunks)。");

        // 3. 批量生成向量并插入数据库
        for (int i = 0; i < paragraphs.Count; i++)
        {
            string chunkText = paragraphs[i];

            // 调用远端 Embedding 模型
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunkText);

            // 写入 PostgreSQL
            await _repository.InsertChunkAsync(chunkText, embedding);

            Console.WriteLine($"   ➔ [Chunk {i + 1}/{paragraphs.Count}] 1024维向量计算完毕，已沉淀至本地 HNSW 硬盘。");
        }
    }
}