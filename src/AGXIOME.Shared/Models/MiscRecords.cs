using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Models;

public record RunMetrics(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    TimeSpan Duration,
    int ToolCallCount,
    Dictionary<string, ModelMetrics> PerModel
);

public record ModelMetrics(
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    TimeSpan InferenceTime,
    decimal EstimatedCost
);

public record SessionMetrics(
    int TotalRuns,
    int TotalToolCalls,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalEstimatedCost,
    TimeSpan TotalDuration
);

public record KnowledgeChunk(
    string DocumentId,
    string Content,
    float Score,
    Dictionary<string, string>? Metadata = null,
    float[]? Embedding = null
);

public record Document(
    string Id,
    string Content,
    DocumentType Type,
    Dictionary<string, string>? Metadata = null
);

public record Chunk(
    string Text,
    int Index,
    int StartOffset,
    int EndOffset,
    Dictionary<string, string>? Metadata = null
);

public record KnowledgeSearchOptions(
    int TopK = 10,
    double MinScore = 0.65,
    Dictionary<string, string>? MetadataFilter = null,
    SearchType SearchType = SearchType.Vector,
    string? TenantId = null
);

public record NotifyEvent(
    string Type,
    string SourceId,
    string? Payload = null,
    DateTime Timestamp = default
);

public record ProviderDescriptor(
    string Name,
    IReadOnlyList<string> SupportedModels,
    bool RequiresApiKey,
    bool SupportsLocalEndpoint,
    bool IsEmbeddingSupported
);

public record FewShotExample(string Input, string Output, string? Explanation = null);

public record AgentTemplate(
    string Name,
    string Description,
    string Category,
    string? SystemPrompt = null,
    List<string>? SuggestedTools = null,
    List<string>? SuggestedSkills = null,
    LLMWIKYConfig? LLMOverride = null,
    AgentConfig? ConfigOverrides = null
);

public record ReasoningConfig(
    ReasoningMode Mode = ReasoningMode.None,
    string? ReasoningModel = null,
    bool ShowFullReasoning = false
);

public record MCPClientOptions(
    string ServerName,
    MCPTransport Transport,
    string? Command = null,
    string[]? Args = null,
    Uri? Endpoint = null,
    Dictionary<string, string>? Headers = null
);
