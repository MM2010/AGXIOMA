using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

public interface IAgxiomeTool
{
    string Name { get; }
    string Description { get; }
    string? Version { get; }
    IReadOnlyList<string> Categories { get; }
}

public interface IAgxiomeExecutableTool : IAgxiomeTool
{
    Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct);
    System.Text.Json.Nodes.JsonObject ToJsonSchema();
}
