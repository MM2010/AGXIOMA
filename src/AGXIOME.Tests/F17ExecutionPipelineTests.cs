using AGXIOME.Core.Execution;
using AGXIOME.Core.LLM;
using AGXIOME.Core.Tools;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using NSubstitute;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F17 — AgentRunPipeline + ToolExecutionPipeline Integration Tests
//
// Coverage matrix:
//   ARP-01..ARP-10  AgentRunPipeline (10 tests)
//   TEP-01..TEP-10  ToolExecutionPipeline (10 tests)
//
// All tests use NSubstitute fakes or in-process real implementations.
// No network calls, no filesystem access.
// ─────────────────────────────────────────────────────────────────────────────

// ═════════════════════════════════════════════════════════════════════════════
// Helpers shared by both suites
// ═════════════════════════════════════════════════════════════════════════════

file static class Fake
{
    public static IAgxiomeAgent Agent(
        string response = "ok",
        AgentStatus status = AgentStatus.Completed)
    {
        var a = Substitute.For<IAgxiomeAgent>();
        a.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentResponse(response, status)));
        a.RunStreamAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(AsyncChunks("hello", " ", "world"));
        return a;
    }

    private static async IAsyncEnumerable<AgentChunk> AsyncChunks(
        params string[] parts)
    {
        foreach (var p in parts)
        {
            yield return new AgentChunk(p, IsFinal: false);
            await Task.Yield();
        }
    }

    /// <summary>Minimal executable tool that always succeeds.</summary>
    public static IAgxiomeExecutableTool ExecutableTool(
        string name = "echo",
        string category = "test",
        object? data = null,
        bool fail = false)
    {
        var t = Substitute.For<IAgxiomeExecutableTool>();
        t.Name.Returns(name);
        t.Description.Returns(name);
        t.Categories.Returns(new[] { category });
        t.Version.Returns((string?)null);
        t.ToJsonSchema().Returns(new JsonObject { ["name"] = name });
        t.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                fail
                    ? new ToolResult(false, null, "forced failure")
                    : new ToolResult(true, data ?? "echo-result")));
        return t;
    }

    public static ToolContext Context(string runId = "run-1", string agentId = "agent-1")
        => new(agentId, runId, null, null, null, null, null);

    public static AgentRequest Req(string prompt = "hi") => new(prompt);
}

// ═════════════════════════════════════════════════════════════════════════════
// ARP — AgentRunPipeline (10 tests)
// ═════════════════════════════════════════════════════════════════════════════

public class AgentRunPipelineTests
{
    // ── ARP-01 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP01_RunAsync_Bare_ReturnsAgentResponse()
    {
        var agent    = Fake.Agent("hello");
        var pipeline = new AgentRunPipeline(agent);

        var result = await pipeline.RunAsync(Fake.Req("hi"));

        Assert.Equal("hello", result.Content);
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    // ── ARP-02 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP02_RunAsync_RegistersAndRemovesRunId_InCancellationManager()
    {
        var agent    = Fake.Agent();
        var manager  = new CancellationManager();
        var pipeline = new AgentRunPipeline(agent,
            options: new AgentRunPipelineOptions { EnableCancellationTracking = true },
            cancellationManager: manager);

        await pipeline.RunAsync(Fake.Req(), runId: "my-run");

        // After completion the run must have been cleaned up
        Assert.False(manager.IsActive("my-run"));
    }

    // ── ARP-03 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP03_RunAsync_WithTokenCounter_DoesNotThrow()
    {
        var agent    = Fake.Agent("big response from llm");
        var counter  = new TokenCounter();
        var pipeline = new AgentRunPipeline(agent,
            options: new AgentRunPipelineOptions
            {
                EnableTokenCounting = true,
                TokenCounterProvider = LLMProviderType.DeepSeek
            },
            tokenCounter: counter);

        // Must complete without exceptions
        var result = await pipeline.RunAsync(Fake.Req("count my tokens please"));
        Assert.Equal(AgentStatus.Completed, result.Status);
    }

    // ── ARP-04 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP04_RunAsync_WithCache_SecondCallReturnsCachedValue()
    {
        int callCount = 0;
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return Task.FromResult(new AgentResponse("cached-response", AgentStatus.Completed));
            });

