using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Resilience;
using System.Diagnostics;

namespace AGXIOME.LLMWIKY.Providers;

/// <summary>OpenAI provider. Also used for DeepSeek, Custom, and any OpenAI-compatible endpoint.
/// Integrated resilience: retry (429/500/502/503), exponential backoff, circuit breaker, fallback.</summary>
public sealed class OpenAIProvider : ILLMProvider
{
    public string Name => _config.Endpoint?.Contains("deepseek") == true ? "DeepSeek" : "OpenAI";

    public LLMProviderCapabilities Capabilities => new(true, true, true, 1536, 128_000,
        new[] { "gpt-4o", "gpt-4o-mini", "gpt-4", "o1", "o1-mini", "o3", "gpt-4.1", "deepseek-chat" });

    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly HttpClient _http;
    private readonly LLMWIKYConfig _config;
    private readonly ResilientExecutor _resilient;
    private readonly CircuitBreaker _breaker;
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public OpenAIProvider(LLMWIKYConfig config)
    {
        _config = config;
        _breaker = new CircuitBreaker(failureThreshold: 5);
        _resilient = new ResilientExecutor(
            new RetryPolicy { MaxRetries = config.MaxRetries, BaseDelay = TimeSpan.FromMilliseconds(500) },
            _breaker, "OpenAI");
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
        _http.BaseAddress = new Uri(config.Endpoint ?? "https://api.openai.com/v1");
    }

    public async Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
    {
        var body = BuildChatBody(request);
        var result = await _resilient.ExecuteAsync("chat.completions", async token =>
        {
            var resp = await _http.PostAsJsonAsync("chat/completions", body, _json, token);
            // Handle 429 explicitly for rate-limit message
            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStreamAsync(token);
        }, ct: ct);

        if (!result.Success || result.Data == null)
            throw new HttpRequestException($"LLM request failed after {result.Attempts} attempts: {result.ErrorMessage}");

        using var doc = await JsonDocument.ParseAsync(result.Data, cancellationToken: ct);
        var choice = doc.RootElement.GetProperty("choices")[0];
        var message = choice.GetProperty("message");
        var usage = doc.RootElement.GetProperty("usage");

        string? content = message.TryGetProperty("content", out var c) ? c.GetString() : null;
        string? toolCallId = null, toolName = null, toolArgs = null;

        if (message.TryGetProperty("tool_calls", out var calls) && calls.GetArrayLength() > 0)
        {
            var call = calls[0];
            toolCallId = call.GetProperty("id").GetString();
            toolName = call.GetProperty("function").GetProperty("name").GetString();
            toolArgs = call.GetProperty("function").GetProperty("arguments").GetString();
        }

        return new LLMWIKYResponse(content ?? "", toolCallId, toolName, toolArgs,
            new LLMWIKYUsage(usage.GetProperty("prompt_tokens").GetInt32(),
                             usage.GetProperty("completion_tokens").GetInt32(),
                             usage.GetProperty("total_tokens").GetInt32()));
    }

    public async IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildChatBody(request);
        body["stream"] = true;
        var content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("chat/completions", content, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct) ?? "";
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") { yield return new LLMWIKYChunk("", IsFinal: true); yield break; }

            using var doc = JsonDocument.Parse(data);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) continue;
            var delta = choices[0].GetProperty("delta");

            string? contentChunk = delta.TryGetProperty("content", out var cc) ? cc.GetString() : null;

            string? tId = null, tName = null;
            if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.GetArrayLength() > 0)
            {
                var tc = tcs[0];
                tId = tc.TryGetProperty("id", out var id) ? id.GetString() : null;
                tName = tc.GetProperty("function").GetProperty("name").GetString();
            }

            if (contentChunk != null || tName != null)
                yield return new LLMWIKYChunk(contentChunk ?? "", tName != null, tId, false);
        }
        yield return new LLMWIKYChunk("", IsFinal: true);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var body = new { input = text, model = "text-embedding-3-small" };
        var response = await _http.PostAsJsonAsync("embeddings", body, _json, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
        return data.EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var body = new { input = texts, model = "text-embedding-3-small" };
        var response = await _http.PostAsJsonAsync("embeddings", body, _json, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return doc.RootElement.GetProperty("data").EnumerateArray()
            .OrderBy(e => e.GetProperty("index").GetInt32())
            .Select(e => e.GetProperty("embedding").EnumerateArray().Select(v => (float)v.GetDouble()).ToArray())
            .ToArray();
    }

    private JsonObject BuildChatBody(LLMCompletionRequest req)
    {
        var messages = new JsonArray();

        // System prompt
        messages.Add(new JsonObject { ["role"] = "system", ["content"] = req.SystemPrompt });

        // History
        if (req.History != null)
            foreach (var m in req.History)
                messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });

        // User message
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = req.UserPrompt });

        var body = new JsonObject
        {
            ["model"] = req.Model,
            ["messages"] = messages,
            ["temperature"] = req.Temperature ?? 0.7,
            ["max_tokens"] = req.MaxTokens ?? 4096
        };

        // Tools
        if (req.Tools?.Count > 0)
        {
            body["tools"] = new JsonArray(req.Tools.Select(t =>
                new JsonObject { ["type"] = "function", ["function"] = t }).ToArray());
        }

        return body;
    }

    public void Dispose() => _http.Dispose();
}
