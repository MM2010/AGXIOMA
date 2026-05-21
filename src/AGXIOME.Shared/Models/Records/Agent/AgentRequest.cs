using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

/// <summary>Agent execution request envelope.</summary>
public record AgentRequest(
    string Prompt,
    AgentContext? Context = null,
    IReadOnlyList<AgentMessage>? History = null,
    AgentRequestOptions? Options = null
);
