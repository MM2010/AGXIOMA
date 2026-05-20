using System.Text.Json;
using System.Text.Json.Serialization;
using AgxiomaLLMWiki;

namespace AgxiomaLegalEmail.Services;

/// <summary>
/// Cross-encoder style reranker using Ollama (qwen2.5-coder:3b — piccolo e già installato).
/// Prende i candidati dalla ricerca vettoriale e li ri-ordina per rilevanza semantica.
/// </summary>
public sealed class LegalReranker
{
    private readonly HttpClient _http;
    private readonly WikiPageWriter _pageWriter;
    private readonly ILogger<LegalReranker> _log;
    private const string Model = "qwen2.5-coder:3b";

    public LegalReranker(IHttpClientFactory httpFactory, WikiPageWriter pageWriter, ILogger<LegalReranker> log)
    {
        _http = httpFactory.CreateClient("ollama");
        _pageWriter = pageWriter;
        _log = log;
    }

    /// <summary>Re-rank WikiPageRef list using LLM relevance scoring.</summary>
    public async Task<List<RerankedSource>> RerankAsync(
        string query,
        List<WikiPageRef> sources,
        CancellationToken ct = default)
    {
        if (sources.Count <= 1)
            return sources.Select(s => new RerankedSource(s, s.Score, null)).ToList();

        // Only rerank top 5 to keep prompt short for the small model
        var candidates = sources.Take(5).ToList();

        // Load full page content for each source
        var fullContents = new List<string>();
        foreach (var src in candidates)
        {
            var content = await _pageWriter.ReadWikiPageAsync(src.Slug, ct) ?? src.Slug;
            fullContents.Add(content.Length > 400 ? content[..400] + "…[troncato]" : content);
        }

        var prompt = BuildPrompt(query, candidates, fullContents);

        try
        {
            var llmOutput = await CallOllamaAsync(prompt, ct);
            var scores = ParseScores(llmOutput, sources.Count);

            var reranked = candidates.Select((src, i) =>
            {
                var rerank = i < scores.Count ? scores[i] : 5.0f;
                // Combined: 30% embed score + 70% LLM rerank score
                var combined = src.Score * 0.3f + (rerank / 10f) * 0.7f;
                var rsi = new RerankedSource(src, combined, rerank);
                // Carry over remaining sources in original order after reranked ones
                return rsi;
            })
            .OrderByDescending(x => x.CombinedScore)
            .ToList();

            // Append any sources beyond the 5 we reranked
            foreach (var src in sources.Skip(5))
                reranked.Add(new RerankedSource(src, src.Score, null));

            _log.LogInformation("[Reranker] Top: {Slug} combined={S:F3} (embed={E:F3} rerank={R:F1})",
                reranked[0].Slug, reranked[0].CombinedScore, reranked[0].EmbedScore, reranked[0].RerankScore ?? 0);
            return reranked;
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Reranker] Fallback to embedding order: {Error}", ex.Message);
            return sources.Select(s => new RerankedSource(s, s.Score, null)).ToList();
        }
    }

    private static string BuildPrompt(string query, List<WikiPageRef> candidates, List<string> contents)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Valuta la rilevanza legale di questi documenti per la domanda.");
        sb.AppendLine();
        sb.AppendLine("DOMANDA: " + query);
        sb.AppendLine();

        for (int i = 0; i < candidates.Count; i++)
            sb.AppendLine($"[{i + 1}] ({candidates[i].Slug})\n{contents[i]}\n");

        sb.AppendLine("Rispondi SOLO con un array JSON di numeri da 0.0 a 10.0, uno per documento.");
        sb.AppendLine("Esempio: [8.5, 3.0, 9.2]");
        return sb.ToString();
    }

    private async Task<string> CallOllamaAsync(string prompt, CancellationToken ct)
    {
        var body = new { model = Model, prompt, stream = false, options = new { num_predict = 256, temperature = 0.1 } };
        var resp = await _http.PostAsJsonAsync("api/generate", body, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<OllamaGenResponse>(ct);
        return json?.Response ?? "[]";
    }

    private static List<float> ParseScores(string resp, int n)
    {
        try
        {
            var txt = resp;
            var s = txt.IndexOf('['); var e = txt.LastIndexOf(']');
            if (s >= 0 && e > s) txt = txt[s..(e + 1)];
            var arr = JsonSerializer.Deserialize<float[]>(txt);
            if (arr?.Length > 0) return arr.ToList();
        }
        catch { }
        return Enumerable.Repeat(7.0f, n).ToList();
    }

    private sealed class OllamaGenResponse { [JsonPropertyName("response")] public string Response { get; set; } = ""; }
}

public record RerankedSource(WikiPageRef Source, float CombinedScore, float? RerankScore)
{
    public string Slug => Source.Slug;
    public float EmbedScore => Source.Score;
}

