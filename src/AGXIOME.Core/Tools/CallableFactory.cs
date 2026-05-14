using System.Text.Json.Nodes;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Target LLM callable format.
/// Each provider uses a slightly different schema for tool/function definitions.
/// </summary>
public enum LlmCallableFormat
{
    /// <summary>OpenAI Chat Completions — <c>tools[].function</c> schema.</summary>
    OpenAiFunction = 0,
    /// <summary>Anthropic Claude — <c>tools[]</c> schema with <c>input_schema</c>.</summary>
    AnthropicTool = 1,
    /// <summary>Google Gemini — <c>function_declarations[]</c> schema.</summary>
    GeminiTool = 2
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>
/// Provider-agnostic callable definition produced by <see cref="ICallableFactory"/>.
/// Contains the serialised JSON object and the format it was generated for.
/// </summary>
public record CallableDefinition(
    string Name,
    string Description,
    /// <summary>JSON parameters schema (JSON Schema "object" node).</summary>
    JsonObject ParametersSchema,
    LlmCallableFormat Format,
    /// <summary>Raw provider payload (the outer wrapper expected by the LLM API).</summary>
    JsonObject Payload
);

/// <summary>
/// Parsed result of a tool-call message emitted by an LLM.
/// <c>Arguments</c> is the raw JSON string from the model; callers must deserialise it.
/// </summary>
public record ParsedToolCall(
    string ToolName,
    string ToolCallId,
    /// <summary>Raw JSON arguments string as returned by the LLM.</summary>
    string ArgumentsJson
);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface ICallableFactory
{
    /// <summary>
    /// Build a <see cref="CallableDefinition"/> from an <see cref="IAgxiomeExecutableTool"/>
    /// in the requested <paramref name="format"/>.
    /// </summary>
    CallableDefinition CreateCallable(IAgxiomeExecutableTool tool, LlmCallableFormat format);

    /// <summary>
    /// Build callable definitions for every tool in the provided list.
    /// Tools that cannot be serialised are skipped (logged at warning level, never thrown).
    /// </summary>
    IReadOnlyList<CallableDefinition> CreateCallableList(
        IEnumerable<IAgxiomeExecutableTool> tools,
        LlmCallableFormat format);

    /// <summary>
    /// Build a JSON array containing all callable payloads — ready to be embedded in an LLM API request.
    /// </summary>
    JsonArray BuildToolsArray(IEnumerable<IAgxiomeExecutableTool> tools, LlmCallableFormat format);

    /// <summary>
    /// Parse an LLM tool-call response into a <see cref="ParsedToolCall"/>.
    /// Returns null if the <paramref name="rawCallNode"/> does not match the expected structure.
    /// </summary>
    ParsedToolCall? ParseToolCall(JsonNode rawCallNode, LlmCallableFormat format);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Converts <see cref="IAgxiomeExecutableTool"/> instances into the JSON callable schemas
/// expected by major LLM providers.
/// <para>
/// Supported formats:
/// <list type="bullet">
///   <item><b>OpenAI</b>: <c>{ "type": "function", "function": { "name", "description", "parameters" } }</c></item>
///   <item><b>Anthropic</b>: <c>{ "name", "description", "input_schema" }</c></item>
///   <item><b>Gemini</b>: <c>{ "name", "description", "parameters" }</c> (inside <c>function_declarations</c>)</item>
/// </list>
/// </para>
/// </summary>
public sealed class CallableFactory : ICallableFactory
{
    public CallableDefinition CreateCallable(IAgxiomeExecutableTool tool, LlmCallableFormat format)
    {
        ArgumentNullException.ThrowIfNull(tool);
        var schema = tool.ToJsonSchema();
        // Extract parameters sub-object — ToJsonSchema() returns { name, description, parameters }
        var parameters = (schema["parameters"] as JsonObject) ?? new JsonObject();

        var payload = format switch
        {
            LlmCallableFormat.OpenAiFunction => BuildOpenAiPayload(tool.Name, tool.Description, parameters),
            LlmCallableFormat.AnthropicTool  => BuildAnthropicPayload(tool.Name, tool.Description, parameters),
            LlmCallableFormat.GeminiTool     => BuildGeminiPayload(tool.Name, tool.Description, parameters),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

        return new CallableDefinition(tool.Name, tool.Description, parameters, format, payload);
    }

    public IReadOnlyList<CallableDefinition> CreateCallableList(
        IEnumerable<IAgxiomeExecutableTool> tools,
        LlmCallableFormat format)
    {
        ArgumentNullException.ThrowIfNull(tools);
        var result = new List<CallableDefinition>();
        foreach (var tool in tools)
        {
            try { result.Add(CreateCallable(tool, format)); }
            catch { /* skip un-serialisable tools */ }
        }
        return result;
    }

    public JsonArray BuildToolsArray(IEnumerable<IAgxiomeExecutableTool> tools, LlmCallableFormat format)
    {
        var arr = new JsonArray();
        foreach (var def in CreateCallableList(tools, format))
            arr.Add(def.Payload.DeepClone());
        return arr;
    }

    public ParsedToolCall? ParseToolCall(JsonNode rawCallNode, LlmCallableFormat format)
    {
        if (rawCallNode is not JsonObject obj) return null;

        return format switch
        {
            LlmCallableFormat.OpenAiFunction => ParseOpenAi(obj),
            LlmCallableFormat.AnthropicTool  => ParseAnthropic(obj),
            LlmCallableFormat.GeminiTool     => ParseGemini(obj),
            _ => null
        };
    }

    // ── Provider payload builders ─────────────────────────────────────────────

    /// <summary>
    /// OpenAI format:
    /// <code>{ "type": "function", "function": { "name": ..., "description": ..., "parameters": ... } }</code>
    /// </summary>
    private static JsonObject BuildOpenAiPayload(string name, string description, JsonObject parameters)
        => new()
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"]        = name,
                ["description"] = description,
                ["parameters"]  = parameters.DeepClone()
            }
        };

    /// <summary>
    /// Anthropic format:
    /// <code>{ "name": ..., "description": ..., "input_schema": { ... } }</code>
    /// </summary>
    private static JsonObject BuildAnthropicPayload(string name, string description, JsonObject parameters)
        => new()
        {
            ["name"]         = name,
            ["description"]  = description,
            ["input_schema"] = parameters.DeepClone()
        };

    /// <summary>
    /// Gemini format (function declaration):
    /// <code>{ "name": ..., "description": ..., "parameters": { ... } }</code>
    /// </summary>
    private static JsonObject BuildGeminiPayload(string name, string description, JsonObject parameters)
        => new()
        {
            ["name"]        = name,
            ["description"] = description,
            ["parameters"]  = parameters.DeepClone()
        };

    // ── Provider response parsers ─────────────────────────────────────────────

    private static ParsedToolCall? ParseOpenAi(JsonObject obj)
    {
        // OpenAI tool_call: { "id": ..., "type": "function", "function": { "name": ..., "arguments": ... } }
        var func = obj["function"] as JsonObject;
        if (func is null) return null;
        var name      = func["name"]?.GetValue<string>();
        var args      = func["arguments"]?.GetValue<string>() ?? "{}";
        var id        = obj["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
        return name is null ? null : new ParsedToolCall(name, id, args);
    }

    private static ParsedToolCall? ParseAnthropic(JsonObject obj)
    {
        // Anthropic tool_use: { "type": "tool_use", "id": ..., "name": ..., "input": { ... } }
        var name  = obj["name"]?.GetValue<string>();
        var id    = obj["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString();
        var input = obj["input"]?.ToJsonString() ?? "{}";
        return name is null ? null : new ParsedToolCall(name, id, input);
    }

    private static ParsedToolCall? ParseGemini(JsonObject obj)
    {
        // Gemini functionCall: { "name": ..., "args": { ... } }
        var name = obj["name"]?.GetValue<string>();
        var args = obj["args"]?.ToJsonString() ?? "{}";
        var id   = Guid.NewGuid().ToString();
        return name is null ? null : new ParsedToolCall(name, id, args);
    }
}
