using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

/// <summary>Runtime context passed to agent and tool execution within a session.</summary>
public record RunContext(
    string UserId,
    string SessionId,
    ISessionState SessionState,
    Dictionary<string, object>? Dependencies = null,
    Dictionary<string, object>? Metadata = null,
    Dictionary<string, string>? KnowledgeFilters = null
);
