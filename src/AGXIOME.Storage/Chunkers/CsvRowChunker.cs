using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Splits CSV text so that each chunk contains at most <c>rowsPerChunk</c> data rows,
/// with the header row prepended to every chunk for self-contained retrieval.
///
/// Rules:
/// - Line 1 is always treated as the header and is never a standalone chunk.
/// - Empty lines are skipped.
/// - If the CSV has no header (i.e. no comma on line 1), the chunker falls back
///   to <see cref="FixedSizeChunker"/> to avoid incorrect assumptions.
/// - <paramref name="maxTokens"/> is re-interpreted as <em>approximate rows per chunk</em>
///   (1 token ≈ 1 row) to align with the chunker contract while staying meaningful for CSV.
/// - <paramref name="overlap"/> rows from the previous chunk are prepended to the next
///   chunk (after the header) to preserve windowed context for vector search.
/// </summary>
public sealed class CsvRowChunker : IChunker
{
    private readonly FixedSizeChunker _fallback = new();

    public ChunkerType ChunkerType => ChunkerType.CsvRow;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Detect header: must contain at least one comma.
        string header = lines[0];
        if (!header.Contains(','))
            return _fallback.Chunk(text, maxTokens, overlap);

        var dataRows = lines.Skip(1).ToList();
        if (dataRows.Count == 0)
            return [new DocumentChunk(header, 0, 0, header.Length)];

        // Reinterpret maxTokens as rows-per-chunk (sensible for CSV).
        int rowsPerChunk = Math.Max(1, maxTokens);
        int overlapRows  = Math.Max(0, Math.Min(overlap, rowsPerChunk - 1));

        var chunks = new List<DocumentChunk>();
        int index  = 0;
        int cursor = header.Length + 1; // offset past the header line

        for (int i = 0; i < dataRows.Count; i += rowsPerChunk)
        {
            // Main window rows.
            var window = dataRows.GetRange(i, Math.Min(rowsPerChunk, dataRows.Count - i));

            // Prepend overlap rows from the previous window.
            var overlapWindow = new List<string>();
            if (i > 0 && overlapRows > 0)
            {
                int overlapStart = Math.Max(0, i - overlapRows);
                overlapWindow = dataRows.GetRange(overlapStart, i - overlapStart);
            }

            var allRows = overlapWindow.Concat(window).ToList();
            string content = header + "\n" + string.Join("\n", allRows);

            int start = cursor;
            int rawLen = window.Sum(r => r.Length + 1);
            cursor += rawLen;

            chunks.Add(new DocumentChunk(content, index++, start, start + content.Length));
        }

        return chunks;
    }
}
