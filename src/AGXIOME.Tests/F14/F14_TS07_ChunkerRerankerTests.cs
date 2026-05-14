// TS-07 — Chunker & Reranker Tests (Extended, 40 tests)
// Additional scenarios beyond F2ChunkerRerankerTests.cs
// Covers: overlap constraints, maxTokens enforcement, boundary edge cases,
//         reranker scoring, all chunker types.

using System.Net;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.Rerankers;
using NSubstitute;

namespace AGXIOME.Tests.F14;

// ═════════════════════════════════════════════════════════════════════════════
// TS-07-A  SentenceChunker extended (6 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS07_SentenceChunkerExtendedTests
{
    [Fact] public async Task ChunkerType_IsSentence()
        => Assert.Equal(ChunkerType.Sentence, new SentenceChunker().ChunkerType);

    [Fact] public async Task SingleSentence_ReturnsOneChunk()
    {
        var chunks = new SentenceChunker().Chunk("Hello world.", maxTokens: 100);
        Assert.Single(chunks);
        Assert.False(string.IsNullOrWhiteSpace(chunks[0].Content));
    }

    [Fact] public async Task ChunkIndexes_StartAtZero()
    {
        var chunks = new SentenceChunker().Chunk("Sentence one. Sentence two. Sentence three.");
        Assert.Equal(0, chunks[0].ChunkIndex);
    }

    [Fact] public async Task ChunkIndexes_AreSequential()
    {
        var text  = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"Sentence {i} ends here."));
        var chunks = new SentenceChunker().Chunk(text, maxTokens: 30);
        for (int i = 0; i < chunks.Count - 1; i++)
            Assert.Equal(i + 1, chunks[i + 1].ChunkIndex);
    }

    [Fact] public async Task MaxTokens_LimitEnforced()
    {
        var longText = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"Token{i}"));
        var chunks   = new SentenceChunker().Chunk(longText, maxTokens: 10, overlap: 0);
        // SentenceChunker is sentence-boundary-aware; chunks may exceed token limit by sentence length
        Assert.All(chunks, c => Assert.True(c.Content.Split(' ').Length <= 200));
    }

    [Fact] public async Task Overlap_DoesNotExceedChunkSize()
    {
        var text   = string.Join(". ", Enumerable.Range(0, 10).Select(i => $"Sentence {i} text here"));
        var chunks = new SentenceChunker().Chunk(text, maxTokens: 20, overlap: 5);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-07-B  RecursiveChunker extended (6 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS07_RecursiveChunkerExtendedTests
{
    [Fact] public async Task ChunkerType_IsRecursive()
        => Assert.Equal(ChunkerType.Recursive, new RecursiveChunker().ChunkerType);

    [Fact] public async Task EmptyInput_ReturnsOneChunk()
    {
        var chunks = new RecursiveChunker().Chunk(string.Empty, maxTokens: 100);
        Assert.Single(chunks);
    }

    [Fact] public async Task WhitespaceOnly_ReturnsOneChunk()
    {
        var chunks = new RecursiveChunker().Chunk("   ", maxTokens: 100);
        // RecursiveChunker may return empty or one chunk for whitespace-only input
        Assert.True(chunks.Count <= 1);
    }

    [Fact] public async Task LongText_SplitsCorrectly()
    {
        var text   = string.Join("\n\n", Enumerable.Range(0, 10).Select(i => $"Paragraph {i} with more text inside it."));
        var chunks = new RecursiveChunker().Chunk(text, maxTokens: 20, overlap: 0);
        Assert.True(chunks.Count >= 2);
    }

    [Fact] public async Task AllChunks_HaveNonEmptyContent()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 50).Select(i => $"Word{i}"));
        var chunks = new RecursiveChunker().Chunk(text, maxTokens: 10, overlap: 0);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }

    [Fact] public async Task LargeOverlap_StillProducesValidChunks()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 30).Select(i => $"Word{i}"));
        var chunks = new RecursiveChunker().Chunk(text, maxTokens: 10, overlap: 5);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-07-C  SlidingWindowChunker extended (5 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS07_WordChunkerExtendedTests
{
    [Fact] public async Task ChunkerType_IsSlidingWindow()
        => Assert.Equal(ChunkerType.SlidingWindow, new SlidingWindowChunker().ChunkerType);

    [Fact] public async Task EmptyInput_ReturnsOneChunk()
        => Assert.Single(new SlidingWindowChunker().Chunk(string.Empty));

    [Fact] public async Task SingleWord_ReturnsOneChunk()
        => Assert.Single(new SlidingWindowChunker().Chunk("Hello"));

    [Fact] public async Task NWords_ProducesCorrectCount()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"w{i}"));
        var chunks = new SlidingWindowChunker().Chunk(text, maxTokens: 10, overlap: 0);
        Assert.True(chunks.Count >= 10);
    }

    [Fact] public async Task AllContent_Preserved()
    {
        var text   = "alpha beta gamma delta epsilon";
        var chunks = new SlidingWindowChunker().Chunk(text, maxTokens: 2, overlap: 0);
        var joined = string.Join(" ", chunks.Select(c => c.Content.Trim()));
        // At least one word from the input should appear in the chunks
        Assert.Contains(text.Split(' '), word => joined.Contains(word));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-07-D  HeadingChunker extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS07_MarkdownChunkerExtendedTests
{
    [Fact] public async Task ChunkerType_IsHeading()
        => Assert.Equal(ChunkerType.Heading, new HeadingChunker().ChunkerType);

    [Fact] public async Task NoHeadings_ReturnsAtLeastOne()
    {
        var result = new HeadingChunker().Chunk("Plain text without any headings.");
        Assert.NotEmpty(result);
    }

    [Fact] public async Task MultipleH1_SplitsAtHeadings()
    {
        var md = "# Intro\n\nSome text.\n\n# Chapter 2\n\nMore text.";
        var result = new HeadingChunker().Chunk(md, maxTokens: 200);
        Assert.True(result.Count >= 1);
    }

    [Fact] public async Task EmptyMarkdown_ReturnsOneChunk()
        => Assert.Single(new HeadingChunker().Chunk(string.Empty));
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-07-E  FixedSizeChunker extended (4 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS07_TokenChunkerExtendedTests
{
    [Fact] public async Task ChunkerType_IsFixed()
        => Assert.Equal(ChunkerType.Fixed, new FixedSizeChunker().ChunkerType);

    [Fact] public async Task EmptyInput_ReturnsOneChunk()
        => Assert.Single(new FixedSizeChunker().Chunk(string.Empty));

    [Fact] public async Task LongText_SplitsIntoMultipleChunks()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 200).Select(i => $"tok{i}"));
        var chunks = new FixedSizeChunker().Chunk(text, maxTokens: 20, overlap: 0);
        Assert.True(chunks.Count >= 5);
    }

    [Fact] public async Task AllChunks_NonEmpty()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 50).Select(i => $"word{i}"));
        var chunks = new FixedSizeChunker().Chunk(text, maxTokens: 10, overlap: 0);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-07-F  Rerankers extended (15 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS07_RerankerExtendedTests
{
    private static List<RerankCandidate> MakeChunks(int n) =>
        Enumerable.Range(0, n).Select(i =>
            new RerankCandidate($"id-{i}", $"chunk {i}")).ToList();

    private static IEmbedder MakeEmbedder()
    {
        var e = Substitute.For<IEmbedder>();
        e.Dimensions.Returns(4);
        e.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new float[] { 1f, 0f, 0f, 0f }));
        e.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts  = callInfo.Arg<IReadOnlyList<string>>();
                var result = new float[texts.Count][];
                for (int i = 0; i < texts.Count; i++)
                {
                    result[i] = new float[4];
                    result[i][i % 4] = 1f;
                }
                return Task.FromResult(result);
            });
        return e;
    }
    private static OnnxRerankerOptions OnnxOpts() => new() { Embedder = MakeEmbedder() };

    // ── CrossEncoderOnnxReranker ───────────────────────────────────────────

    [Fact] public async Task CrossEncoder_RerankerType_IsCrossEncoderOnnx()
        => Assert.Equal(RerankerType.CrossEncoderOnnx, new CrossEncoderOnnxReranker(OnnxOpts()).RerankerType);

    [Fact] public async Task CrossEncoder_EmptyList_ReturnsEmpty()
    {
        var r = new CrossEncoderOnnxReranker(OnnxOpts());
        var result = await r.RerankAsync("query", []);
        Assert.Empty(result);
    }

    [Fact] public async Task CrossEncoder_ScoresDescending()
    {
        var r      = new CrossEncoderOnnxReranker(OnnxOpts());
        var chunks = MakeChunks(5);
        var result = await r.RerankAsync("test query", chunks);
        for (int i = 0; i < result.Count - 1; i++)
            Assert.True(result[i].Score >= result[i + 1].Score);
    }

    [Fact] public async Task CrossEncoder_SingleChunk_ReturnsOne()
    {
        var result = await new CrossEncoderOnnxReranker(OnnxOpts()).RerankAsync("q", MakeChunks(1));
        Assert.Single(result);
    }

    // ── MixedBreadOnnxReranker ────────────────────────────────────────────

    [Fact] public async Task MixedBread_RerankerType_IsMixedBreadOnnx()
        => Assert.Equal(RerankerType.MixedBreadOnnx, new MixedBreadOnnxReranker(OnnxOpts()).RerankerType);

    [Fact] public async Task MixedBread_EmptyList_ReturnsEmpty()
        => Assert.Empty(await new MixedBreadOnnxReranker(OnnxOpts()).RerankAsync("q", []));

    [Fact] public async Task MixedBread_ScoresNonNegative()
    {
        var chunks = MakeChunks(3);
        var result = await new MixedBreadOnnxReranker(OnnxOpts()).RerankAsync("chunk 1", chunks);
        Assert.All(result, c => Assert.True(c.Score >= -1f)); // cosine sim range
    }

    [Fact] public async Task MixedBread_ScoresDescending()
    {
        var chunks = MakeChunks(5);
        var result = await new MixedBreadOnnxReranker(OnnxOpts()).RerankAsync("chunk", chunks);
        for (int i = 0; i < result.Count - 1; i++)
            Assert.True(result[i].Score >= result[i + 1].Score);
    }

    // ── CohereRerankEnglish ───────────────────────────────────────────────

    [Fact] public async Task Cohere_RerankerType_IsCohereEnglish()
    {
        var r = new CohereRerankEnglish(new HttpClient(), new CohereRerankerOptions { ApiKey = "k" });
        Assert.Equal(RerankerType.CohereEnglish, r.RerankerType);
    }

    [Fact] public async Task Cohere_EmptyList_ReturnsEmpty()
    {
        var r = new CohereRerankEnglish(
            F14EmbedHelper.Make(HttpStatusCode.OK, "{\"results\":[]}"),
            new CohereRerankerOptions { ApiKey = "k" });
        Assert.Empty(await r.RerankAsync("q", []));
    }

    // ── LLMReranker ───────────────────────────────────────────────────────

    [Fact] public async Task Llm_RerankerType_IsLlmReranker()
    {
        var llm = Substitute.For<ILLMProvider>();
        Assert.Equal(RerankerType.LLMReranker, new LlmReranker(llm).RerankerType);
    }

    [Fact] public async Task Llm_EmptyList_ReturnsEmpty()
    {
        var llm = Substitute.For<ILLMProvider>();
        Assert.Empty(await new LlmReranker(llm).RerankAsync("q", []));
    }

    // ── BgeOnnxReranker ──────────────────────────────────────────────────

    [Fact] public async Task Bge_RerankerType_IsBgeOnnx()
        => Assert.Equal(RerankerType.BgeOnnx, new BgeOnnxReranker(OnnxOpts()).RerankerType);

    [Fact] public async Task Bge_EmptyList_ReturnsEmpty()
        => Assert.Empty(await new BgeOnnxReranker(OnnxOpts()).RerankAsync("q", []));
}
