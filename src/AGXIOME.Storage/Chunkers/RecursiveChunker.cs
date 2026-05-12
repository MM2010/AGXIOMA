using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Splits text recursively using a priority-ordered separator list:
///   \n\n  →  \n  →  .  →  ?  →  !  →  (single whitespace)
///
/// For each level, the text is split on the current separator. Pieces that still
/// exceed <paramref name="maxTokens"/> (approx 4 chars/token) are recursed with the
/// next separator until the list is exhausted; at that point the piece is passed to
/// <see cref="FixedSizeChunker"/> as a last resort.
///
/// Overlap is re-applied at the leaf level by carrying the tail of the previous chunk
/// (overlap chars) as a prefix for the next chunk — same behaviour as FixedSizeChunker.
/// </summary>
public sealed class RecursiveChunker : IChunker
{
    // Default separator priority: paragraph → line → sentence terminators → word.
    private static readonly string[] DefaultSeparators =
        ["\n\n", "\n", ". ", "? ", "! ", " "];

    public ChunkerType ChunkerType => ChunkerType.Recursive;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        var leafSegments = new List<string>();
        SplitRecursive(text, DefaultSeparators, 0, maxTokens * 4, leafSegments);

        // Merge very-short segments so downstream retrievers get meaningful chunks.
        var merged = MergeShort(leafSegments, maxTokens * 4);

        // Build DocumentChunk list with approximate source offsets and overlap.
        var chunks = new List<DocumentChunk>(merged.Count);
        int charBudget = overlap * 4;
        int index = 0;
        int cursor = 0;

        for (int i = 0; i < merged.Count; i++)
        {
            string content = merged[i];

            // Prepend tail of previous chunk as overlap context (read-only prefix).
            if (i > 0 && charBudget > 0)
            {
                string prev = merged[i - 1];
                string tail = prev.Length > charBudget
                    ? prev[^charBudget..]
                    : prev;
                content = tail + content;
            }

            int start = cursor;
            // Advance cursor by the raw (non-overlapped) segment length.
            cursor += merged[i].Length;

            chunks.Add(new DocumentChunk(content.Trim(), index++, start, cursor));
        }

        return chunks;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Recursive split engine
    // ─────────────────────────────────────────────────────────────────────────

    private static void SplitRecursive(
        string text,
        string[] separators,
        int sepIndex,
        int maxChars,
        List<string> output)
    {
        if (text.Length <= maxChars)
        {
            // Small enough — emit directly.
            if (!string.IsNullOrWhiteSpace(text))
                output.Add(text);
            return;
        }

        if (sepIndex >= separators.Length)
        {
            // No more separators — fall back to hard word split.
            HardSplit(text, maxChars, output);
            return;
        }

        string sep = separators[sepIndex];
        string[] parts = text.Split(sep, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length <= 1)
        {
            // Separator not found in text — try next level.
            SplitRecursive(text, separators, sepIndex + 1, maxChars, output);
            return;
        }

        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.Length <= maxChars)
                output.Add(trimmed);
            else
                SplitRecursive(trimmed, separators, sepIndex + 1, maxChars, output);
        }
    }

    /// <summary>Hard-split a string that cannot be broken on any separator, by character budget.</summary>
    private static void HardSplit(string text, int maxChars, List<string> output)
    {
        for (int i = 0; i < text.Length; i += maxChars)
        {
            int len = Math.Min(maxChars, text.Length - i);
            string part = text.Substring(i, len).Trim();
            if (!string.IsNullOrWhiteSpace(part))
                output.Add(part);
        }
    }

    /// <summary>
    /// Merge consecutive very-short segments into a single chunk so we don't return
    /// dozens of 2-word fragments when text has many sentence terminators.
    /// </summary>
    private static List<string> MergeShort(List<string> segments, int maxChars)
    {
        // Minimum segment length: 10 % of budget.
        int minChars = Math.Max(20, maxChars / 10);

        var result = new List<string>(segments.Count);
        var buffer = new System.Text.StringBuilder();

        foreach (string seg in segments)
        {
            if (buffer.Length == 0)
            {
                buffer.Append(seg);
            }
            else if (buffer.Length + seg.Length < minChars)
            {
                buffer.Append(' ').Append(seg);
            }
            else
            {
                if (buffer.Length > 0)
                    result.Add(buffer.ToString());
                buffer.Clear();
                buffer.Append(seg);
            }
        }

        if (buffer.Length > 0)
            result.Add(buffer.ToString());

        return result;
    }
}
