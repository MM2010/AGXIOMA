using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Combines semantic splitting with recursive splitting in two passes:
///
///  Pass 1 — Semantic (topic-aware):
///    Use <see cref="SemanticChunker"/> to find high-level topic boundaries.
///    This produces coarse semantic sections.
///
///  Pass 2 — Recursive (size-enforcement):
///    Any section that still exceeds the token budget is recursively split by
///    <see cref="RecursiveChunker"/> so the final chunks always fit within
///    <paramref name="maxTokens"/>.
///
/// When no <see cref="IEmbedder"/> is provided, Pass 1 degrades to syntactic
/// sentence-grouping (same as <see cref="SemanticChunker"/> without embedder).
/// The result is always well-bounded by <paramref name="maxTokens"/>.
///
/// Overlap is applied only in Pass 2 (leaf level) to avoid double-overlap.
/// </summary>
public sealed class HybridChunker : IChunker
{
    private readonly SemanticChunker _semantic;
    private readonly RecursiveChunker _recursive = new();

    public ChunkerType ChunkerType => ChunkerType.Hybrid;

    public HybridChunker(IEmbedder? embedder = null)
    {
        _semantic = new SemanticChunker(embedder);
    }

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Pass 1 — coarse semantic split (no overlap at this level).
        var semanticChunks = _semantic.Chunk(text, maxTokens: maxTokens * 4, overlap: 0);

        int charBudget = maxTokens * 4;
        var leafSegments = new List<string>(semanticChunks.Count);

        // Pass 2 — enforce size budget per semantic section.
        foreach (var sc in semanticChunks)
        {
            if (sc.Content.Length <= charBudget)
            {
                leafSegments.Add(sc.Content);
            }
            else
            {
                var sub = _recursive.Chunk(sc.Content, maxTokens, overlap: 0);
                leafSegments.AddRange(sub.Select(s => s.Content));
            }
        }

        // Final: build DocumentChunks with overlap applied at leaf level.
        int overlapChars = overlap * 4;
        var result = new List<DocumentChunk>(leafSegments.Count);
        int cursor = 0, index = 0;

        for (int i = 0; i < leafSegments.Count; i++)
        {
            string content = leafSegments[i];

            if (i > 0 && overlapChars > 0)
            {
                string prev = leafSegments[i - 1];
                string tail = prev.Length > overlapChars ? prev[^overlapChars..] : prev;
                content = tail + " " + content;
            }

            int start = cursor;
            cursor += leafSegments[i].Length + 1;
            result.Add(new DocumentChunk(content.Trim(), index++, start, cursor));
        }

        return result;
    }
}
