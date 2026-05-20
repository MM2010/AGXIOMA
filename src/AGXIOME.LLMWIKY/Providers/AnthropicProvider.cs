using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.LlmWiky.Http;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.LlmWiky.Providers;

/// <summary>Anthropic Claude provider. Full Messages API with tool_use and SSE streaming.</summary>
public sealed class AnthropicProvider : ILLMProvider
{
    public string Name => "Anthropic";
    public LLMProviderCapabilities Capabilities => new(true, true, true, 1024, 200_000,
        ["claude-sonnet-4-20250514", "claude-3-5-sonnet-20241022", "claude-opus-4-20250514", "claude-3-5-haiku-20241022"]);
    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly HttpClient _http;
    private readonly LLMWIKYConfig _config;

    public AnthropicProvider(LLMWIKYConfig config, IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;

        if (httpClientFactory is not null)
        {
            _http = httpClientFactory.CreateClient(AgxiomeHttpClientDefaults.ClientName(LLMProviderType.Anthropic));
        }
        else
        {
            _http = new HttpClient();
            _http.BaseAddress = new Uri("https://api.anthropic.com/v1");
        }

        // Auth headers are always set per-instance
        _http.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key",
            config.ApiKey ?? throw new ArgumentException("Anthropic ApiKey required"));
        _http.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public async Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
    {
        var body = BuildBody(request, false);
        var httpContent = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("messages", httpContent, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return ParseResponse(doc.RootElement);
    }

    public async IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildBody(request, true);
        var httpContent = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("messages", httpContent, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") { yield return new LLMWIKYChunk("", IsFinal: true); yield break; }

            using var doc = JsonDocument.Parse(data);
            var type = doc.RootElement.GetProperty("type").GetString();

            switch (type)
            {
                case "content_block_start":
                    var block = doc.RootElement.GetProperty("content_block");
                    if (block.GetProperty("type").GetString() == "tool_use")
                        yield return new LLMWIKYChunk("", IsToolCall: true, ToolCallId: block.GetProperty("id").GetString());
                    break;
                case "content_block_delta":
                    var delta = doc.RootElement.GetProperty("delta");
                    var dt = delta.GetProperty("type").GetString();
                    if (dt == "text_delta")
                        yield return new LLMWIKYChunk(delta.GetProperty("text").GetString() ?? "");
                    else if (dt == "input_json_delta")
                        yield return new LLMWIKYChunk(delta.GetProperty("partial_json").GetString() ?? "");
                    break;
                case "message_stop":
                    yield return new LLMWIKYChunk("", IsFinal: true);
                    yield break;
            }
        }
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => throw new NotSupportedException("Anthropic does not support embeddings natively");

    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => throw new NotSupportedException("Anthropic does not support embeddings natively");

    private JsonObject BuildBody(LLMCompletionRequest req, bool stream)
    {
        var messages = new JsonArray();
        if (req.History != null)
            foreach (var m in req.History)
                messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = req.UserPrompt });

        var body = new JsonObject
        {
            ["model"] = req.Model,
            ["max_tokens"] = req.MaxTokens ?? 4096,
            ["stream"] = stream,
            ["messages"] = messages
        };

        if (!string.IsNullOrEmpty(req.SystemPrompt))
            body["system"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = req.SystemPrompt } };

        if (req.Tools?.Count > 0)
        {
            body["tools"] = new JsonArray();
            foreach (var t in req.Tools)
            {
                var tool = new JsonObject
                {
                    ["name"] = t["name"]?.GetValue<string>() ?? "",
                    ["description"] = t["description"]?.GetValue<string>() ?? "",
                    ["input_schema"] = t["parameters"]?.DeepClone()
                };
                (body["tools"] as JsonArray)!.Add(tool);
            }
        }
        return body;
    }

    private static LLMWIKYResponse ParseResponse(JsonElement root)
    {
        string? content = null, toolCallId = null, toolName = null, toolArgs = null;
        if (root.TryGetProperty("content", out var arr))
            foreach (var b in arr.EnumerateArray())
            {
                var t = b.GetProperty("type").GetString();
                if (t == "text") content = b.GetProperty("text").GetString();
                else if (t == "tool_use")
                {
                    toolName = b.GetProperty("name").GetString();
                    toolCallId = b.GetProperty("id").GetString();
                    toolArgs = b.GetProperty("input").GetRawText();
                }
            }

        var usage = root.TryGetProperty("usage", out var u)
            ? new LLMWIKYUsage(u.GetProperty("input_tokens").GetInt32(), u.GetProperty("output_tokens").GetInt32(), 0)
            : default;
        return new LLMWIKYResponse(content ?? "", toolCallId, toolName, toolArgs, usage);
    }

    public void Dispose() => _http.Dispose();
}
