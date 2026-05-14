using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Splits text using a sliding window of fixed size with configurable step and overlap.
///
/// Parameters (re-using the standard contract):
/// - <paramref name="maxTokens"/> = window size in approximate tokens (1 token ≈ 4 chars).
/// - <paramref name="overlap"/>   = number of tokens shared between consecutive windows.
///
/// The step size is <c>maxTokens - overlap</c> tokens. Each window is emitted as one chunk.
/// This chunker is intentionally simple and allocation-efficient: it operates on the
/// original string via index arithmetic without copying sub-strings until the final
/// <see cref="DocumentChunk"/> creation.
/// </summary>
public sealed class SlidingWindowChunker : IChunker
{
    public ChunkerType ChunkerType => ChunkerType.SlidingWindow;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Approximate: 1 token ≈ 4 chars. Work at character level for speed.
        int windowChars = maxTokens * 4;
        int stepChars   = Math.Max(1, (maxTokens - overlap) * 4);

        if (windowChars >= text.Length)
            return [new DocumentChunk(text, 0, 0, text.Length)];

        var chunks = new List<DocumentChunk>();
        int index  = 0;

        for (int start = 0; start < text.Length; start += stepChars)
        {
            int end     = Math.Min(start + windowChars, text.Length);
            string content = text[start..end];

            // Snap to nearest word boundary to avoid mid-word cuts.
            if (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                int boundary = content.LastIndexOf(' ');
                if (boundary > 0)
                {
                    end     = start + boundary;
                    content = text[start..end];
                }
            }

            if (!string.IsNullOrWhiteSpace(content))
                chunks.Add(new DocumentChunk(content, index++, start, end));

            // Stop if we've consumed the whole string.
            if (end >= text.Length) break;
        }

        return chunks.Count > 0
            ? chunks
            : [new DocumentChunk(text, 0, 0, text.Length)];
    }
}
