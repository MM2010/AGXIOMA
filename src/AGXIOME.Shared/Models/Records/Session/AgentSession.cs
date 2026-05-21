namespace AGXIOME.Shared.Models;

/// <summary>A persisted user session with state, type, and lifecycle metadata.</summary>
public record AgentSession(
    string Id,
    string UserId,
    string ComponentId,
    SessionType Type,
    string? Name,
    Dictionary<string, object>? State,
    Dictionary<string, string>? Metadata,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
);

/// <summary>Options for creating or configuring a session.</summary>
public record SessionOptions(
    string? Name = null,
    Dictionary<string, string>? Metadata = null,
    bool EnableHistory = true,
    int MaxHistoryMessages = 100,
    bool EnableSummary = false
);
