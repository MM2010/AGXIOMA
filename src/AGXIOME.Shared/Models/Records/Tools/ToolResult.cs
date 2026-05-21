namespace AGXIOME.Shared.Models;

/// <summary>Result of a tool invocation with success flag, data payload, and optional timing.</summary>
public record ToolResult(
    bool Success,
    object? Data,
    string? ErrorMessage = null,
    TimeSpan? ExecutionTime = null,
    ToolMetadata? Metadata = null
);

/// <summary>Metadata describing the tool that produced a result.</summary>
public record ToolMetadata(
    string ToolName,
    string? Version,
    long BytesProcessed = 0,
    int ItemsProcessed = 0
);
