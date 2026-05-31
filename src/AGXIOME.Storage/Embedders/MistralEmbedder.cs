using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Embedders;

// ─────────────────────────────────────────────────────────────────────────────
// E-05 — Mistral Embeddings (mistral-embed, 1024 dims)
// OpenAI-compatible /v1/embeddings endpoint.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Data transfer record for the AGXIOME framework.</summary>
public sealed record MistralEmbedderOptions
{
    public required string ApiKey { get; init; }
    public string BaseUrl          { get; init; } = "https://api.mistral.ai/";
    public string Model            { get; init; } = "mistral-embed";
}

public sealed class MistralEmbedder : IEmbedder, IDisposable
{
    private readonly OpenAICompatEmbedderClient _client;

    public EmbedderType EmbedderType => EmbedderType.Mistral;
    public int Dimensions            => 1024;

    public MistralEmbedder(HttpClient http, MistralEmbedderOptions opts)
    {
        ArgumentNullException.ThrowIfNull(opts);
        http.BaseAddress ??= new Uri(opts.BaseUrl);
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
        _client = new OpenAICompatEmbedderClient(http, opts.Model);
    }

    public Task<float[]>   EmbedAsync(string text, CancellationToken ct = default)
        => _client.EmbedAsync(text, ct);
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        => _client.EmbedBatchAsync(texts, ct);
    public void Dispose() => _client.Dispose();
}
