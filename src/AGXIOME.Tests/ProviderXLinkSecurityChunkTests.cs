using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.LlmWiky;
using AGXIOME.LlmWiky.Providers;
using AGXIOME.Storage.Chunkers;
using AGXIOME.XLink;
using AGXIOME.Security;
using Xunit;

namespace AGXIOME.Tests;

public class ProviderXLinkSecurityChunkTests
{
    // ── Cost Tracker ──

    [Fact]
    public void CostTracker_KnownProvider_ReturnsCost()
    {
        var tracker = new ProviderCostTracker();
        var cost = tracker.Estimate("OpenAI", "gpt-4o", 1000, 500);
        Assert.True(cost > 0);
        Assert.True(cost < 1m); // less than $1 for these tokens
    }

    [Fact]
    public void CostTracker_WildcardProvider_ReturnsCost()
    {
        var tracker = new ProviderCostTracker();
        var cost = tracker.Estimate("Ollama", "llama3", 10000, 5000);
        Assert.Equal(0m, cost);
    }

    [Fact]
    public void CostTracker_UnknownProvider_Zero()
    {
        var tracker = new ProviderCostTracker();
        Assert.Equal(0m, tracker.Estimate("FakeProvider", "fake-model", 100, 100));
    }

    // ── X-Link ──

    [Fact]
    public async Task XLink_SendSubscribe_Roundtrip()
    {
        var router = new XLinkRouter();
        string? received = null;

        router.Subscribe<string>("topic", msg => { received = msg; return Task.CompletedTask; });
        await router.SendAsync("topic", "hello");

        Assert.Equal("hello", received);
    }

    [Fact]
    public async Task XLink_SendNoSubscribers_NoError()
    {
        var router = new XLinkRouter();
        await router.SendAsync("nowhere", "data"); // should not throw
    }

    [Fact]
    public async Task XLink_MultipleSubscribers_AllReceive()
    {
        var router = new XLinkRouter();
        int count = 0;

        router.Subscribe<string>("topic", _ => { count++; return Task.CompletedTask; });
        router.Subscribe<string>("topic", _ => { count++; return Task.CompletedTask; });
        await router.SendAsync("topic", "msg");

        Assert.Equal(2, count);
    }

    // ── Security ──

    [Fact]
    public void Crypto_SignVerify_WithSameKey_Succeeds()
    {
        var crypto = new CryptoProvider();
        var (priv, pub) = crypto.GenerateKeyPair();
        var data = "important message"u8.ToArray();

        var sig = crypto.Sign(data, priv);
        var valid = crypto.Verify(data, sig, pub);

        Assert.True(valid);
    }

    [Fact]
    public void Crypto_SignVerify_WrongKey_Fails()
    {
        var crypto = new CryptoProvider();
        var (_, pub1) = crypto.GenerateKeyPair();
        var (priv2, _) = crypto.GenerateKeyPair();
        var data = "test"u8.ToArray();

        var sig = crypto.Sign(data, priv2);
        var valid = crypto.Verify(data, sig, pub1);

        Assert.False(valid);
    }

    [Fact]
    public void Crypto_HMAC_SameData_SameHash()
    {
        var crypto = new CryptoProvider();
        var key = "secret"u8.ToArray();
        var data = "data"u8.ToArray();

        var h1 = crypto.ComputeHMAC(data, key);
        var h2 = crypto.ComputeHMAC(data, key);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Crypto_HMAC_TamperedData_DifferentHash()
    {
        var crypto = new CryptoProvider();
        var key = "secret"u8.ToArray();

        var h1 = crypto.ComputeHMAC("original"u8.ToArray(), key);
        var h2 = crypto.ComputeHMAC("tampered"u8.ToArray(), key);

        Assert.NotEqual(h1, h2);
    }

    // ── Audit ──

    [Fact]
    public async Task AuditLogger_WriteAndQuery()
    {
        var logger = new AuditLogger();
        var evt = new AuditEvent("1", "agent.run", "agent-1", null, "EXECUTE",
            null, null, true, null, DateTime.UtcNow);

        await logger.LogAsync(evt);
        var results = await logger.QueryAsync(new AuditQuery(EventType: "agent.run"));

        Assert.Single(results);
    }

    // ── Chunkers ──

    [Fact]
    public void FixedSizeChunker_SmallerThanChunk_OneChunk()
    {
        var chunker = new FixedSizeChunker();
        var chunks = chunker.Chunk("hello world", 1000, 0);
        Assert.Single(chunks);
    }

    [Fact]
    public void FixedSizeChunker_LargerThanChunk_MultipleChunks()
    {
        var chunker = new FixedSizeChunker();
        var text = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"word{i}"));
        var chunks = chunker.Chunk(text, 3, 1);
        Assert.True(chunks.Count > 1, $"Expected multiple chunks, got {chunks.Count}");
    }

    [Fact]
    public void HeadingChunker_Markdown_SplitsAtHeadings()
    {
        var chunker = new HeadingChunker();
        var md = "# Header1\ncontent here\n## Header2\nmore content\n### Header3\nfinal";

        var chunks = chunker.Chunk(md);

        Assert.True(chunks.Count >= 2);
    }

    [Fact]
    public void HeadingChunker_PlainText_FallsBack()
    {
        var chunker = new HeadingChunker();
        var chunks = chunker.Chunk("just some text without headings");
        Assert.NotEmpty(chunks);
    }

    [Fact]
    public void SentenceChunker_SplitsAtSentenceBoundaries()
    {
        var chunker = new SentenceChunker();
        var text = "First sentence. Second sentence. Third sentence. Fourth sentence.";

        var chunks = chunker.Chunk(text, 5);

        Assert.True(chunks.Count >= 2);
    }
}
