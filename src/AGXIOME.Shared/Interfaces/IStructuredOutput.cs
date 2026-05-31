using System.Text.Json;
using System.Text.Json.Nodes;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Parses structured output from raw LLM response.</summary>
public interface IOutputParser
{
    Task<T> ParseAsync<T>(string agentOutput, CancellationToken ct = default);
}

/// <summary>Formats structured output into natural language response.</summary>
public interface IResponseModel
{
    Task<string> FormatResponseAsync<T>(T structuredOutput, CancellationToken ct = default);
}

/// <summary>Agent input/output schema definition.</summary>
public record AgentOutputSchema
{
    public Type OutputType { get; init; } = null!;
    public JsonObject JsonSchema { get; init; } = null!;
}

public record AgentInputSchema
{
    public Type InputType { get; init; } = null!;
    public JsonObject JsonSchema { get; init; } = null!;
    public object? Validate(Dictionary<string, object> input) => null;
}
