using System.Text.Json;
using AGXIOME.DynamicRuntime.Generation;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

/// <summary>
/// Tool orchestratore che genera un nuovo agente dinamico tramite LLM.
/// Richiede: userId, description. Opzionale: example_input, example_output.
/// </summary>
public sealed class GenerateAgentTool(AgentCodeGenerator generator) : AgxiomeFunction
{
    public override string Name => "generate_agent";
    public override string Description => "Generates a new dynamic C# agent from a natural language description using LLM code synthesis.";
    public override IReadOnlyList<string> Categories => ["dynamic", "codegen", "agent"];

    public override async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var parameters = context.Parameters;
        if (parameters is null)
            return new ToolResult(false, null, "Parameters required: userId, description");

        var userId = GetParam(parameters, "userId");
        var description = GetParam(parameters, "description");

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(description))
            return new ToolResult(false, null, "userId and description are required");

        var request = new AgentGenerationRequest(
            userId,
            description,
            ExampleInput: GetParam(parameters, "example_input"),
            ExampleOutput: GetParam(parameters, "example_output")
        );

        var result = await generator.GenerateAsync(request, ct);

        if (!result.Success)
            return new ToolResult(false, null, string.Join("; ", result.Errors));

        var output = new
        {
            result.AgentId,
            ElapsedMs = (long)result.TotalTime.TotalMilliseconds,
            result.SavedFilePath
        };
        return new ToolResult(true, JsonSerializer.Serialize(output));
    }

    private static string? GetParam(Dictionary<string, object> parameters, string key) =>
        parameters.TryGetValue(key, out var v) ? v?.ToString() : null;
}
