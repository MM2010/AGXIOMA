using AGXIOME.XLink.Bridges;
using Xunit;

namespace AGXIOME.Tests.F21;

// ══════════════════════════════════════════════════════════════════════════════
// TS02  XLink Bridges — contract + offline behaviour
// ══════════════════════════════════════════════════════════════════════════════

[Trait("Phase", "F21")]
public class F21_TS02_Bridges_HappyPath
{
    // ── AgentBridgeResult record ──

    [Fact]
    public void AgentBridgeResult_Properties_Accessible()
    {
        var r = new AgentBridgeResult(
            Success:       true,
            Content:       "hello",
            Error:         null,
            FrameworkName: "Claude",
            LatencyMs:     42);

        Assert.True(r.Success);
        Assert.Equal("hello",  r.Content);
        Assert.Equal("Claude", r.FrameworkName);
        Assert.Equal(42,       r.LatencyMs);
        Assert.Null(r.Error);
    }

    [Fact]
    public void AgentBridgeResult_FailureResult_HasError()
    {
        var r = new AgentBridgeResult(false, null, "bad request", "LangChain");

        Assert.False(r.Success);
        Assert.Equal("bad request", r.Error);
        Assert.Null(r.Content);
    }

    // ── ClaudeBridge (offline: no ANTHROPIC_API_KEY in env) ──

    [Fact]
    public void ClaudeBridge_NoApiKey_IsNotAvailable()
    {
        var saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            // Ensure no key in env for this test
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            var bridge = new ClaudeBridge(apiKey: null);

            Assert.False(bridge.IsAvailable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    [Fact]
    public void ClaudeBridge_SupportedModels_NonEmpty()
    {
        var bridge = new ClaudeBridge();
        Assert.NotEmpty(bridge.SupportedModels);
        Assert.Contains(bridge.SupportedModels, m => m.StartsWith("claude-"));
    }

    [Fact]
    public void ClaudeBridge_DefaultModels_ContainHaiku()
    {
        Assert.Contains("claude-3-haiku-20240307", ClaudeBridge.DefaultModels);
    }

    [Fact]
    public async Task ClaudeBridge_NoApiKey_CompleteAsync_ReturnsFailure()
    {
        var saved = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
            var bridge = new ClaudeBridge(apiKey: null);
            var result = await bridge.CompleteAsync("hello");

            Assert.False(result.Success);
            Assert.NotNull(result.Error);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", saved);
        }
    }

    // ── SemanticKernelBridge (no adapter → unavailable) ──

    [Fact]
    public void SemanticKernelBridge_NoAdapter_IsNotAvailable()
    {
        var bridge = new SemanticKernelBridge(adapter: null);
        Assert.False(bridge.IsAvailable);
    }

    [Fact]
    public void SemanticKernelBridge_NoAdapter_ListPlugins_ReturnsEmpty()
    {
        var bridge = new SemanticKernelBridge(adapter: null);
        Assert.Empty(bridge.ListPlugins());
    }

    [Fact]
    public async Task SemanticKernelBridge_NoAdapter_InvokePluginAsync_ReturnsFailure()
    {
        var bridge = new SemanticKernelBridge(adapter: null);
        var result = await bridge.InvokePluginAsync("MyPlugin", "MyFunction",
            new Dictionary<string, string>());

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    // ── LangChainBridge (no endpoint → unavailable) ──

    [Fact]
    public void LangChainBridge_NoEndpoint_IsNotAvailable()
    {
        var saved = Environment.GetEnvironmentVariable("LANGCHAIN_ENDPOINT");
        try
        {
            Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", null);
            var bridge = new LangChainBridge(endpointUrl: null);
            Assert.False(bridge.IsAvailable);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", saved);
        }
    }

    [Fact]
    public async Task LangChainBridge_NoEndpoint_RunChainAsync_ReturnsFailure()
    {
        var saved = Environment.GetEnvironmentVariable("LANGCHAIN_ENDPOINT");
        try
        {
            Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", null);
            var bridge = new LangChainBridge(endpointUrl: null);
            var result = await bridge.RunChainAsync("my-chain",
                new Dictionary<string, string> { ["input"] = "test" });

            Assert.False(result.Success);
        }
        finally
        {
            Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", saved);
        }
    }
}
