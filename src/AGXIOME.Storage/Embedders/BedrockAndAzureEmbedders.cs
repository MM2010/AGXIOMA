using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-09 — AWS Bedrock Titan Embeddings (titan-embed-text-v2, 1024 dims)
// POST /model/{modelId}/invoke via AWS Bedrock runtime endpoint.
// Auth: standard AWS SigV4 — caller must inject pre-configured HttpClient with
// custom SigV4 message handler or use SDK-generated presigned URL.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record BedrockEmbedderOptions
{
    /// <summary>AWS region endpoint, e.g. "https://bedrock-runtime.us-east-1.amazonaws.com/"</summary>
    public required string EndpointUrl { get; init; }
    public string ModelId              { get; init; } = "amazon.titan-embed-text-v2:0";
}

public sealed class BedrockTitanEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly BedrockEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.AWSBedrockTitan;
    public int Dimensions            => 1024;

    /// <param name="http">Must carry AWS SigV4 auth (e.g. via AWSSignatureMessageHandler).
    /// Never pass a plain HttpClient with no auth for authenticated Bedrock calls.</param>
    public BedrockTitanEmbedder(HttpClient http, BedrockEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.EndpointUrl);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];

        var url  = $"model/{_opts.ModelId}/invoke";
        var resp = await _http.PostAsJsonAsync(url, new { inputText = text, dimensions = Dimensions, normalize = true }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<BedrockEmbedResponse>(ct);
        return raw?.Embedding ?? new float[Dimensions];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        // Bedrock Titan does not support batch natively — fan out sequentially to avoid rate limits.
        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
            results[i] = await EmbedAsync(texts[i], ct);
        return results;
    }

    public void Dispose() => _http.Dispose();

    private sealed class BedrockEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// E-10 — Azure OpenAI Embeddings (text-embedding-3-large, 3072 dims)
// POST {azureEndpoint}/openai/deployments/{deployment}/embeddings?api-version=...
// ─────────────────────────────────────────────────────────────────────────────

public sealed record AzureOpenAIEmbedderOptions
{
    public required string ApiKey     { get; init; }
    public required string Endpoint   { get; init; }   // e.g. "https://{resource}.openai.azure.com/"
    public required string Deployment { get; init; }   // e.g. "text-embedding-3-large"
    public string ApiVersion          { get; init; } = "2024-02-01";
}

public sealed class AzureOpenAIEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly AzureOpenAIEmbedderOptions _opts;
    private readonly string _relativeUrl;

    public EmbedderType EmbedderType => EmbedderType.AzureOpenAI;
    public int Dimensions            => 3072;

    public AzureOpenAIEmbedder(HttpClient http, AzureOpenAIEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.Endpoint);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("api-key", opts.ApiKey);
        _relativeUrl = $"openai/deployments/{opts.Deployment}/embeddings?api-version={opts.ApiVersion}";
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];
        var batch = await EmbedBatchAsync([text], ct);
        return batch[0];
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        var resp = await _http.PostAsJsonAsync(_relativeUrl,
            new { input = texts, encoding_format = "float" }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<AzureEmbedResponse>(ct);
        if (raw?.Data is null) return new float[texts.Count][];

        return raw.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
    }

    public void Dispose() => _http.Dispose();

    private sealed class AzureEmbedResponse
    {
        [JsonPropertyName("data")]
        public List<AzureEmbedData>? Data { get; set; }
    }

    private sealed class AzureEmbedData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
