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
    string? ToolArguments = null,
    IReadOnlyList<MediaContent>? Media = null
);

/// <summary>Options for agent request execution.</summary>
public record AutoStopCriteria(
    int? MaxTokens = null,
    double? MinConfidence = null,
    string[]? StopPhrases = null,
    bool StopOnNoToolCall = true
);

public record MediaContent(
    MediaType MediaType,
    string Data,
    string MimeType,
    string? AltText = null,
    MediaSource Source = MediaSource.Base64
);

public record HeavySwarmResult(
    string Question,
    List<string> ResearchFindings,
    string Analysis,
    List<string> Alternatives,
    string Synthesis,
    Dictionary<string, TimeSpan> PhaseTiming
);

/// <summary>Result of a 4-agent Grok-mode swarm (Captain + Harper/Benjamin/Lucas).</summary>
public record GrokSwarmResult(
    string Task,
    string HarperOutput,
    string BenjaminOutput,
    string LucasOutput,
    string CaptainSynthesis,
    Dictionary<string, TimeSpan> PhaseTiming
);

/// <summary>Result of a 16-agent Grok-Heavy-mode swarm (Captain + 15 domain specialists).</summary>
public record GrokHeavySwarmResult(
    string Task,
    IReadOnlyDictionary<string, string> SpecialistOutputs,
    string CaptainSynthesis,
    Dictionary<string, TimeSpan> PhaseTiming
);

/// <summary>Result of a ForestSwarm routing and execution.</summary>
public record ForestSwarmResult(
    string Task,
    string SelectedTree,
    string SelectedAgent,
    string Output,
    IReadOnlyList<string> MatchedKeywords,
    TimeSpan Duration
);

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
