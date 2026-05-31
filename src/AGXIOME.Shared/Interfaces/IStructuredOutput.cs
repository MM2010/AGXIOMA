using System.Text.Json;
using System.Text.Json.Nodes;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Parses structured output (JSON) from raw LLM responses into typed objects.
/// Used when an agent is configured with <c>SetOutputSchema&lt;T&gt;()</c> to
/// guarantee structured, machine-readable responses instead of free-text.
/// </summary>
/// <remarks>
/// <para><b>Pipeline</b>: LLM → raw JSON string → <see cref="ParseAsync{T}"/> → typed object.</para>
/// <para><b>Error handling</b>: Implementations should throw <see cref="System.Text.Json.JsonException"/>
/// or a custom <c>AgxiomeException</c> when the output cannot be parsed, enabling
/// the agent to retry with a correction prompt.</para>
/// </remarks>
/// <example>
/// <code>
/// var parser = new DefaultOutputParser();
/// var result = await parser.ParseAsync&lt;StockQuote&gt;("""{"symbol":"AAPL","price":198.50}""");
/// Console.WriteLine(result.Symbol); // "AAPL"
/// </code>
/// </example>
public interface IOutputParser
{
    /// <summary>
    /// Deserializes the raw agent output into the specified type.
    /// </summary>
    /// <typeparam name="T">The target type. Must be JSON-deserializable.</typeparam>
    /// <param name="agentOutput">The raw output string from the agent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized object.</returns>
    /// <exception cref="ArgumentException">If <paramref name="agentOutput"/> is null or empty.</exception>
    /// <exception cref="System.Text.Json.JsonException">If the JSON is malformed or does not match the schema.</exception>
    Task<T> ParseAsync<T>(string agentOutput, CancellationToken ct = default);
}

/// <summary>
/// Formats typed structured output into a human-readable natural language response.
/// The inverse of <see cref="IOutputParser"/>: takes structured data and produces
/// a conversational reply suitable for end-user display.
/// </summary>
/// <remarks>
/// <para><b>Pipeline</b>: Typed object → <see cref="FormatResponseAsync{T}"/> → natural language string.</para>
/// </remarks>
/// <example>
/// <code>
/// var model = new DefaultResponseModel();
/// var quote = new StockQuote("AAPL", 198.50m, 2.3m, "USD");
/// var text = await model.FormatResponseAsync(quote);
/// // "AAPL is trading at $198.50, up 2.3% today."
/// </code>
/// </example>
public interface IResponseModel
{
    /// <summary>
    /// Formats the structured output into a natural language response string.
    /// </summary>
    /// <typeparam name="T">The type of the structured output.</typeparam>
    /// <param name="structuredOutput">The structured data to format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A human-readable response string.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="structuredOutput"/> is null.</exception>
    Task<string> FormatResponseAsync<T>(T structuredOutput, CancellationToken ct = default);
}

/// <summary>
/// Defines the expected output schema for an agent. When set, the LLM is instructed
/// to produce JSON matching this schema instead of free-text.
/// </summary>
/// <remarks>
/// The <see cref="JsonSchema"/> is serialized and injected into the system prompt.
/// The <see cref="OutputType"/> is used for deserialization after the LLM responds.
/// </remarks>
public record AgentOutputSchema
{
    /// <summary>The CLR type to deserialize the agent output into.</summary>
    public Type OutputType { get; init; } = null!;

    /// <summary>The JSON Schema definition (JSON Schema draft 2020-12 format).</summary>
    public JsonObject JsonSchema { get; init; } = null!;
}

/// <summary>
/// Defines the expected input schema for an agent. Incoming user prompts are validated
/// against this schema before processing.
/// </summary>
public record AgentInputSchema
{
    /// <summary>The CLR type representing the input structure.</summary>
    public Type InputType { get; init; } = null!;

    /// <summary>The JSON Schema definition for input validation.</summary>
    public JsonObject JsonSchema { get; init; } = null!;

    /// <summary>
    /// Validates a dictionary of input values against the schema.
    /// Returns null if valid, or throws/returns an error description if invalid.
    /// </summary>
    /// <param name="input">The input values to validate.</param>
    /// <returns>Null if valid; an error object otherwise.</returns>
    public object? Validate(Dictionary<string, object> input) => null;
}
