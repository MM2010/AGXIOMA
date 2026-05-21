using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

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
