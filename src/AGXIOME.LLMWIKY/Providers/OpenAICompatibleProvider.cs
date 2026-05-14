using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.LLMWIKY.Http;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Resilience;

namespace AGXIOME.LLMWIKY.Providers;

/// <summary>
/// OpenAICompatibleProvider — adapter universale per qualsiasi API OpenAI-compatible.
///
/// SUPERIORE a Swarms litellm: type-safe, zero dipendenze Python, routing intelligente
/// integrato con ProviderMetricsCollector (latency, cost, error rate).
///
/// Provider supportati (e molti altri):
///   - Together AI     → endpoint: https://api.together.xyz/v1
///   - Fireworks AI    → endpoint: https://api.fireworks.ai/inference/v1
///   - Perplexity      → endpoint: https://api.perplexity.ai
///   - DeepInfra       → endpoint: https://api.deepinfra.com/v1/openai
///   - Novita AI       → endpoint: https://api.novita.ai/v3/openai
///   - Hyperbolic      → endpoint: https://api.hyperbolic.xyz/v1
///   - LM Studio local → endpoint: http://localhost:1234/v1
///   - vLLM server     → endpoint: http://localhost:8000/v1
///   - Qualsiasi altro endpoint OpenAI-compat
///
/// Usage:
///   var config = OpenAICompatibleConfig.For("together-ai", apiKey,
///       endpoint: "https://api.together.xyz/v1",
///       model: "mistralai/Mixtral-8x7B-Instruct-v0.1");
///   var provider = new OpenAICompatibleProvider(config);
/// </summary>
public sealed class OpenAICompatibleProvider : ILLMProvider
{
    public string Name { get; }

    public LLMProviderCapabilities Capabilities { get; }

    public int EmbeddingDimensions => _config.EmbeddingDimensions;

    private readonly HttpClient _http;
    private readonly LLMWIKYConfig _config;
    private readonly ResilientExecutor _resilient;
    private readonly CircuitBreaker _breaker;
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public OpenAICompatibleProvider(LLMWIKYConfig config, string? displayName = null, IHttpClientFactory? httpClientFactory = null)
    {
        _config = config;
        Name = displayName ?? DeriveNameFromEndpoint(config.Endpoint) ?? "OpenAI-Compatible";

        Capabilities = new LLMProviderCapabilities(
            SupportsStreaming: true,
            SupportsFunctionCalling: true,
            SupportsVision: false,
            EmbeddingDimensions: config.EmbeddingDimensions,
            MaxContextTokens: 128_000,
            SupportedModels: [config.Model]);

        _breaker = new CircuitBreaker(failureThreshold: 5);
        _resilient = new ResilientExecutor(
            new RetryPolicy { MaxRetries = config.MaxRetries, BaseDelay = TimeSpan.FromMilliseconds(500) },
            _breaker, Name);

        if (httpClientFactory is not null)
        {
            _http = httpClientFactory.CreateClient(AgxiomeHttpClientDefaults.ClientName(config.Provider));
            // For unregistered providers BaseAddress is null — set from config.
            if (_http.BaseAddress is null && !string.IsNullOrWhiteSpace(config.Endpoint))
                _http.BaseAddress = new Uri(config.Endpoint.TrimEnd('/') + "/");
        }
        else
        {
            _http = new HttpClient { Timeout = config.Timeout };
            if (!string.IsNullOrWhiteSpace(config.Endpoint))
                _http.BaseAddress = new Uri(config.Endpoint.TrimEnd('/') + "/");
        }
        if (!string.IsNullOrWhiteSpace(config.ApiKey))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {config.ApiKey}");
    }

    // ─── Completion ──────────────────────────────────────────────────────────