        var cache = new ResponseCache(new ResponseCacheOptions { Strategy = CacheStrategy.InMemory, MaxEntries = 256, DefaultTtl = TimeSpan.FromMinutes(5) });
        var pipeline = new AgentRunPipeline(agent,
            options: new AgentRunPipelineOptions
            {
                EnableResponseCache = true,
                EnableCancellationTracking = false,
                EnableTokenCounting = false
            },
            responseCache: cache);

        var req = Fake.Req("same prompt");
        await pipeline.RunAsync(req);
        var second = await pipeline.RunAsync(req);

        Assert.Equal(1, callCount);                          // agent called once only
        Assert.Equal("cached-response", second.Content);
        Assert.Equal(AgentStatus.Completed, second.Status);
    }

    // ── ARP-05 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP05_RunAsync_CustomRunIdFactory_UsedForRunId()
    {
        var agent   = Fake.Agent();
        var manager = new CancellationManager();
        string? capturedId = null;

        var pipeline = new AgentRunPipeline(agent,
            options: new AgentRunPipelineOptions
            {
                EnableCancellationTracking = true,
                RunIdFactory = () => { capturedId = "fixed-id"; return capturedId; }
            },
            cancellationManager: manager);

        await pipeline.RunAsync(Fake.Req());

        Assert.Equal("fixed-id", capturedId);
    }

    // ── ARP-06 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP06_RunStreamAsync_ForwardsAllChunks()
    {
        var agent    = Fake.Agent();
        var pipeline = new AgentRunPipeline(agent,
            options: new AgentRunPipelineOptions { EnableCancellationTracking = false });

        var chunks = new List<string>();
        await foreach (var chunk in pipeline.RunStreamAsync(Fake.Req()))
            chunks.Add(chunk.Content);

        Assert.Equal(["hello", " ", "world"], chunks);
    }

    // ── ARP-07 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ARP07_RunStreamAsync_CancellationManager_CleanedUpAfterStream()
    {
        var agent    = Fake.Agent();
        var manager  = new CancellationManager();
        var pipeline = new AgentRunPipeline(agent,
            options: new AgentRunPipelineOptions { EnableCancellationTracking = true },
            cancellationManager: manager);

        await foreach (var _ in pipeline.RunStreamAsync(Fake.Req(), runId: "stream-run")) { }

        Assert.False(manager.IsActive("stream-run"));
    }

    // ── ARP-08 ────────────────────────────────────────────────────────────────
    [Fact]
    public void ARP08_RunBackground_EnqueuesJobAndReturnsRunId()
    {
        var agent   = Fake.Agent();
        var worker  = new BackgroundRunWorker(agent);
        var pipeline = new AgentRunPipeline(agent,
            backgroundWorker: worker);

        var jobId = pipeline.RunBackground(Fake.Req("bg task"), runId: "bg-001");

        Assert.Equal("bg-001", jobId);
        var state = worker.GetState("bg-001");
        Assert.NotNull(state);
    }

    // ── ARP-09 ────────────────────────────────────────────────────────────────
    [Fact]
    public void ARP09_RunBackground_NoWorker_Throws()
    {
        var agent    = Fake.Agent();
        var pipeline = new AgentRunPipeline(agent); // no backgroundWorker

        Assert.Throws<InvalidOperationException>(() =>
            pipeline.RunBackground(Fake.Req("bg task")));
    }

    // ── ARP-10 ────────────────────────────────────────────────────────────────
    [Fact]
    public void ARP10_CancelRun_ViaCancellationManager_ReturnsTrue()
    {
        var agent   = Fake.Agent();
        var manager = new CancellationManager();
        manager.Register("active-run"); // manually register
        var pipeline = new AgentRunPipeline(agent, cancellationManager: manager);

        var cancelled = pipeline.CancelRun("active-run");

        Assert.True(cancelled);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TEP — ToolExecutionPipeline (10 tests)
// ═════════════════════════════════════════════════════════════════════════════

public class ToolExecutionPipelineTests
{
    private static ToolRegistry MakeRegistry(params IAgxiomeTool[] tools)
    {
        var reg = new ToolRegistry();
        foreach (var t in tools) reg.Register(t);
        return reg;
    }

    // ── TEP-01 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP01_ExecuteAsync_UnknownTool_ReturnsFailureResult()
    {
        var reg      = MakeRegistry();
        var pipeline = new ToolExecutionPipeline(reg);

        var result = await pipeline.ExecuteAsync("ghost-tool", Fake.Context());

        Assert.False(result.Success);
        Assert.Contains("not registered", result.ErrorMessage);
    }

    // ── TEP-02 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP02_ExecuteAsync_NonExecutableTool_ReturnsFailureResult()
    {
        // IAgxiomeTool that is NOT IAgxiomeExecutableTool
        var tool = Substitute.For<IAgxiomeTool>();
        tool.Name.Returns("meta-tool");
        tool.Description.Returns("metadata only");
        tool.Categories.Returns(Array.Empty<string>());

        var reg      = MakeRegistry(tool);
        var pipeline = new ToolExecutionPipeline(reg);

        var result = await pipeline.ExecuteAsync("meta-tool", Fake.Context());

        Assert.False(result.Success);
        Assert.Contains("not executable", result.ErrorMessage);
    }

    // ── TEP-03 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP03_ExecuteAsync_FilterBlocksTool_ReturnsFailureResult()
    {
        var tool   = Fake.ExecutableTool("blocked-tool");
        var reg    = MakeRegistry(tool);
        var filter = new ToolFilter();
        filter.SetFilter(new ToolFilterRule(
            "run-1",
            ToolFilterMode.Allowlist,
            new HashSet<string>(["other-tool"]))); // "blocked-tool" not in allowlist

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions { EnableFilter = true },
            filter: filter);

        var result = await pipeline.ExecuteAsync("blocked-tool", Fake.Context("run-1"));

        Assert.False(result.Success);
        Assert.Contains("blocked", result.ErrorMessage);
    }

    // ── TEP-04 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP04_ExecuteAsync_LimitExceeded_ReturnsFailureResult()
    {
        var tool    = Fake.ExecutableTool("limited-tool");
        var reg     = MakeRegistry(tool);
        var limiter = new ToolCallLimiter(new ToolCallLimits(MaxTotalCalls: 1));

        // Exhaust the limit
        limiter.RecordSuccess("run-1", "limited-tool");

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions { EnableCallLimits = true },
            limiter: limiter);

        var result = await pipeline.ExecuteAsync("limited-tool", Fake.Context("run-1"));

        Assert.False(result.Success);
        Assert.Contains("blocked", result.ErrorMessage);
    }

    // ── TEP-05 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP05_ExecuteAsync_CacheHit_ReturnsWithoutCallingTool()
    {
        int execCount = 0;
        var tool = Substitute.For<IAgxiomeExecutableTool>();
        tool.Name.Returns("cached-tool");
        tool.Description.Returns("cached");
        tool.Categories.Returns(Array.Empty<string>());
        tool.ToJsonSchema().Returns(new JsonObject());
        tool.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => { execCount++; return Task.FromResult(new ToolResult(true, "from-tool")); });

        var reg   = MakeRegistry(tool);
        var cache = new ToolResultCache(defaultTtl: TimeSpan.FromMinutes(5));

        // Pre-populate cache
        var ctx = Fake.Context();
        await cache.SetAsync("cached-tool", ctx.Parameters, new ToolResult(true, "from-cache"));

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions { EnableResultCache = true },
            cache: cache);

        var result = await pipeline.ExecuteAsync("cached-tool", ctx);

        Assert.Equal(0, execCount);
        Assert.Equal("from-cache", result.Data?.ToString());
    }

    // ── TEP-06 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP06_ExecuteAsync_Success_RecordsMetrics()
    {
        var tool    = Fake.ExecutableTool("metered-tool");
        var reg     = MakeRegistry(tool);
        var metrics = new ToolMetricsCollector();

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions { EnableMetrics = true },
            metrics: metrics);

        await pipeline.ExecuteAsync("metered-tool", Fake.Context());

        var snap = metrics.GetSnapshot("metered-tool");
        Assert.NotNull(snap);
        Assert.Equal(1, snap.TotalCalls);
        Assert.Equal(1, snap.SuccessCount);
        Assert.Equal(0, snap.ErrorCount);
    }

    // ── TEP-07 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP07_ExecuteAsync_ToolFails_RecordsErrorMetrics()
    {
        var tool    = Fake.ExecutableTool("fail-tool", fail: true);
        var reg     = MakeRegistry(tool);
        var metrics = new ToolMetricsCollector();

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions { EnableMetrics = true },
            metrics: metrics);

        var result = await pipeline.ExecuteAsync("fail-tool", Fake.Context());

        Assert.False(result.Success);
        var snap = metrics.GetSnapshot("fail-tool");
        Assert.NotNull(snap);
        Assert.Equal(1, snap.ErrorCount);
        Assert.Equal(0.0, snap.SuccessCount / (double)snap.TotalCalls, precision: 5);
    }

    // ── TEP-08 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP08_ExecuteAsync_ThrowingTool_ReturnsFailureWithoutPropagating()
    {
        var tool = Substitute.For<IAgxiomeExecutableTool>();
        tool.Name.Returns("bomb");
        tool.Description.Returns("throws");
        tool.Categories.Returns(Array.Empty<string>());
        tool.ToJsonSchema().Returns(new JsonObject());
        tool.ExecuteAsync(Arg.Any<ToolContext>(), Arg.Any<CancellationToken>())
            .Returns<Task<ToolResult>>(_ => throw new InvalidOperationException("BOOM"));

        var reg      = MakeRegistry(tool);
        var pipeline = new ToolExecutionPipeline(reg);

        var result = await pipeline.ExecuteAsync("bomb", Fake.Context());

        Assert.False(result.Success);
        Assert.Equal("BOOM", result.ErrorMessage);
    }

    // ── TEP-09 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP09_ExecuteAsync_Success_StoredInCache()
    {
        var tool  = Fake.ExecutableTool("storable", data: "fresh-value");
        var reg   = MakeRegistry(tool);
        var cache   = new ToolResultCache(defaultTtl: TimeSpan.FromMinutes(5));

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions { EnableResultCache = true },
            cache: cache);

        var ctx = Fake.Context();
        await pipeline.ExecuteAsync("storable", ctx);

        // Cache should now contain the result
        var cached = await cache.TryGetAsync("storable", ctx.Parameters);
        Assert.NotNull(cached);
        Assert.True(cached!.Success);
    }

    // ── TEP-10 ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TEP10_ExecuteAsync_AllMiddleware_FullHappyPath()
    {
        var tool    = Fake.ExecutableTool("full-tool", data: "result");
        var reg     = MakeRegistry(tool);
        var filter  = new ToolFilter();
        var limiter = new ToolCallLimiter(new ToolCallLimits(MaxTotalCalls: 10));
        var cache   = new ToolResultCache(defaultTtl: TimeSpan.FromMinutes(1));
        var metrics = new ToolMetricsCollector();

        var pipeline = new ToolExecutionPipeline(
            reg,
            options: new ToolExecutionPipelineOptions
            {
                EnableFilter     = true,
                EnableCallLimits = true,
                EnableResultCache = true,
                EnableMetrics    = true
            },
            filter:  filter,
            limiter: limiter,
            cache:   cache,
            metrics: metrics);

        var ctx    = Fake.Context("run-x");
        var result = await pipeline.ExecuteAsync("full-tool", ctx);

        Assert.True(result.Success);
        // Limiter recorded a success
        var stats = limiter.GetStats("run-x");
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalCalls);
        // Metrics recorded
        Assert.Equal(1, metrics.GetSnapshot("full-tool")!.SuccessCount);
        // Cache populated
        Assert.NotNull(await cache.TryGetAsync("full-tool", ctx.Parameters));
    }
}
