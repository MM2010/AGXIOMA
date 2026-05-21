namespace AGXIOME.Shared.Models;

/// <summary>A user-scoped memory entry persisted across sessions.</summary>
public record MemoryEntry(
    string Id,
    string UserId,
    string Content,
    IReadOnlyList<string> Topics,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
);

/// <summary>User profile data including preferences and custom metadata fields.</summary>
public record UserProfile(
    string UserId,
    string? Name,
    string? PreferredName,
    Dictionary<string, object> CustomFields
);

/// <summary>A typed entity memory entry (fact, event, or relationship) in the knowledge graph.</summary>
public record EntityMemory(
    string Id,
    string EntityId,
    EntityMemoryType Type,
    string Content,
    string? RelatedEntityId = null,
    string? RelationshipType = null,
    DateTime CreatedAt = default
);

/// <summary>Session-level learning context holding a summary and active goals.</summary>
public record SessionContext(
    string SessionId,
    LearningSessionMode Mode,
    string? Summary,
    List<Goal>? Goals = null,
    DateTime UpdatedAt = default
);

/// <summary>A tracked goal within a session context.</summary>
public record Goal(
    string Description,
    GoalStatus Status,
    List<string>? Steps = null,
    int? Progress = null
);

/// <summary>Lifecycle status of a session goal.</summary>
public enum GoalStatus { NotStarted, InProgress, Completed, Blocked }

/// <summary>A learned knowledge entry extracted by the agent and optionally embedded.</summary>
public record LearnedKnowledge(
    string Id,
    string Content,
    float[]? Embedding,
    IReadOnlyList<string> Topics,
    DateTime CreatedAt = default
);

/// <summary>A logged agent decision with its reasoning and outcome for audit and self-improvement.</summary>
public record DecisionLogEntry(
    string Id,
    string AgentId,
    string Decision,
    string Reasoning,
    Dictionary<string, object>? Context = null,
    bool Successful = true,
    DateTime CreatedAt = default
);
