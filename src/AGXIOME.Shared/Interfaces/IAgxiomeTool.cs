using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Base tool contract — all tools have identity and metadata.</summary>
public interface IAgxiomeTool
{
    string Name { get; }
    string Description { get; }
    string? Version { get; }
    IReadOnlyList<string> Categories { get; }
}

/// <summary>
/// Executable tool contract. Tools implement this to be callable by LLMs.
/// Each tool exposes a JSON Schema describing its parameters for function calling.
/// </summary>
public interface IAgxiomeExecutableTool : IAgxiomeTool
{
    Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct);
    System.Text.Json.Nodes.JsonObject ToJsonSchema();
}
