namespace AGXIOME.Shared.Models;

/// <summary>Streaming chunk for real-time agent output.</summary>
public record AgentChunk(
    string Content,
    string? ToolCallId = null,
    bool IsFinal = false,
    bool IsPaused = false,
    IReadOnlyList<HITLRequirement>? ActiveRequirements = null
);
