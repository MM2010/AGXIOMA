namespace AGXIOME.Shared.Models;

/// <summary>Agent execution response.</summary>
public record AgentResponse(
    string Content,
    AgentStatus Status,
    IReadOnlyList<ToolCallResult>? ToolResults = null,
    AgentMetadata? Metadata = null
);
