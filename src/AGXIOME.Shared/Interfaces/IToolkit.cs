using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Result of a toolkit tool invocation.</summary>
public record ToolkitResult(
    bool Success,
    object? Data,
    string? ErrorMessage = null,
    TimeSpan? Duration = null,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Descriptor for a single callable tool inside a toolkit.</summary>
public record ToolkitToolDescriptor(
    string Name,
    string Description,
    System.Text.Json.Nodes.JsonObject ParameterSchema
);

/// <summary>Toolkit contract. One toolkit contains 1-N named tools callable by the agent.
/// Implementations must NOT store mutable per-call state (stateless invocations).</summary>
public interface IToolkit
{
    string Name { get; }
    ToolkitCategory Category { get; }
    string Version { get; }
    IReadOnlyList<ToolkitToolDescriptor> Tools { get; }

    Task<ToolkitResult> ExecuteAsync(string toolName, Dictionary<string, object?> parameters,
        CancellationToken ct = default);
}
