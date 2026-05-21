namespace AGXIOME.Shared.Models;

/// <summary>Options for agent request execution.</summary>
public record AgentRequestOptions(
    bool? EnableStreaming = null,
    bool? EnableRetry = null,
    int? MaxIterations = null,
    TimeSpan? Timeout = null
);

/// <summary>Criteria that cause an agent to automatically stop before reaching MaxIterations.</summary>
public record AutoStopCriteria(
    int? MaxTokens = null,
    double? MinConfidence = null,
    string[]? StopPhrases = null,
    bool StopOnNoToolCall = true
);

/// <summary>Metadata about agent execution.</summary>
public record AgentMetadata(
    int InferenceCount,
    int ToolCallCount,
    TimeSpan Duration,
    int? TotalTokens = null
);

/// <summary>Multimodal media content attached to an agent message.</summary>
public record MediaContent(
    MediaType MediaType,
    string Data,
    string MimeType,
    string? AltText = null,
    MediaSource Source = MediaSource.Base64
);
