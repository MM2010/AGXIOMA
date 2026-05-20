using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AgxiomaLLMWiki;

/// <summary>
/// Lightweight reranker that uses a local Ollama LLM (llama3.1:8b) to score
/// and re-rank candidate documents for a given query.
///
/// This provides cross-encoder quality without needing a dedicated reranker model
/// or Ollama 0.5.0+ /api/rerank endpoint.
///
/// Batch design: all candidates are sent in a single prompt, so only 1 LLM call
/// per query (not K calls). Returns scores 0-10 for each candidate.
/// </summary>
public sealed class OllamaReranker
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<OllamaReranker> _log;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public OllamaReranker(HttpClient http, string model, ILogger<OllamaReranker> log)
    {
        _http = http;
        _model = model;
        _log = log;
    }

    /// <summary>
    /// Reranks candidate documents by relevance to the query.
    /// Returns the same list with updated scores (0.0–10.0) sorted descending.
    /// On failure, returns candidates unchanged (original order/scores preserved).
    /// </summary>
    public async Task<List<RerankCandidate>> RerankAsync(
        string query,
        List<RerankCandidate> candidates,
        CancellationToken ct = default)
    {
        if (candidates.Count <= 2)
            return candidates; // not worth reranking

        try
        {
            // Build a single prompt with all candidates
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Sei un valutatore di pertinenza legale. Per ogni documento elencato, assegna un punteggio da 0 a 10 che indica quanto è rilevante per la domanda dell'utente.");
            sb.AppendLine();
            sb.AppendLine($"DOMANDA: {query}");
            sb.AppendLine();
            sb.AppendLine("--- DOCUMENTI ---");

            for (int i = 0; i < candidates.Count; i++)
            {
                // Truncate to avoid token overflow
                var text = candidates[i].Text.Length > 800
                    ? candidates[i].Text[..800] + "…"
                    : candidates[i].Text;
                sb.AppendLine($"<doc id=\"{i}\">{text}</doc>");
            }

            sb.AppendLine();
            sb.AppendLine("--- ISTRUZIONI ---");
            sb.AppendLine("Restituisci SOLO un JSON array di numeri, uno per ogni documento, con il punteggio di pertinenza (0=irrilevante, 10=perfettamente pertinente).");
            sb.AppendLine("Esempio: [8.5, 3.0, 6.2]");
            sb.AppendLine("Non aggiungere spiegazioni, solo il JSON array.");

            var payload = new
            {
                model = _model,
                prompt = sb.ToString(),
                stream = false,
                temperature = 0.1,
                max_tokens = 512
            };

            var resp = await _http.PostAsJsonAsync("api/generate", payload, JsonOpts, ct);
            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadFromJsonAsync<OllamaGenerateResponse>(JsonOpts, ct);
            var scores = ParseScoreArray(raw?.Response);

            if (scores == null || scores.Length != candidates.Count)
            {
                _log.LogWarning("[Reranker] LLM returned {Count} scores for {Expected} candidates — skipping rerank",
                    scores?.Length ?? 0, candidates.Count);
                return candidates;
            }

            // Apply scores and re-sort
            for (int i = 0; i < candidates.Count; i++)
                candidates[i] = candidates[i] with { RerankScore = Math.Clamp(scores[i], 0f, 10f) };

            var reranked = candidates.OrderByDescending(c => c.RerankScore).ToList();
            _log.LogInformation("[Reranker] Reranked {N} candidates. Top: {Top} ({F:F1}), Bottom: {Bot} ({B:F1})",
                candidates.Count, reranked[0].Slug, reranked[0].RerankScore,
                reranked[^1].Slug, reranked[^1].RerankScore);

            return reranked;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Reranker] Failed — returning original order");
            return candidates;
        }
    }

    private static float[]? ParseScoreArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Find the first '[' and last ']'
        var start = raw.IndexOf('[');
        var end = raw.LastIndexOf(']');
        if (start < 0 || end <= start) return null;

        var json = raw[start..(end + 1)];
        try { return JsonSerializer.Deserialize<float[]>(json); }
        catch { return null; }
    }

    private sealed class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = "";
    }
}

/// <summary>
/// A single candidate for reranking with both the original (bi-encoder) score
/// and the cross-encoder rerank score.
/// </summary>
public record RerankCandidate(
    string Slug,
    string Text,
    double OriginalScore,
    float RerankScore
);
