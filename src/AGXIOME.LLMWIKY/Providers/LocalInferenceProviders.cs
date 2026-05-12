using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AGXIOME.LLMWIKY.Providers;

// ── M-21 — llama.cpp ─────────────────────────────────────────────────────────
/// <summary>llama.cpp server provider. Runs GGUF quantised models locally.
/// OpenAI-compatible API on localhost:8080/v1 by default.
/// Supports embeddings when a GGUF embedding model is loaded.</summary>
public sealed class LlamaCppProvider : ILLMProvider
{
    public string Name => "LlamaCpp";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 1024,  // Actual dimension depends on loaded model
        MaxContextTokens: 131_072,
        SupportedModels: ["local-gguf-model"]);
    // Returns documented default; actual dimension is model-dependent at runtime
    public int EmbeddingDimensions => Capabilities.EmbeddingDimensions;

    private readonly OpenAIProvider _inner;
    private readonly LLMWIKYConfig _config;

    public LlamaCppProvider(LLMWIKYConfig config)
    {
        _config = config;
        _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:8080/v1" });
    }

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-22 — LM Studio ─────────────────────────────────────────────────────────
/// <summary>LM Studio desktop inference provider.
/// OpenAI-compatible server on localhost:1234/v1 by default.
/// Supports any model loaded in the LM Studio GUI.</summary>
public sealed class LMStudioProvider : ILLMProvider
{
    public string Name => "LMStudio";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 1024,
        MaxContextTokens: 131_072,
        SupportedModels: ["local-lm-studio-model"]);
    // Returns documented default; actual dimension is model-dependent at runtime
    public int EmbeddingDimensions => Capabilities.EmbeddingDimensions;

    private readonly OpenAIProvider _inner;
    private readonly LLMWIKYConfig _config;

    public LMStudioProvider(LLMWIKYConfig config)
    {
        _config = config;
        _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:1234/v1" });
    }

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-23 — Ollama Responses ──────────────────────────────────────────────────
/// <summary>Ollama Responses API provider. Uses the <c>POST /api/responses</c> endpoint
/// (distinct from /api/chat used by OllamaProvider). Suitable for single-turn structured
/// completions with tool calling support added in Ollama 0.5+.</summary>
public sealed class OllamaResponsesProvider : ILLMProvider
{
    public string Name => "OllamaResponses";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["llama3.1", "phi4", "deepseek-r1", "qwen2.5"]);
    public int EmbeddingDimensions => 0;

    private readonly HttpClient _http;
    private readonly LLMWIKYConfig _config;
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public OllamaResponsesProvider(LLMWIKYConfig config)
    {
        _config = config;
        _http = new HttpClient { BaseAddress = new Uri(config.Endpoint ?? "http://localhost:11434") };
    }

    public async Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default)
    {
        var body = BuildBody(r);
        var content = new System.Net.Http.StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/responses", content, ct);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var output = doc.RootElement.GetProperty("output").EnumerateArray()
            .FirstOrDefault(x => x.TryGetProperty("type", out var t) && t.GetString() == "message");
        var text = output.TryGetProperty("content", out var c)
            ? c.EnumerateArray().FirstOrDefault().TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : ""
            : "";
        return new LLMWIKYResponse(text);
    }

    public async IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(
        LLMCompletionRequest r,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Ollama Responses API streams via SSE — each event carries a delta or done marker
        var body = BuildBody(r);
        body["stream"] = true;
        var content = new System.Net.Http.StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/responses", content, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct) ?? "";
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") { yield return new LLMWIKYChunk("", IsFinal: true); yield break; }
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("delta", out var delta) && delta.TryGetProperty("text", out var txt))
                yield return new LLMWIKYChunk(txt.GetString() ?? "");
        }
        yield return new LLMWIKYChunk("", IsFinal: true);
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => throw new NotSupportedException("Use OllamaProvider.EmbedAsync for embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => throw new NotSupportedException("Use OllamaProvider.EmbedAsync for embeddings.");

    private JsonObject BuildBody(LLMCompletionRequest r)
    {
        var input = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = r.UserPrompt } };
        if (r.History != null)
        {
            var withHistory = new JsonArray();
            foreach (var m in r.History) withHistory.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
            withHistory.Add(input[0]!.DeepClone());
            input = withHistory;
        }
        return new JsonObject { ["model"] = r.Model, ["input"] = input };
    }
}

