using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-17 — HuggingFace Inference API Embeddings
// POST /pipeline/feature-extraction/{model}  (HF Inference API)
// Returns float[][] shaped (batch_size, seq_len, hidden_dim) — we mean-pool.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record HuggingFaceEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://api-inference.huggingface.co/";
    public string Model            { get; init; } = "sentence-transformers/all-MiniLM-L6-v2";
    public int Dimensions          { get; init; } = 384;
}

public sealed class HuggingFaceEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly HuggingFaceEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.HuggingFaceApi;
    public int Dimensions            => _opts.Dimensions;

    public HuggingFaceEmbedder(HttpClient http, HuggingFaceEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.BaseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return new float[Dimensions];

        var resp = await _http.PostAsJsonAsync(
            $"pipeline/feature-extraction/{_opts.Model}",
            new { inputs = text, options = new { wait_for_model = true } }, ct);
        resp.EnsureSuccessStatusCode();

        // HF returns either float[] (already pooled) or float[][] (token embeddings).
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return ParseHFVector(doc.RootElement, Dimensions);
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        var resp = await _http.PostAsJsonAsync(
            $"pipeline/feature-extraction/{_opts.Model}",
            new { inputs = texts, options = new { wait_for_model = true } }, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var results = new float[texts.Count][];
            int i = 0;
            foreach (var el in root.EnumerateArray())
                results[i++] = ParseHFVector(el, Dimensions);
            return results;
        }

        return [ParseHFVector(root, Dimensions)];
    }

    /// <summary>Parses HF output: 1-D float array or 2-D token matrix (mean-pooled).</summary>
    private static float[] ParseHFVector(System.Text.Json.JsonElement el, int dims)
    {
        if (el.ValueKind != System.Text.Json.JsonValueKind.Array) return new float[dims];

        // Detect depth: if first element is also an array → token-level output → mean-pool.
        var first = el.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            // 2-D: (seq_len, hidden_dim) → mean pool
            var tokenVecs = el.EnumerateArray()
                .Select(row => row.EnumerateArray().Select(x => x.GetSingle()).ToArray())
                .ToArray();
            if (tokenVecs.Length == 0) return new float[dims];
            var pooled = new float[tokenVecs[0].Length];
            foreach (var tv in tokenVecs)
                for (int d = 0; d < tv.Length; d++) pooled[d] += tv[d];
            float inv = 1f / tokenVecs.Length;
            for (int d = 0; d < pooled.Length; d++) pooled[d] *= inv;
            return pooled;
        }

        return el.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    }

    public void Dispose() => _http.Dispose();
}

// ─────────────────────────────────────────────────────────────────────────────
// E-18 — NVIDIA NIM Embeddings (NV-Embed-QA, 1024 dims)
// OpenAI-compatible endpoint on NVIDIA's inference platform.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record NvidiaNimEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://integrate.api.nvidia.com/v1/";
    public string Model            { get; init; } = "nvidia/nv-embed-v1";
    public string InputType        { get; init; } = "passage";
    public string TruncateMode     { get; init; } = "END";
}

public sealed class NvidiaNimEmbedder : IEmbedder, IDisposable
{
    private readonly HttpClient _http;
    private readonly NvidiaNimEmbedderOptions _opts;

    public EmbedderType EmbedderType => EmbedderType.NvidiaNIM;
    public int Dimensions            => 1024;

    public NvidiaNimEmbedder(HttpClient http, NvidiaNimEmbedderOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _http.BaseAddress ??= new Uri(opts.BaseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
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

        var resp = await _http.PostAsJsonAsync("embeddings", new
        {
            input      = texts,
            model      = _opts.Model,
            input_type = _opts.InputType,
            truncate   = _opts.TruncateMode,
            encoding_format = "float",
        }, ct);
        resp.EnsureSuccessStatusCode();

        var raw = await resp.Content.ReadFromJsonAsync<NvEmResponse>(ct);
        if (raw?.Data is null) return new float[texts.Count][];
        return raw.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToArray();
    }

    public void Dispose() => _http.Dispose();

    private sealed class NvEmResponse
    {
        [JsonPropertyName("data")]
        public List<NvEmData>? Data { get; set; }
    }

    private sealed class NvEmData
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
