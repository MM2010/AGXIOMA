/// <summary>Tool-related records: ToolContext (inputs, agent, session), ToolResult (success/failure with data), ToolCallLimit, ToolResultCache.</summary>
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

public record ToolContext(
    string AgentId,
    string RunId,
    string? TenantId = null,
    Dictionary<string, object>? Parameters = null,
    TimeSpan? Timeout = null,
    ISessionState? SessionState = null,
    IDependencyProvider? Dependencies = null
);

public record ToolResult(
    bool Success,
    object? Data,
    string? ErrorMessage = null,
    TimeSpan? ExecutionTime = null,
    ToolMetadata? Metadata = null
);

public record ToolMetadata(
    string ToolName,
    string? Version,
    long BytesProcessed = 0,
    int ItemsProcessed = 0
);

public record ToolRegistration(
    string TypeName,
    Dictionary<string, object>? Config = null
);

public record ToolCallResult(
    string ToolName,
    string ToolCallId,
    bool Success,
    object? Result,
    TimeSpan? Duration = null
);
