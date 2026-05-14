using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Uses an <see cref="ILLMProvider"/> to decide where to split the text.
///
/// Strategy:
///  1. Split the input into sentence-level candidates with <see cref="RecursiveChunker"/>.
///  2. Feed a sliding context window of candidates to the LLM, asking it to identify
///     natural topic boundaries (returns a JSON array of 0-based candidate indices
///     where a new chunk should begin).
///  3. Group candidates at the returned boundaries.
///  4. Apply overlap as tail-carry from the previous segment.
///
/// When no <see cref="ILLMProvider"/> is provided the chunker degrades gracefully to
/// <see cref="RecursiveChunker"/> output so it always returns valid results.
///
/// Prompt is intentionally minimal to keep token cost low. The LLM is instructed to
/// return only a JSON integer array — no explanation — to prevent prompt-injection via
/// document content affecting the chunking boundary list.
/// </summary>
public sealed class AgenticChunker : IChunker
{
    private readonly ILLMProvider? _llm;
    private readonly RecursiveChunker _fallback = new();

    /// <summary>Maximum number of candidate sentences sent to the LLM per window call.</summary>
    public int WindowSize { get; init; } = 20;

    public ChunkerType ChunkerType => ChunkerType.Agentic;

    public AgenticChunker(ILLMProvider? llm = null)
    {
        _llm = llm;
    }

    /// <inheritdoc />
    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
        => ChunkAsync(text, maxTokens, overlap).GetAwaiter().GetResult();

    /// <summary>Async-first entry point preferred in production call sites.</summary>
    public async Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        string text,
        int maxTokens = 512,
        int overlap = 64,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Step 1 — pre-split into sentence-level candidates.
        var candidates = _fallback
            .Chunk(text, maxTokens: 30, overlap: 0)
            .Select(c => c.Content)
            .ToList();

        if (candidates.Count <= 1 || _llm is null)
            return _fallback.Chunk(text, maxTokens, overlap);

        // Step 2 — ask the LLM to identify topic boundaries in sliding windows.
        var boundarySet = new HashSet<int>();
        for (int start = 0; start < candidates.Count; start += WindowSize)
        {
            int end = Math.Min(start + WindowSize, candidates.Count);
            var window = candidates.GetRange(start, end - start);
            var indices = await GetBoundaryIndicesAsync(window, start, ct);
            foreach (int idx in indices)
                boundarySet.Add(idx);
        }

        // Step 3 — group candidates.
        var segments = GroupAtBoundaries(candidates, boundarySet);

        // Step 4 — build final chunks with overlap.
        return BuildChunks(segments, overlap * 4);
    }

    // ─────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<int>> GetBoundaryIndicesAsync(
        List<string> window, int offset, CancellationToken ct)
    {
        // Build a minimal, injection-resistant prompt.
        // The LLM is instructed to respond ONLY with a JSON array of integers.
        var numberedList = string.Join("\n",
            window.Select((s, i) => $"[{offset + i}] {EscapeForPrompt(s)}"));

        string prompt =
            "You are a document chunking assistant. " +
            "Read the following numbered text segments and return ONLY a JSON integer array " +
            "of the segment indices where a new topic begins (i.e., where you would start " +
            "a new chunk). Do not include any explanation or other text.\n\n" +
            $"Segments:\n{numberedList}\n\nResponse (JSON array only):";

        var request = new LLMCompletionRequest(
            SystemPrompt: "You are a document chunking assistant. Output ONLY a JSON integer array.",
            UserPrompt: prompt,
            History: null,
            Temperature: 0f,
            MaxTokens: 128,
            Model: string.Empty,
            Tools: null);
        LLMWIKYResponse response;
        try
        {
            response = await _llm!.CompleteAsync(request, ct);
        }
        catch
        {
            return []; // graceful degradation on LLM failure
        }

        return ParseJsonIntArray(response.Content ?? "[]");
    }

    /// <summary>
    /// Strip characters that could be used for prompt injection via document content.
    /// We only need the text content for presentation; we don't eval the response.
    /// </summary>
    private static string EscapeForPrompt(string s)
        => s.Replace("[", "(").Replace("]", ")").Replace("\"", "'");

    private static IReadOnlyList<int> ParseJsonIntArray(string json)
    {
        try
        {
            json = json.Trim();
            // Extract first [...] array from response in case the LLM added extra text.
            int start = json.IndexOf('[');
            int end   = json.LastIndexOf(']');
            if (start < 0 || end <= start) return [];

            string inner = json[(start + 1)..end];
            if (string.IsNullOrWhiteSpace(inner)) return [];

            return inner.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => int.TryParse(p, out _))
                        .Select(int.Parse)
                        .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<string> GroupAtBoundaries(List<string> candidates, HashSet<int> boundaries)
    {
        var segments = new List<string>();
        var buffer   = new List<string>();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (boundaries.Contains(i) && buffer.Count > 0)
            {
                segments.Add(string.Join(" ", buffer));
                buffer.Clear();
            }
            buffer.Add(candidates[i]);
        }

        if (buffer.Count > 0)
            segments.Add(string.Join(" ", buffer));

        return segments;
    }

    private static IReadOnlyList<DocumentChunk> BuildChunks(List<string> segments, int overlapChars)
    {
        var chunks = new List<DocumentChunk>(segments.Count);
        int cursor = 0, index = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            string content = segments[i];

            if (i > 0 && overlapChars > 0)
            {
                string prev = segments[i - 1];
                string tail = prev.Length > overlapChars ? prev[^overlapChars..] : prev;
                content = tail + " " + content;
            }

            int start = cursor;
            cursor += segments[i].Length + 1;
            chunks.Add(new DocumentChunk(content.Trim(), index++, start, cursor));
        }

        return chunks;
    }
}
