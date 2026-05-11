using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

/// <summary>Agent execution request envelope.</summary>
public record AgentRequest(
    string Prompt,
    AgentContext? Context = null,
    IReadOnlyList<AgentMessage>? History = null,
    AgentRequestOptions? Options = null
);

/// <summary>Agent execution response.</summary>
public record AgentResponse(
    string Content,
    AgentStatus Status,
    IReadOnlyList<ToolCallResult>? ToolResults = null,
    AgentMetadata? Metadata = null
);

/// <summary>Streaming chunk for real-time agent output.</summary>
public record AgentChunk(
    string Content,
    string? ToolCallId = null,
    bool IsFinal = false,
    bool IsPaused = false,
    IReadOnlyList<HITLRequirement>? ActiveRequirements = null
);

/// <summary>Single message in agent conversation history.</summary>
public record AgentMessage(
    string Role,
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolArguments = null
);

/// <summary>Options for agent request execution.</summary>
public record AgentRequestOptions(
    bool? EnableStreaming = null,
    bool? EnableRetry = null,
    int? MaxIterations = null,
    TimeSpan? Timeout = null
);

/// <summary>Metadata about agent execution.</summary>
public record AgentMetadata(
    int InferenceCount,
    int ToolCallCount,
    TimeSpan Duration,
    int? TotalTokens = null
);

/// <summary>Context injected into agent run (session, dependencies, etc).</summary>
public record AgentContext(
    string? SessionId = null,
    string? UserId = null,
    Dictionary<string, object>? AdditionalData = null,
    IDependencyProvider? Dependencies = null
);

/// <summary>Interaction persisted in agent memory store.</summary>
public record AgentInteraction(
    string AgentId,
    string Role,
    string Content,
    string? ToolCallsJson = null,
    DateTime? CreatedAt = null
);