// ── M-24 — LocalAI ───────────────────────────────────────────────────────────
/// <summary>LocalAI self-hosted provider. Drop-in OpenAI API replacement supporting
/// GGUF, diffusion, whisper, and embedding models locally.
/// Default endpoint: localhost:8080/v1</summary>
public sealed class LocalAIProvider : ILLMProvider
{
    public string Name => "LocalAI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: true,
        SupportsVision: false,
        EmbeddingDimensions: 1024,
        MaxContextTokens: 131_072,
        SupportedModels: ["local-ai-model"]);
    // Returns documented default; actual dimension is model-dependent at runtime
    public int EmbeddingDimensions => Capabilities.EmbeddingDimensions;

    private readonly OpenAIProvider _inner;
    private readonly LLMWIKYConfig _config;

    public LocalAIProvider(LLMWIKYConfig config)
    {
        _config = config;
        _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:8080/v1" });
    }

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => _inner.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => _inner.EmbedBatchAsync(texts, ct);
}

// ── M-25 — GPT4All ───────────────────────────────────────────────────────────
/// <summary>GPT4All desktop inference provider. OpenAI-compatible server on localhost:4891.
/// Runs cross-platform on CPU or GPU without cloud dependency.</summary>
public sealed class GPT4AllProvider : ILLMProvider
{
    public string Name => "GPT4All";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 8_192,
        SupportedModels: ["mistral-7b-openorca.Q4_0.gguf", "gpt4all-falcon-newbpe-q4_0.gguf"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public GPT4AllProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:4891/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("GPT4All server does not expose embeddings endpoint.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("GPT4All server does not expose embeddings endpoint.");
}

// ── M-26 — text-gen-webui (Oobabooga) ────────────────────────────────────────
/// <summary>Oobabooga text-generation-webui provider. OpenAI-compatible extension server
/// on localhost:5000 by default. Supports GPTQ, GGUF, AWQ models.</summary>
public sealed class TextGenWebUIProvider : ILLMProvider
{
    public string Name => "TextGenWebUI";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 32_768,
        SupportedModels: ["local-text-gen-model"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public TextGenWebUIProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:5000/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("TextGenWebUI does not expose embeddings by default.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("TextGenWebUI does not expose embeddings by default.");
}

// ── M-27 — KoboldCPP ─────────────────────────────────────────────────────────
/// <summary>KoboldCPP provider. GGUF inference with OpenAI-compatible API extension
/// at localhost:5001 by default. Commonly used for roleplay and creative writing workloads.</summary>
public sealed class KoboldCPPProvider : ILLMProvider
{
    public string Name => "KoboldCPP";
    public LLMProviderCapabilities Capabilities => new(
        SupportsStreaming: true,
        SupportsFunctionCalling: false,
        SupportsVision: false,
        EmbeddingDimensions: 0,
        MaxContextTokens: 131_072,
        SupportedModels: ["local-kobold-model"]);
    public int EmbeddingDimensions => 0;

    private readonly OpenAIProvider _inner;
    public KoboldCPPProvider(LLMWIKYConfig config)
        => _inner = new OpenAIProvider(config with { Endpoint = config.Endpoint ?? "http://localhost:5001/v1" });

    public Task<LLMWIKYResponse> CompleteAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteAsync(r, ct);
    public IAsyncEnumerable<LLMWIKYChunk> CompleteStreamAsync(LLMCompletionRequest r, CancellationToken ct = default) => _inner.CompleteStreamAsync(r, ct);
    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotSupportedException("KoboldCPP does not support embeddings.");
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotSupportedException("KoboldCPP does not support embeddings.");
}
