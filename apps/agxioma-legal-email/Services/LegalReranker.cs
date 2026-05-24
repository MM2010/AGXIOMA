using System.Text.Json.Serialization;
using AgxiomaLLMWiki;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Cross-encoder reranker using Qwen3-Reranker via /api/chat with pre-filled think block.
/// Strategy: pass empty &lt;think&gt;&lt;/think&gt; as assistant prefix → model outputs only "yes"/"no" (num_predict=1, ~270ms/doc).
/// Falls back to embedding order on error.
/// </summary>
public sealed class LegalReranker
{
    private readonly HttpClient _http;
    private readonly ILogger<LegalReranker> _log;
    private readonly string _model;
    // Max concurrency to avoid overwhelming Ollama
    private static readonly SemaphoreSlim _sem = new(3, 3);

    public LegalReranker(IHttpClientFactory httpFactory, ILogger<LegalReranker> log,
        string model = "dengcao/Qwen3-Reranker-0.6B:F16")
    {
        _http = httpFactory.CreateClient("ollama");
        _log = log;
        _model = model;
    }

    /// <summary>Re-rank WikiPageRef list via Qwen3-Reranker yes/no scoring.</summary>
    public async Task<List<RerankedSource>> RerankAsync(
        string query,
        List<WikiPageRef> sources,
        CancellationToken ct = default)
    {
        if (sources.Count <= 1)
            return sources.Select(s => new RerankedSource(s, s.Score, null)).ToList();

        try
        {
            var documents = sources.Select(s =>
                s.Snippet?.Length > 0
                    ? (s.Snippet.Length > 512 ? s.Snippet[..512] : s.Snippet)
                    : s.Slug).ToList();

            var scores = await ScoreDocumentsAsync(query, documents, ct);

            var reranked = sources.Select((src, i) =>
            {
                var rerank = i < scores.Count ? scores[i] : 0.5f;
                var combined = src.Score * 0.3f + rerank * 0.7f;
                return new RerankedSource(src, combined, rerank);
            })
            .OrderByDescending(x => x.CombinedScore)
            .ToList();

            _log.LogInformation("[Reranker] Top: {Slug} combined={S:F3} (embed={E:F3} rerank={R:F3})",
                reranked[0].Slug, reranked[0].CombinedScore, reranked[0].EmbedScore, reranked[0].RerankScore ?? 0);
            return reranked;
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Reranker] Fallback to embedding order: {Error}", ex.Message);
            return sources.Select(s => new RerankedSource(s, s.Score, null)).ToList();
        }
    }

    /// <summary>Re-rank WikiPageChunk list via Qwen3-Reranker yes/no scoring.</summary>
    public async Task<List<(WikiPageChunk Source, float CombinedScore, float? RerankScore)>> RerankChunksAsync(
        string query,
        List<WikiPageChunk> sources,
        CancellationToken ct = default)
    {
        if (sources.Count <= 1)
            return sources.Select(s => (s, s.Score, (float?)null)).ToList();

        try
        {
            var documents = sources.Select(s =>
                s.Content.Length > 512 ? s.Content[..512] : s.Content).ToList();

            var scores = await ScoreDocumentsAsync(query, documents, ct);

            var reranked = sources.Select((chunk, i) =>
            {
                var rerank = i < scores.Count ? scores[i] : 0.5f;
                var combined = chunk.Score * 0.3f + rerank * 0.7f;
                return (Source: chunk, CombinedScore: combined, RerankScore: (float?)rerank);
            })
            .OrderByDescending(x => x.CombinedScore)
            .ToList();

            _log.LogInformation("[Reranker] Top chunk: {Slug} combined={S:F3} (embed={E:F3} rerank={R:F3})",
                reranked[0].Source.Slug, reranked[0].CombinedScore, reranked[0].Source.Score, reranked[0].RerankScore ?? 0);
            return reranked;
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Reranker] Chunk fallback to embedding order: {Error}", ex.Message);
            return sources.Select(s => (s, s.Score, (float?)null)).ToList();
        }
    }

    /// <summary>
    /// Scores each document against the query using Qwen3-Reranker's yes/no classification.
    /// Uses pre-filled empty &lt;think&gt; block to skip chain-of-thought and get a single token.
    /// Documents are scored concurrently (max 3 parallel).
    /// </summary>
    private async Task<List<float>> ScoreDocumentsAsync(string query, List<string> documents, CancellationToken ct)
    {
        var tasks = documents.Select(doc => ScoreOneAsync(query, doc, ct)).ToList();
        var scores = await Task.WhenAll(tasks);
        return scores.ToList();
    }

    private async Task<float> ScoreOneAsync(string query, string document, CancellationToken ct)
    {
        await _sem.WaitAsync(ct);
        try
        {
            var body = new
            {
                model = _model,
                stream = false,
                messages = new object[]
                {
                    new { role = "system",    content = "Judge whether the Document is relevant to the Query. Output only yes or no. /no_think" },
                    new { role = "user",      content = $"Query: {query}\nDocument: {document}\nRelevant?" },
                    new { role = "assistant", content = "<think>\n\n</think>\n\n" }
                },
                options = new { num_predict = 1 }
            };

            var resp = await _http.PostAsJsonAsync("api/chat", body, ct);
            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(ct);
            var text = result?.Message?.Content?.Trim().ToLowerInvariant() ?? "";
            return text.StartsWith("yes") ? 1.0f : 0.0f;
        }
        finally
        {
            _sem.Release();
        }
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage? Message { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("content")] public string? Content { get; set; }
    }
}

public record RerankedSource(WikiPageRef Source, float CombinedScore, float? RerankScore)
{
    public string Slug => Source.Slug;
    public float EmbedScore => Source.Score;
}

