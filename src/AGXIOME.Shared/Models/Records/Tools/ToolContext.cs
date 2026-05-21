using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

/// <summary>Context passed to a tool during agent-driven execution.</summary>
public record ToolContext(
    string AgentId,
    string RunId,
    string? TenantId = null,
    Dictionary<string, object>? Parameters = null,
    TimeSpan? Timeout = null,
    ISessionState? SessionState = null,
    IDependencyProvider? Dependencies = null
);
