namespace AGXIOME.Shared.Models;

public record LLMWIKYRequest(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<AgentMessage>? History = null,
    double? Temperature = 0.7,
    int? MaxTokens = 4096,
    IReadOnlyList<System.Text.Json.Nodes.JsonObject>? Tools = null
);

public record LLMWIKYResponse(
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolArguments = null,
    LLMWIKYUsage Usage = default
);

public record LLMWIKYChunk(
    string Content,
    bool IsToolCall = false,
    string? ToolCallId = null,
    bool IsFinal = false
);

public record LLMWIKYUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);

public record LLMCompletionRequest(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<AgentMessage>? History,
    double? Temperature,
    int? MaxTokens,
    string Model,
    IReadOnlyList<System.Text.Json.Nodes.JsonObject>? Tools
);

public record LLMProviderCapabilities(
    bool SupportsStreaming,
    bool SupportsFunctionCalling,
    bool SupportsVision,
    int EmbeddingDimensions,
    int MaxContextTokens,
    IReadOnlyList<string> SupportedModels
);
