using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Rerankers;

// ─────────────────────────────────────────────────────────────────────────────
// RK-08 — LlmReranker  (relevance scoring via ILLMProvider)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Reranker that uses an <see cref="ILLMProvider"/> to score each candidate's
/// relevance to the query on a 0–1 scale.
///
/// Scoring strategy:
///   For each candidate, send a short prompt asking the LLM to output a single
///   float relevance score in [0,1]. Candidates are sorted descending by score.
///
/// Security note:
///   Candidate content is included in the prompt but marked as untrusted user
///   content to limit prompt injection impact. The LLM response is parsed purely
///   as a float — no code execution or instruction following occurs on the result.
///
/// Cost note:
///   One LLM call per candidate. Use <see cref="BatchSize"/> to parallelise calls
///   (default 4). For large candidate lists consider a cheaper cross-encoder reranker.
/// </summary>
public sealed class LlmReranker : IReranker
{
    private readonly ILLMProvider _llm;

    /// <summary>Number of candidates scored concurrently. Default 4.</summary>
    public int BatchSize { get; init; } = 4;

    public RerankerType RerankerType => RerankerType.LLMReranker;

    public LlmReranker(ILLMProvider llm)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        string query,
        IReadOnlyList<RerankCandidate> candidates,
        int topK = 0,
        CancellationToken ct = default)
    {
        if (candidates.Count == 0) return [];

        // Score all candidates in parallel batches.
        var scores = new float[candidates.Count];
        var semaphore = new SemaphoreSlim(BatchSize, BatchSize);

        var tasks = candidates.Select(async (candidate, i) =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                scores[i] = await ScoreCandidateAsync(query, candidate.Text, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Sort descending by score.
        var ranked = candidates
            .Select((c, i) => (Candidate: c, Score: scores[i], OriginalRank: i))
            .OrderByDescending(x => x.Score)
            .ToList();

        int take = topK <= 0 ? ranked.Count : Math.Min(topK, ranked.Count);

        return ranked
            .Take(take)
            .Select((x, newRank) => new RerankResult(
                Id:           x.Candidate.Id,
                Text:         x.Candidate.Text,
                Score:        x.Score,
                OriginalRank: x.OriginalRank,
                NewRank:      newRank,
                Metadata:     x.Candidate.Metadata))
            .ToList();
    }

    private async Task<float> ScoreCandidateAsync(
        string query, string documentText, CancellationToken ct)
    {
        // Minimal prompt — instructs LLM to output only a float.
        // Document text is sandboxed between delimiters to reduce injection risk.
        string prompt =
            "Rate the relevance of the following document to the query on a scale of 0.0 to 1.0. " +
            "Output ONLY the numeric score, nothing else.\n\n" +
            $"Query: {EscapeContent(query)}\n\n" +
            $"Document:\n<<<BEGIN DOCUMENT>>>\n{EscapeContent(documentText)}\n<<<END DOCUMENT>>>\n\n" +
            "Score:";

        var request = new LLMCompletionRequest(
            SystemPrompt: "You are a relevance scoring assistant. Output ONLY a single float score.",
            UserPrompt: prompt,
            History: null,
            Temperature: 0f,
            MaxTokens: 8,
            Model: string.Empty,
            Tools: null);

        LLMWIKYResponse response;
        try
        {
            response = await _llm.CompleteAsync(request, ct);
        }
        catch
        {
            return 0f; // graceful degradation
        }

        return ParseScore(response.Content ?? "0");
    }

    /// <summary>Strip prompt-injection attempts from user content passed into the prompt.</summary>
    private static string EscapeContent(string s)
        => s.Replace("<<<", "< < <").Replace(">>>", "> > >");

    private static float ParseScore(string raw)
    {
        string trimmed = raw.Trim();
        // Take first numeric token in case LLM added extra text despite instructions.
        foreach (string token in trimmed.Split([' ', '\n', '\r', '\t']))
        {
            if (float.TryParse(token, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float score))
            {
                return Math.Clamp(score, 0f, 1f);
            }
        }
        return 0f;
    }
}
