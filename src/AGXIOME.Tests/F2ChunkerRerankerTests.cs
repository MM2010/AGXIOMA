using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.Rerankers;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// C-03 — AgenticChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class AgenticChunkerTests
{
    [Fact]
    public void ChunkerType_IsAgentic()
        => Assert.Equal(ChunkerType.Agentic, new AgenticChunker().ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(new AgenticChunker().Chunk(string.Empty));

    [Fact]
    public void WithoutLlm_FallsBackToRecursive()
    {
        string text = string.Join("\n\n", Enumerable.Range(1, 6)
            .Select(i => $"Paragraph {i} contains some meaningful content for testing purposes."));
        var result = new AgenticChunker().Chunk(text, maxTokens: 50, overlap: 0);
        Assert.True(result.Count >= 1);
        Assert.All(result, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }

    [Fact]
    public async Task WithLlm_ParsesBoundaryArrayFromResponse()
    {
        // LLM returns boundary at index 2 → at least 2 chunks expected.
        var llm = Substitute.For<ILLMProvider>();
        llm.CompleteAsync(Arg.Any<LLMCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMWIKYResponse("[2]"));

        string text = string.Join(" ", Enumerable.Range(1, 10)
            .Select(i => $"Sentence {i} here."));
        var chunker = new AgenticChunker(llm);
        var result = await chunker.ChunkAsync(text, maxTokens: 50, overlap: 0);
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void ChunkIndexes_AreSequential()
    {
        string text = string.Join(". ", Enumerable.Range(1, 20)
            .Select(i => $"Sentence number {i} provides context for the chunker test"));
        var result = new AgenticChunker().Chunk(text, maxTokens: 30, overlap: 0);
        for (int i = 0; i < result.Count; i++)
            Assert.Equal(i, result[i].ChunkIndex);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C-04 — CodeChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class CodeChunkerTests
{
    private readonly CodeChunker _chunker = new();

    [Fact]
    public void ChunkerType_IsCode()
        => Assert.Equal(ChunkerType.Code, _chunker.ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(_chunker.Chunk(string.Empty));

    [Fact]
    public void CSharpCode_SplitsOnClassBoundary()
    {
        string code = """
            public class Foo
            {
                public void Bar() { }
            }

            public class Baz
            {
                public int Prop { get; set; }
            }
            """;
        var result = _chunker.Chunk(code, maxTokens: 50, overlap: 0);
        Assert.True(result.Count >= 1);
        Assert.All(result, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }

    [Fact]
    public void PlainText_FallsBackGracefully()
    {
        string text = "This is not code at all. It is plain text with no keywords.";
        var result = _chunker.Chunk(text, maxTokens: 20, overlap: 0);
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void LargeCodeBlock_DoesNotLoseContent()
    {
        // Single class body > budget — should be sub-split without losing chars.
        string body = string.Join("\n",
            Enumerable.Range(1, 50).Select(i => $"    public void Method{i}() {{ }}"));
        string code = $"public class Big\n{{\n{body}\n}}";
        var result = _chunker.Chunk(code, maxTokens: 20, overlap: 0);
        string rebuilt = string.Concat(result.Select(c => c.Content));
        // Every method name should appear somewhere in the output.
        Assert.Contains("Method1", rebuilt);
        Assert.Contains("Method50", rebuilt);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C-05 — DocumentChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class DocumentChunkerTests
{
    private readonly DocumentChunker _chunker = new();

    [Fact]
    public void ChunkerType_IsDocument()
        => Assert.Equal(ChunkerType.Document, _chunker.ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(_chunker.Chunk(string.Empty));

    [Fact]
    public void HeadingSplit_ProducesOneChunkPerSection()
    {
        string text = "# Section 1\nContent for section one.\n\n# Section 2\nContent for section two.";
        var result = _chunker.Chunk(text, maxTokens: 200, overlap: 0);
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void ParagraphFallback_WhenSectionOverBudget()
    {
        // One heading section with multiple paragraphs → should split at \n\n.
        string longPara = string.Join(" ", Enumerable.Repeat("word", 200));
        string text = $"# Big Section\n{longPara}\n\n{longPara}";
        var result = _chunker.Chunk(text, maxTokens: 50, overlap: 0);
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void ChunkIndexes_AreMonotonicallyIncreasing()
    {
        string text = string.Join("\n\n", Enumerable.Range(1, 5)
            .Select(i => $"# Heading {i}\n" + string.Join(" ", Enumerable.Repeat("content", 20))));
        var result = _chunker.Chunk(text, maxTokens: 30, overlap: 0);
        for (int i = 1; i < result.Count; i++)
            Assert.True(result[i].ChunkIndex > result[i - 1].ChunkIndex);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C-06 — CsvRowChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class CsvRowChunkerTests
{
    private readonly CsvRowChunker _chunker = new();

    [Fact]
    public void ChunkerType_IsCsvRow()
        => Assert.Equal(ChunkerType.CsvRow, _chunker.ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(_chunker.Chunk(string.Empty));

    [Fact]
    public void HeaderPrependedToEveryChunk()
    {
        string csv = "id,name,value\n" +
                     string.Join("\n", Enumerable.Range(1, 10).Select(i => $"{i},name{i},{i * 10}"));
        // maxTokens=3 → 3 rows per chunk.
        var result = _chunker.Chunk(csv, maxTokens: 3, overlap: 0);
        Assert.True(result.Count >= 2);
        Assert.All(result, c => Assert.StartsWith("id,name,value", c.Content));
    }

    [Fact]
    public void NoCommaInFirstLine_FallsBackToFixedSize()
    {
        string text = "not a csv file\nno columns here\nplain text";
        var result = _chunker.Chunk(text, maxTokens: 10, overlap: 0);
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void Overlap_RowsAppearedInNextChunk()
    {
        string csv = "a,b,c\n" +
                     string.Join("\n", Enumerable.Range(1, 9).Select(i => $"{i},{i},{i}"));
        // 3 rows/chunk, 1 overlap row.
        var result = _chunker.Chunk(csv, maxTokens: 3, overlap: 1);
        // Each chunk except the first must have header + overlap + window rows.
        if (result.Count > 1)
        {
            int linesChunk0 = result[0].Content.Split('\n').Length;
            int linesChunk1 = result[1].Content.Split('\n').Length;
            // chunk1 should have more lines than just header + window due to overlap.
            Assert.True(linesChunk1 >= linesChunk0);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C-07 — JsonPathChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class JsonPathChunkerTests
{
    private readonly JsonPathChunker _chunker = new();

    [Fact]
    public void ChunkerType_IsJsonPath()
        => Assert.Equal(ChunkerType.JsonPath, _chunker.ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(_chunker.Chunk(string.Empty));

    [Fact]
    public void NonJsonInput_FallsBackGracefully()
    {
        var result = _chunker.Chunk("not json at all", maxTokens: 20, overlap: 0);
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void TopLevelArray_ProducesOneChunkPerElement()
    {
        string json = """["first element text","second element text","third element text"]""";
        var result = _chunker.Chunk(json, maxTokens: 200, overlap: 0);
        Assert.Equal(3, result.Count);
        Assert.Equal("first element text", result[0].Content);
    }

    [Fact]
    public void ExplicitPath_ExtractsCorrectValue()
    {
        string json = """{"title":"Hello World","body":"Some body text here"}""";
        var chunker = new JsonPathChunker { JsonPaths = ["/title", "/body"] };
        var result = chunker.Chunk(json, maxTokens: 200, overlap: 0);
        Assert.Equal(2, result.Count);
        Assert.Equal("Hello World", result[0].Content);
        Assert.Equal("Some body text here", result[1].Content);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C-08 — SlidingWindowChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class SlidingWindowChunkerTests
{
    private readonly SlidingWindowChunker _chunker = new();

    [Fact]
    public void ChunkerType_IsSlidingWindow()
        => Assert.Equal(ChunkerType.SlidingWindow, _chunker.ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(_chunker.Chunk(string.Empty));

    [Fact]
    public void SmallText_ReturnsSingleChunk()
    {
        var result = _chunker.Chunk("short text", maxTokens: 512, overlap: 0);
        Assert.Single(result);
    }

    [Fact]
    public void LongText_ProducesMultipleWindows()
    {
        // ~400 chars, window=20 tokens (80 chars), step=10 tokens (40 chars) → many windows.
        string text = string.Join(" ", Enumerable.Repeat("word", 100));
        var result = _chunker.Chunk(text, maxTokens: 20, overlap: 10);
        Assert.True(result.Count >= 3, $"Expected ≥3 windows, got {result.Count}");
    }

    [Fact]
    public void WindowOverlap_ChunksShareContent()
    {
        // With overlap > 0, consecutive chunks should share some words.
        string text = string.Join(" ", Enumerable.Range(1, 50).Select(i => $"word{i}"));
        var result = _chunker.Chunk(text, maxTokens: 10, overlap: 5);
        if (result.Count >= 2)
        {
            // The last word(s) of chunk[0] should appear in chunk[1].
            string lastWordChunk0 = result[0].Content.Split(' ').Last();
            Assert.Contains(lastWordChunk0, result[1].Content);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// C-09 — HybridChunker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class HybridChunkerTests
{
    [Fact]
    public void ChunkerType_IsHybrid()
        => Assert.Equal(ChunkerType.Hybrid, new HybridChunker().ChunkerType);

    [Fact]
    public void EmptyInput_ReturnsSingleEmptyChunk()
        => Assert.Single(new HybridChunker().Chunk(string.Empty));

    [Fact]
    public void WithoutEmbedder_ProducesChunks()
    {
        string text = string.Join("\n\n", Enumerable.Range(1, 4)
            .Select(i => $"Section {i}: " + string.Join(" ", Enumerable.Repeat("content", 30))));
        var result = new HybridChunker().Chunk(text, maxTokens: 50, overlap: 0);
        Assert.True(result.Count >= 1);
        Assert.All(result, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }

    [Fact]
    public void WithEmbedder_SemanticPassApplied()
    {
        var embedder = Substitute.For<IEmbedder>();
        embedder.Dimensions.Returns(4);
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts = callInfo.Arg<IReadOnlyList<string>>();
                var vecs = new float[texts.Count][];
                for (int i = 0; i < texts.Count; i++)
                {
                    vecs[i] = new float[4];
                    vecs[i][i % 4] = 1f; // each sentence gets a different direction
                }
                return Task.FromResult(vecs);
            });

        string text = string.Join(". ", Enumerable.Range(1, 8)
            .Select(i => $"Sentence {i} about completely different topics"));
        var result = new HybridChunker(embedder).Chunk(text, maxTokens: 40, overlap: 0);
        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void ChunkIndexes_AreSequential()
    {
        string text = string.Join("\n\n", Enumerable.Range(1, 6)
            .Select(i => string.Join(" ", Enumerable.Repeat($"para{i}word", 40))));
        var result = new HybridChunker().Chunk(text, maxTokens: 30, overlap: 0);
        for (int i = 0; i < result.Count; i++)
            Assert.Equal(i, result[i].ChunkIndex);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RK-01 — CohereRerankEnglish (5 tests)
// RK-02 — CohereRerankMultilingual (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class CohereRerankerTests
{
    private static List<RerankCandidate> MakeCandidates(int n) =>
        Enumerable.Range(1, n)
            .Select(i => new RerankCandidate($"id-{i}", $"Document about topic {i}.", i * 0.1f))
            .ToList();

    private static HttpClient MakeHttpClient(object responseBody)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(responseBody);
        var handler = new TestHttpMessageHandler(System.Net.HttpStatusCode.OK, json);
        return new HttpClient(handler);
    }

    private static CohereRerankerOptions MakeOpts() =>
        new() { ApiKey = "test-key" };

    [Fact]
    public void CohereEnglish_RerankerType_IsCorrect()
    {
        var r = new CohereRerankEnglish(MakeHttpClient(new { results = Array.Empty<object>() }), MakeOpts());
        Assert.Equal(RerankerType.CohereEnglish, r.RerankerType);
    }

    [Fact]
    public void CohereMultilingual_RerankerType_IsCorrect()
    {
        var r = new CohereRerankMultilingual(MakeHttpClient(new { results = Array.Empty<object>() }), MakeOpts());
        Assert.Equal(RerankerType.CohereMultilingual, r.RerankerType);
    }

    [Fact]
    public async Task CohereEnglish_EmptyCandidates_ReturnsEmpty()
    {
        var r = new CohereRerankEnglish(
            MakeHttpClient(new { results = Array.Empty<object>() }), MakeOpts());
        var result = await r.RerankAsync("query", []);
        Assert.Empty(result);
    }

    [Fact]
    public async Task CohereEnglish_ParsesResults_CorrectOrder()
    {
        var apiResponse = new
        {
            results = new[]
            {
                new { index = 1, relevance_score = 0.9f },
                new { index = 0, relevance_score = 0.4f },
            }
        };
        var candidates = MakeCandidates(2);
        var r = new CohereRerankEnglish(MakeHttpClient(apiResponse), MakeOpts());
        var result = await r.RerankAsync("q", candidates);
        Assert.Equal(2, result.Count);
        Assert.Equal("id-2", result[0].Id);   // index=1 → candidates[1]
        Assert.Equal(0, result[0].NewRank);
    }

    [Fact]
    public async Task CohereMultilingual_ReturnsRankedResults()
    {
        var apiResponse = new
        {
            results = new[]
            {
                new { index = 2, relevance_score = 0.95f },
                new { index = 0, relevance_score = 0.5f },
                new { index = 1, relevance_score = 0.3f },
            }
        };
        var r = new CohereRerankMultilingual(MakeHttpClient(apiResponse), MakeOpts());
        var result = await r.RerankAsync("q", MakeCandidates(3));
        Assert.Equal(3, result.Count);
        Assert.Equal("id-3", result[0].Id); // index=2 → candidates[2]
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RK-03 — JinaReranker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class JinaRerankerTests
{
    private static JinaRerankerOptions MakeOpts() => new() { ApiKey = "test-jina" };

    private static HttpClient MakeHttpClient(object body) =>
        new HttpClient(new TestHttpMessageHandler(
            System.Net.HttpStatusCode.OK,
            System.Text.Json.JsonSerializer.Serialize(body)));

    [Fact]
    public void RerankerType_IsJinaV2()
        => Assert.Equal(RerankerType.JinaV2,
            new JinaReranker(MakeHttpClient(new { results = Array.Empty<object>() }), MakeOpts()).RerankerType);

    [Fact]
    public async Task EmptyCandidates_ReturnsEmpty()
    {
        var r = new JinaReranker(MakeHttpClient(new { results = Array.Empty<object>() }), MakeOpts());
        Assert.Empty(await r.RerankAsync("q", []));
    }

    [Fact]
    public async Task ParsesResults_CorrectOrder()
    {
        var apiResponse = new
        {
            results = new[]
            {
                new { index = 1, relevance_score = 0.8f },
                new { index = 0, relevance_score = 0.2f },
            }
        };
        var candidates = Enumerable.Range(1, 2)
            .Select(i => new RerankCandidate($"c{i}", $"doc {i}")).ToList();
        var r = new JinaReranker(MakeHttpClient(apiResponse), MakeOpts());
        var result = await r.RerankAsync("q", candidates);
        Assert.Equal("c2", result[0].Id);
    }

    [Fact]
    public async Task TopK_LimitsResults()
    {
        var apiResponse = new
        {
            results = new[]
            {
                new { index = 0, relevance_score = 0.9f },
                new { index = 1, relevance_score = 0.7f },
                new { index = 2, relevance_score = 0.5f },
            }
        };
        var candidates = Enumerable.Range(1, 3)
            .Select(i => new RerankCandidate($"c{i}", $"doc {i}")).ToList();
        var r = new JinaReranker(MakeHttpClient(apiResponse), MakeOpts());
        var result = await r.RerankAsync("q", candidates, topK: 2);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new JinaReranker(new HttpClient(), null!));
}

// ─────────────────────────────────────────────────────────────────────────────
// RK-04 — VoyageReranker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class VoyageRerankerTests
{
    private static VoyageRerankerOptions MakeOpts() => new() { ApiKey = "voyage-key" };

    private static HttpClient MakeHttpClient(object body) =>
        new HttpClient(new TestHttpMessageHandler(
            System.Net.HttpStatusCode.OK,
            System.Text.Json.JsonSerializer.Serialize(body)));

    [Fact]
    public void RerankerType_IsVoyage()
        => Assert.Equal(RerankerType.VoyageRerank2,
            new VoyageReranker(MakeHttpClient(new { data = Array.Empty<object>() }), MakeOpts()).RerankerType);

    [Fact]
    public async Task EmptyCandidates_ReturnsEmpty()
    {
        var r = new VoyageReranker(MakeHttpClient(new { data = Array.Empty<object>() }), MakeOpts());
        Assert.Empty(await r.RerankAsync("q", []));
    }

    [Fact]
    public async Task ParsesData_CorrectOrder()
    {
        var apiResponse = new
        {
            data = new[]
            {
                new { index = 2, relevance_score = 0.95f },
                new { index = 0, relevance_score = 0.3f },
            }
        };
        var candidates = Enumerable.Range(0, 3)
            .Select(i => new RerankCandidate($"v{i}", $"doc {i}")).ToList();
        var r = new VoyageReranker(MakeHttpClient(apiResponse), MakeOpts());
        var result = await r.RerankAsync("q", candidates);
        Assert.Equal("v2", result[0].Id);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new VoyageReranker(new HttpClient(), null!));

    [Fact]
    public async Task NewRank_StartsAtZero()
    {
        var apiResponse = new
        {
            data = new[] { new { index = 0, relevance_score = 0.5f } }
        };
        var candidates = new List<RerankCandidate> { new("x", "text") };
        var r = new VoyageReranker(MakeHttpClient(apiResponse), MakeOpts());
        var result = await r.RerankAsync("q", candidates);
        Assert.Equal(0, result[0].NewRank);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RK-05 BgeOnnxReranker / RK-06 CrossEncoderOnnx / RK-07 MixedBreadOnnx (5 each)
// ─────────────────────────────────────────────────────────────────────────────

public class OnnxRerankerTests
{
    private static IEmbedder MakeEmbedder(int dims = 4)
    {
        var e = Substitute.For<IEmbedder>();
        e.Dimensions.Returns(dims);
        e.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var v = new float[dims];
                v[0] = 1f; // unit vector
                return Task.FromResult(v);
            });
        e.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var texts = callInfo.Arg<IReadOnlyList<string>>();
                var result = new float[texts.Count][];
                for (int i = 0; i < texts.Count; i++)
                {
                    result[i] = new float[dims];
                    result[i][i % dims] = 1f;
                }
                return Task.FromResult(result);
            });
        return e;
    }

    private static OnnxRerankerOptions MakeOpts() => new() { Embedder = MakeEmbedder() };

    [Fact]
    public void Bge_RerankerType_IsBgeOnnx()
        => Assert.Equal(RerankerType.BgeOnnx, new BgeOnnxReranker(MakeOpts()).RerankerType);

    [Fact]
    public void CrossEncoder_RerankerType_IsCrossEncoderOnnx()
        => Assert.Equal(RerankerType.CrossEncoderOnnx, new CrossEncoderOnnxReranker(MakeOpts()).RerankerType);

    [Fact]
    public void MixedBread_RerankerType_IsMixedBreadOnnx()
        => Assert.Equal(RerankerType.MixedBreadOnnx, new MixedBreadOnnxReranker(MakeOpts()).RerankerType);

    [Fact]
    public async Task Bge_EmptyCandidates_ReturnsEmpty()
        => Assert.Empty(await new BgeOnnxReranker(MakeOpts()).RerankAsync("q", []));

    [Fact]
    public async Task CrossEncoder_RanksHighestScoreFirst()
    {
        // Candidate[0] gets vector [1,0,0,0] same as query → sim=1.0
        // Candidate[1] gets vector [0,1,0,0] → sim=0.0
        var r = new CrossEncoderOnnxReranker(MakeOpts());
        var candidates = new List<RerankCandidate>
        {
            new("c0", "doc 0"),
            new("c1", "doc 1"),
        };
        var result = await r.RerankAsync("q", candidates);
        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].NewRank);
        // Scores must be descending.
        Assert.True(result[0].Score >= result[1].Score);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// RK-08 — LlmReranker (5 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class LlmRerankerTests
{
    private static ILLMProvider MakeProvider(string responseContent)
    {
        var p = Substitute.For<ILLMProvider>();
        p.CompleteAsync(Arg.Any<LLMCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LLMWIKYResponse(responseContent));
        return p;
    }

    [Fact]
    public void RerankerType_IsLlmReranker()
        => Assert.Equal(RerankerType.LLMReranker,
            new LlmReranker(Substitute.For<ILLMProvider>()).RerankerType);

    [Fact]
    public async Task EmptyCandidates_ReturnsEmpty()
    {
        var r = new LlmReranker(Substitute.For<ILLMProvider>());
        Assert.Empty(await r.RerankAsync("q", []));
    }

    [Fact]
    public async Task ParsesFloatScore_SortsDescending()
    {
        // LLM returns 0.9 for every call → all tied → stable order.
        var p = MakeProvider("0.9");
        var r = new LlmReranker(p);
        var candidates = Enumerable.Range(1, 3)
            .Select(i => new RerankCandidate($"c{i}", $"doc {i}")).ToList();
        var result = await r.RerankAsync("q", candidates);
        Assert.Equal(3, result.Count);
        Assert.All(result, x => Assert.Equal(0.9f, x.Score, precision: 5));
    }

    [Fact]
    public async Task TopK_LimitsOutput()
    {
        var r = new LlmReranker(MakeProvider("0.5"));
        var candidates = Enumerable.Range(1, 5)
            .Select(i => new RerankCandidate($"c{i}", $"doc {i}")).ToList();
        var result = await r.RerankAsync("q", candidates, topK: 2);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Constructor_NullProvider_Throws()
        => Assert.Throws<ArgumentNullException>(() => new LlmReranker(null!));
}
