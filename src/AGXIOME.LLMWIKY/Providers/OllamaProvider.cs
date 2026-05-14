using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AGXIOME.LLMWIKY.Providers;

/// <summary>Ollama local model provider.</summary>
public sealed class OllamaProvider : ILLMProvider
{
    public string Name => "Ollama";
    public LLMProviderCapabilities Capabilities => new(true, true, true, 768, 8_192,
        new[] { "llama3.1", "llama3.2", "deepseek-coder", "mistral", "mixtral", "phi4", "qwen2.5" });
    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly HttpClient _http;
    private readonly LLMWIKYConfig _config;

    public OllamaProvider(LLMWIKYConfig config)
    {
        _config = config;
        _http = new HttpClient();
        _http.BaseAddress = new Uri(config.Endpoint ?? "http://localhost:11434");
    }

    public async Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt }
        };
        if (request.History != null)
            foreach (var m in request.History)
            {
                if (m.Role == "assistant" && m.ToolName != null)
                    messages.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = "",
                        ["tool_calls"] = new JsonArray(new JsonObject
                        {
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = m.ToolName,
                                ["arguments"] = JsonNode.Parse(m.ToolArguments ?? "{}")
                            }
                        })
                    });
                else if (m.Role == "tool")
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["content"] = m.Content
                    });
                else
                    messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
            }
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt });

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = false,
            ["options"] = new JsonObject
            {
                ["temperature"] = request.Temperature ?? 0.7,
                ["num_predict"] = request.MaxTokens ?? 4096
            }
        };

        // Tool schemas — Ollama /api/chat supports tools array (OpenAI-compatible format)
        if (request.Tools?.Count > 0)
            body["tools"] = new JsonArray(request.Tools.Select(t => (JsonNode)new JsonObject
            {
                ["type"] = "function",
                ["function"] = t.DeepClone()
            }).ToArray());

        var jsonStr = body.ToJsonString();
        var content = new StringContent(jsonStr, System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var msg = doc.RootElement.GetProperty("message");

        // Check for tool_calls in Ollama response
        if (msg.TryGetProperty("tool_calls", out var tcs) && tcs.GetArrayLength() > 0)
        {
            var call = tcs[0];
            var func = call.GetProperty("function");
            var toolName = func.GetProperty("name").GetString();
            var toolArgs = func.GetProperty("arguments").GetRawText();
            var toolCallId = call.TryGetProperty("id", out var id) ? id.GetString() : $"call_{Guid.NewGuid():N[..16]}";
            return new LLMWIKYResponse("", toolCallId, toolName, toolArgs);
        }

        return new LLMWIKYResponse(msg.GetProperty("content").GetString() ?? "");
    }

    public async IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new JsonArray();
        messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });
        if (request.History != null)
            foreach (var m in request.History)
                messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt });

        var body = new JsonObject
        {
            ["model"] = request.Model, ["messages"] = messages, ["stream"] = true
        };

        var content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat", content, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("message", out var m) && m.TryGetProperty("content", out var c))
                yield return new LLMWIKYChunk(c.GetString() ?? "");
            if (doc.RootElement.TryGetProperty("done", out var d) && d.GetBoolean())
                yield return new LLMWIKYChunk("", IsFinal: true);
        }
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var body = new JsonObject { ["model"] = "nomic-embed-text", ["prompt"] = text };
        var content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/embeddings", content, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.GetProperty("embedding").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new List<Task<float[]>>();
        foreach (var t in texts) results.Add(EmbedAsync(t, ct));
        return await Task.WhenAll(results);
    }
}
