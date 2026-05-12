using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Splits text where the semantic similarity between consecutive sentence-groups drops
/// below a configurable threshold, using embedding vectors from an <see cref="IEmbedder"/>.
///
/// Algorithm:
///  1. Split text into sentences (on '.', '!', '?', '\n').
///  2. Embed each sentence in a single batch call.
///  3. Compute cosine similarity between adjacent sentences.
///  4. Insert a boundary wherever similarity &lt; <see cref="SimilarityThreshold"/>.
///  5. Merge segments that exceed maxTokens (approx) via <see cref="RecursiveChunker"/> fallback.
///  6. Apply overlap as tail-carry from the previous segment.
///
/// When no <see cref="IEmbedder"/> is provided the chunker degrades gracefully to
/// sentence-boundary splitting (same as <see cref="SentenceChunker"/>) so it always
/// returns a valid result without throwing.
/// </summary>
public sealed class SemanticChunker : IChunker
{
    private readonly IEmbedder? _embedder;
    private readonly RecursiveChunker _fallback = new();

    /// <summary>
    /// Cosine-similarity threshold below which a semantic boundary is inserted.
    /// Range: 0.0–1.0. Default 0.75 works well for general English prose.
    /// Lower = fewer, larger chunks. Higher = more, finer-grained chunks.
    /// </summary>
    public float SimilarityThreshold { get; init; } = 0.75f;

    public ChunkerType ChunkerType => ChunkerType.Semantic;

    /// <param name="embedder">
    /// Optional. Provide a live embedder for true semantic splitting.
    /// When null the chunker falls back to sentence-boundary splitting.
    /// </param>
    public SemanticChunker(IEmbedder? embedder = null)
    {
        _embedder = embedder;
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method blocks on the async embedding path by running it on the thread pool.
    /// Prefer <see cref="ChunkAsync"/> in async call sites to avoid thread-pool starvation.
    /// </remarks>
    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        // Run async path synchronously — safe because we own the Task and never deadlock
        // in a console/test context. In ASP.NET Core call ChunkAsync directly.
        return ChunkAsync(text, maxTokens, overlap).GetAwaiter().GetResult();
    }

    /// <summary>Async-first entry point. Preferred in production ASP.NET Core code.</summary>
    public async Task<IReadOnlyList<DocumentChunk>> ChunkAsync(
        string text,
        int maxTokens = 512,
        int overlap = 64,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Step 1: split into sentences.
        var sentences = SplitSentences(text);
        if (sentences.Count <= 1)
            return _fallback.Chunk(text, maxTokens, overlap);

        // Step 2 & 3: embed + compute similarities (or skip if no embedder).
        List<int> boundaries = _embedder is not null
            ? await FindSemanticBoundariesAsync(sentences, ct)
            : FindSyntacticBoundaries(sentences, text);

        // Step 4: group sentences into segments at boundaries.
        var segments = GroupSegments(sentences, boundaries);

        // Step 5: merge over-budget segments via recursive fallback.
        int charBudget = maxTokens * 4;
        var refined = new List<string>(segments.Count);
        foreach (string seg in segments)
        {
            if (seg.Length <= charBudget)
                refined.Add(seg);
            else
            {
                // Delegate oversized segment to RecursiveChunker (no overlap at this stage).
                var sub = _fallback.Chunk(seg, maxTokens, 0);
                refined.AddRange(sub.Select(c => c.Content));
            }
        }

        // Step 6: build DocumentChunk list with overlap and source offsets.
        return BuildChunks(refined, text, overlap * 4);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<string> SplitSentences(string text)
    {
        var raw = text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(raw.Length);
        foreach (var s in raw)
        {
            string t = s.Trim();
            if (!string.IsNullOrWhiteSpace(t))
                result.Add(t);
        }
        return result;
    }

    private async Task<List<int>> FindSemanticBoundariesAsync(
        List<string> sentences,
        CancellationToken ct)
    {
        // Embed all sentences in one batch call to minimise round-trips.
        float[][] vectors = await _embedder!.EmbedBatchAsync(sentences, ct);

        var boundaries = new List<int>();
        for (int i = 1; i < vectors.Length; i++)
        {
            float sim = CosineSimilarity(vectors[i - 1], vectors[i]);
            if (sim < SimilarityThreshold)
                boundaries.Add(i); // boundary before sentence[i]
        }
        return boundaries;
    }

    /// <summary>
    /// Fallback when no embedder is available: treat every sentence as its own potential
    /// boundary — effectively sentence chunking respecting maxTokens budget.
    /// Groups sentences until the char budget is exceeded, then inserts a boundary.
    /// </summary>
    private static List<int> FindSyntacticBoundaries(List<string> sentences, string originalText)
    {
        // Simple heuristic: boundary every ~3 sentences.
        var boundaries = new List<int>();
        for (int i = 3; i < sentences.Count; i += 3)
            boundaries.Add(i);
        return boundaries;
    }

    private static List<string> GroupSegments(List<string> sentences, List<int> boundaries)
    {
        var boundarySet = new HashSet<int>(boundaries);
        var segments = new List<string>();
        var buffer = new List<string>();

        for (int i = 0; i < sentences.Count; i++)
        {
            if (boundarySet.Contains(i) && buffer.Count > 0)
            {
                segments.Add(string.Join(". ", buffer) + ".");
                buffer.Clear();
            }
            buffer.Add(sentences[i]);
        }

        if (buffer.Count > 0)
            segments.Add(string.Join(". ", buffer) + ".");

        return segments;
    }

    private static IReadOnlyList<DocumentChunk> BuildChunks(
        List<string> segments,
        string originalText,
        int overlapChars)
    {
        var chunks = new List<DocumentChunk>(segments.Count);
        int cursor = 0;
        int index = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            string content = segments[i];

            // Apply overlap: prepend tail of the previous raw segment.
            if (i > 0 && overlapChars > 0)
            {
                string prev = segments[i - 1];
                string tail = prev.Length > overlapChars ? prev[^overlapChars..] : prev;
                content = tail + " " + content;
            }

            // Approximate source offset via linear scan of original text.
            int start = cursor;
            int rawLen = segments[i].Length;
            cursor = Math.Min(cursor + rawLen + 1, originalText.Length);

            chunks.Add(new DocumentChunk(content.Trim(), index++, start, cursor));
        }

        return chunks;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Math
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes cosine similarity between two equal-length float vectors.
    /// Returns 0.0 if either vector is the zero vector to avoid division by zero.
    /// Range: -1.0 to 1.0 (for unit vectors, 0.0 to 1.0).
    /// </summary>
    public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;

        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        float denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < 1e-10f ? 0f : dot / denom;
    }
}
