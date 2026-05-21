namespace AGXIOME.Shared.Models;

/// <summary>Result of an LLM-requested tool call, including its execution outcome.</summary>
public record ToolCallResult(
    string ToolName,
    string ToolCallId,
    bool Success,
    object? Result,
    TimeSpan? Duration = null
);
