namespace AGXIOME.Shared.Models;

/// <summary>Single message in agent conversation history.</summary>
public record AgentMessage(
    string Role,
    string Content,
    string? ToolCallId = null,
    string? ToolName = null,
    string? ToolArguments = null,
    IReadOnlyList<MediaContent>? Media = null
);
