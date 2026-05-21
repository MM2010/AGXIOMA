namespace AGXIOME.Shared.Models;

/// <summary>Aggregated response from a multi-agent team execution.</summary>
public record TeamResponse(
    string Content,
    TeamMode ModeUsed,
    IReadOnlyList<AgentResult> AgentResults,
    TeamMetadata? Metadata = null
);

/// <summary>Per-agent result within a team execution.</summary>
public record AgentResult(
    string AgentId,
    string Role,
    AgentStatus Status,
    string? Output,
    TimeSpan ExecutionTime
);

/// <summary>Shared context passed to all agents in a team run.</summary>
public record TeamContext(
    string? SessionId = null,
    Dictionary<string, object>? SharedState = null,
    object? GlobalInput = null
);

/// <summary>Execution metadata for a completed team run.</summary>
public record TeamMetadata(
    int MembersUsed,
    TimeSpan TotalDuration,
    int TotalToolCalls
);
