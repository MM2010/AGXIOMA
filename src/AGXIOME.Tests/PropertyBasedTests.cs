using AGXIOME.Shared.Models;
using AGXIOME.Core.Agent;
using AGXIOME.LLMWIKY;
using System.Text;
using Xunit;

namespace AGXIOME.Tests;

/// <summary>Property-based tests using manual value generation.</summary>
public class PropertyBasedTests
{
    [Fact]
    public void XLink_EnvelopePayloadRoundtrip_RandomPayload()
    {
        var rng = new Random(42);
        var payload = new byte[100];
        rng.NextBytes(payload);

        var header = new XLinkHeader("1.0", Guid.NewGuid().ToString(), "test",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var envelope = new XLinkEnvelope(header, "checksum", payload);

        Assert.Equal(payload, envelope.Payload);
    }

    [Fact]
    public void XLink_Envelope_EmptyPayload()
    {
        var payload = Array.Empty<byte>();
        var header = new XLinkHeader("1.0", "msg-1", "src", 0);
        var envelope = new XLinkEnvelope(header, "c", payload);
        Assert.Empty(envelope.Payload);
    }

    [Fact]
    public void XLink_Envelope_LargePayload_Roundtrip()
    {
        var payload = new byte[10 * 1024 * 1024];
        new Random(123).NextBytes(payload);
        var header = new XLinkHeader("1.0", Guid.NewGuid().ToString(), "test", 0);
        var envelope = new XLinkEnvelope(header, "cs", payload);
        Assert.Equal(payload.Length, envelope.Payload.Length);
        Assert.Equal(payload, envelope.Payload);
    }

    [Fact]
    public void HMAC_Deterministic_SameDataSameHash()
    {
        var key = "fixed-key"u8.ToArray();
        var crypto = new AGXIOME.Security.CryptoProvider();
        var h1 = crypto.ComputeHMAC(Encoding.UTF8.GetBytes("data"), key);
        var h2 = crypto.ComputeHMAC(Encoding.UTF8.GetBytes("data"), key);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void HMAC_DifferentData_DifferentHash()
    {
        var key = "fixed-key"u8.ToArray();
        var crypto = new AGXIOME.Security.CryptoProvider();
        Assert.NotEqual(
            crypto.ComputeHMAC(Encoding.UTF8.GetBytes("a"), key),
            crypto.ComputeHMAC(Encoding.UTF8.GetBytes("b"), key));
    }

    [Fact]
    public void SessionState_SetGet_IntStringDouble()
    {
        var state = new SessionState();
        state.Set("i", 42); state.Set("s", "hello"); state.Set("d", 3.14);
        Assert.Equal(42, state.Get<int>("i"));
        Assert.Equal("hello", state.Get<string>("s"));
        Assert.Equal(3.14, state.Get<double>("d"), 4);
    }

    [Fact]
    public void SessionState_Overwrite()
    {
        var state = new SessionState();
        state.Set("k", 1); state.Set("k", 2);
        Assert.Equal(2, state.Get<int>("k"));
    }

    [Fact]
    public void CostTracker_AllProviders_NonNegative()
    {
        var t = new ProviderCostTracker();
        Assert.True(t.Estimate("OpenAI", "gpt-4o", 1000, 500) > 0);
        Assert.Equal(0m, t.Estimate("Ollama", "llama3", 10000, 5000));
        Assert.Equal(0m, t.Estimate("Unknown", "x", 1, 1));
    }

    [Fact]
    public void FixedSizeChunker_RandomText_NoNullChunks()
    {
        var rng = new Random(99);
        var sb = new StringBuilder();
        for (int i = 0; i < 5000; i++) sb.Append((char)('a' + rng.Next(26)));
        var chunks = new Storage.Chunkers.FixedSizeChunker().Chunk(sb.ToString(), 50, 5);
        Assert.NotEmpty(chunks);
        Assert.All(chunks, c => Assert.NotNull(c.Content));
    }

    [Fact]
    public void Crypto_KeyPair_ValidForMultipleSizes()
    {
        var c = new AGXIOME.Security.CryptoProvider();
        var (priv, pub) = c.GenerateKeyPair();
        foreach (var size in new[] { 16, 100, 1024 })
        {
            var data = new byte[size]; new Random(size).NextBytes(data);
            Assert.True(c.Verify(data, c.Sign(data, priv), pub), $"Size {size}");
        }
    }
}
