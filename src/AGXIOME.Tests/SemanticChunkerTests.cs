using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C-02 — SemanticChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class SemanticChunkerTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Build a stub IEmbedder that returns random-but-deterministic unit vectors
    /// with configurable per-index similarity: a "break" vector is returned for the
    /// sentence at <paramref name="breakAt"/>, forcing a semantic boundary there.
    /// </summary>
    private static IEmbedder MakeEmbedder(int dims, int breakAt = -1)
    {
        var embedder = Substitute.For<IEmbedder>();
        embedder.Dimensions.Returns(dims);
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts = callInfo.Arg<IReadOnlyList<string>>();
                var result = new float[texts.Count][];
                for (int i = 0; i < texts.Count; i++)
                {
                    result[i] = new float[dims];
                    if (i == breakAt)
                    {
                        // Orthogonal vector — cosine similarity with neighbors = 0 → always a boundary.
                        result[i][dims - 1] = 1f;
                    }
                    else
                    {
                        // All "same topic" sentences share the same unit vector.
                        result[i][0] = 1f;
                    }
                }
                return Task.FromResult(result);
            });
        return embedder;
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public void ChunkerType_IsSemantic()
    {
        Assert.Equal(ChunkerType.Semantic, new SemanticChunker().ChunkerType);
    }

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
    {
        var result = new SemanticChunker().Chunk(string.Empty);
        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].Content);
    }

    [Fact]
    public void WithoutEmbedder_FallsBackToSyntacticSplit()
    {
        // 12 sentences (4 groups of 3) — syntactic fallback inserts boundary every 3.
        var sentences = Enumerable.Range(1, 12)
            .Select(i => $"Sentence number {i} is here");
        string text = string.Join(". ", sentences) + ".";
        var result = new SemanticChunker().Chunk(text, maxTokens: 200, overlap: 0);
        Assert.True(result.Count >= 2, $"Expected ≥2 chunks without embedder, got {result.Count}");
    }

    [Fact]
    public async Task WithEmbedder_InsertsBoundaryAtLowSimilarity()
    {
        // 6 sentences; sentence index 3 is orthogonal → boundary inserted before it.
        var sentences = Enumerable.Range(1, 6)
            .Select(i => $"This is sentence number {i} about the topic");
        string text = string.Join(". ", sentences) + ".";

        var embedder = MakeEmbedder(dims: 8, breakAt: 3);
        var chunker = new SemanticChunker(embedder) { SimilarityThreshold = 0.5f };

        var result = await chunker.ChunkAsync(text, maxTokens: 200, overlap: 0);
        // Should produce at least 2 chunks due to the forced boundary.
        Assert.True(result.Count >= 2, $"Expected ≥2 chunks with embedder break, got {result.Count}");
    }

    [Fact]
    public void CosineSimilarity_KnownValues()
    {
        // Identical vectors → similarity = 1.0
        float[] v = [1f, 0f, 0f];
        Assert.Equal(1f, SemanticChunker.CosineSimilarity(v, v), precision: 5);

        // Orthogonal vectors → similarity = 0.0
        float[] a = [1f, 0f];
        float[] b = [0f, 1f];
        Assert.Equal(0f, SemanticChunker.CosineSimilarity(a, b), precision: 5);

        // Zero vector → returns 0 without throwing.
        float[] zero = [0f, 0f];
        Assert.Equal(0f, SemanticChunker.CosineSimilarity(zero, v[..2]), precision: 5);
    }
}