    public async Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest request, CancellationToken ct = default)
    {
        var body = BuildChatBody(request, stream: false);

        var result = await _resilient.ExecuteAsync(Name, async token =>
        {
            var json = body.ToJsonString();
            using var sc = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("chat/completions", sc, token);

            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new HttpRequestException("Rate limited", null, System.Net.HttpStatusCode.TooManyRequests);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(token);
                throw new HttpRequestException($"{Name} API error {(int)resp.StatusCode}: {err}");
            }

            return await resp.Content.ReadAsStreamAsync(token);
        }, ct: ct);

        if (!result.Success || result.Data is null)
            throw new HttpRequestException($"{Name} failed after {result.Attempts} attempts: {result.ErrorMessage}");

        using var doc  = await System.Text.Json.JsonDocument.ParseAsync(result.Data, cancellationToken: ct);
        var choice     = doc.RootElement.GetProperty("choices")[0];
        var message    = choice.GetProperty("message");
        var text       = message.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
        string? toolCallId = null, toolName = null, toolArgs = null;
        if (message.TryGetProperty("tool_calls", out var tc) && tc.GetArrayLength() > 0)
        {
            var call   = tc[0];
            toolCallId = call.GetProperty("id").GetString();
            var fn     = call.GetProperty("function");
            toolName   = fn.GetProperty("name").GetString();
            toolArgs   = fn.GetProperty("arguments").GetString();
        }
        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
        int pt    = usage.ValueKind != System.Text.Json.JsonValueKind.Undefined ? usage.GetProperty("prompt_tokens").GetInt32() : 0;
        int ct2   = usage.ValueKind != System.Text.Json.JsonValueKind.Undefined ? usage.GetProperty("completion_tokens").GetInt32() : 0;

        return new LLMWIKYResponse(text, toolCallId, toolName, toolArgs,
            new LLMWIKYUsage(pt, ct2, pt + ct2));
    }

    // ─── Streaming ───────────────────────────────────────────────────────────

    public async IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(
        LLMCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = BuildChatBody(request, stream: true);
        var json = body.ToJsonString();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpReq = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = content };
        using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"{Name} stream error {(int)resp.StatusCode}: {err}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];
            if (data == "[DONE]") { yield return new LLMWIKYChunk("", IsFinal: true); yield break; }

            var node = JsonNode.Parse(data);
            var delta = node?["choices"]?[0]?["delta"]?["content"]?.GetValue<string>();
            if (delta is not null)
                yield return new LLMWIKYChunk(delta);
        }

        yield return new LLMWIKYChunk("", IsFinal: true);
    }

    // ─── Embeddings ──────────────────────────────────────────────────────────

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var body = new JsonObject { ["model"] = _config.Model, ["input"] = text };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("embeddings", content, ct);
        resp.EnsureSuccessStatusCode();
        var node = await resp.Content.ReadFromJsonAsync<JsonNode>(_jsonOpts, ct);
        var arr  = node?["data"]?[0]?["embedding"]?.AsArray();
        return arr?.Select(v => v!.GetValue<float>()).ToArray() ?? [];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
            results[i] = await EmbedAsync(texts[i], ct);
        return results;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private JsonObject BuildChatBody(LLMCompletionRequest request, bool stream)
    {
        var messages = new JsonArray();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt });

        if (request.History is not null)
            foreach (var msg in request.History)
                messages.Add(new JsonObject { ["role"] = msg.Role, ["content"] = msg.Content });

        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.UserPrompt });

        var body = new JsonObject
        {
            ["model"] = _config.Model,
            ["messages"] = messages,
            ["temperature"] = request.Temperature ?? _config.DefaultTemperature,
            ["max_tokens"] = _config.MaxTokens,
            ["stream"] = stream
        };

        if (request.Tools is not null && request.Tools.Count > 0)
        {
            var toolsArray = new JsonArray();
            foreach (var t in request.Tools)
                toolsArray.Add(t.DeepClone());
            body["tools"] = toolsArray;
            body["tool_choice"] = "auto";
        }

        return body;
    }

    private static IReadOnlyList<string>? ParseToolCalls(JsonNode? node) => null;

    private static string? DeriveNameFromEndpoint(string? endpoint)
    {
        if (endpoint is null) return null;
        return endpoint switch
        {
            var e when e.Contains("together") => "TogetherAI",
            var e when e.Contains("fireworks") => "FireworksAI",
            var e when e.Contains("perplexity") => "Perplexity",
            var e when e.Contains("deepinfra") => "DeepInfra",
            var e when e.Contains("novita") => "NovitaAI",
            var e when e.Contains("hyperbolic") => "Hyperbolic",
            var e when e.Contains("localhost") || e.Contains("127.0.0.1") => "LocalLLM",
            _ => null
        };
    }
}

/// <summary>Factory helpers for common OpenAI-compatible endpoints.</summary>
public static class OpenAICompatibleConfig
{
    public static LLMWIKYConfig ForTogetherAI(string apiKey, string model = "mistralai/Mixtral-8x7B-Instruct-v0.1")
        => new() { Provider = LLMProviderType.TogetherAI, ApiKey = apiKey, Model = model, Endpoint = "https://api.together.xyz/v1" };

    public static LLMWIKYConfig ForPerplexity(string apiKey, string model = "llama-3.1-sonar-huge-128k-online")
        => new() { Provider = LLMProviderType.Perplexity, ApiKey = apiKey, Model = model, Endpoint = "https://api.perplexity.ai" };

    public static LLMWIKYConfig ForDeepInfra(string apiKey, string model = "meta-llama/Meta-Llama-3.1-70B-Instruct")
        => new() { Provider = LLMProviderType.DeepInfra, ApiKey = apiKey, Model = model, Endpoint = "https://api.deepinfra.com/v1/openai" };

    public static LLMWIKYConfig ForLocalVLLM(string model, string endpoint = "http://localhost:8000/v1")
        => new() { Provider = LLMProviderType.Custom, Model = model, Endpoint = endpoint };

    public static LLMWIKYConfig For(string displayName, string? apiKey, string endpoint, string model)
        => new() { Provider = LLMProviderType.Custom, ApiKey = apiKey, Model = model, Endpoint = endpoint };
}
