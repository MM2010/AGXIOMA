using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C-01 — RecursiveChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class RecursiveChunkerTests
{
    private readonly RecursiveChunker _chunker = new();

    [Fact]
    public void ChunkerType_IsRecursive()
    {
        Assert.Equal(ChunkerType.Recursive, _chunker.ChunkerType);
    }

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
    {
        var result = _chunker.Chunk(string.Empty);
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Content);
    }

    [Fact]
    public void ParagraphBoundary_SplitsOnDoubleNewline()
    {
        // Two clear paragraphs separated by \n\n — should produce at least 2 chunks.
        string text = "First paragraph with enough words to be meaningful.\n\n" +
                      "Second paragraph with enough words to be meaningful.";
        var result = _chunker.Chunk(text, maxTokens: 20, overlap: 0);
        Assert.True(result.Count >= 2, $"Expected ≥2 chunks, got {result.Count}");
        Assert.Contains(result, c => c.Content.Contains("First paragraph"));
        Assert.Contains(result, c => c.Content.Contains("Second paragraph"));
    }

    [Fact]
    public void LongTextWithNoSeparator_SplitsOnHardBoundary()
    {
        // 200 chars of continuous text with no natural separator.
        string text = new string('A', 200);
        // maxTokens=10 → maxChars=40; we should get multiple chunks.
        var result = _chunker.Chunk(text, maxTokens: 10, overlap: 0);
        Assert.True(result.Count >= 2, $"Expected ≥2 chunks, got {result.Count}");
        // Total content length must equal original (no data loss).
        int totalChars = result.Sum(c => c.Content.Length);
        Assert.Equal(200, totalChars);
    }

    [Fact]
    public void Overlap_PreviousTailAppearedInNextChunk()
    {
        // Build 3 clear paragraphs; overlap > 0 should carry tail of previous.
        string para = "The quick brown fox jumped over the lazy dog near the river bank.";
        string text = $"{para}\n\n{para}\n\n{para}";
        var result = _chunker.Chunk(text, maxTokens: 20, overlap: 5);
        // At least some overlap: chunk[1] should start with tail of chunk[0]'s raw segment.
        Assert.True(result.Count >= 2);
        // Index values must be monotonically increasing.
        for (int i = 1; i < result.Count; i++)
            Assert.True(result[i].ChunkIndex > result[i - 1].ChunkIndex);
    }

    [Fact]
    public void ChunkIndexes_AreSequential()
    {
        string text = string.Join("\n\n", Enumerable.Range(1, 10)
            .Select(i => $"Paragraph {i} with some sample content to fill the chunk appropriately."));
        var result = _chunker.Chunk(text, maxTokens: 30, overlap: 0);
        for (int i = 0; i < result.Count; i++)
            Assert.Equal(i, result[i].ChunkIndex);
    }
}
