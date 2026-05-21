namespace AGXIOME.Shared.Models;

/// <summary>A retrieved knowledge chunk with relevance score and optional embedding.</summary>
public record KnowledgeChunk(
    string DocumentId,
    string Content,
    float Score,
    Dictionary<string, string>? Metadata = null,
    float[]? Embedding = null
);

/// <summary>Options that control knowledge base search behaviour.</summary>
public record KnowledgeSearchOptions(
    int TopK = 10,
    double MinScore = 0.65,
    Dictionary<string, string>? MetadataFilter = null,
    SearchType SearchType = SearchType.Vector,
    string? TenantId = null
);
