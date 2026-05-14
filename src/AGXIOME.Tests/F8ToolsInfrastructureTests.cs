using System.Text.Json.Nodes;
using AGXIOME.Core.Tools;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F8 — Tools Infrastructure Tests (TI-01..TI-06)
// Strategy: 7 tests per task = 42 total.
// ─────────────────────────────────────────────────────────────────────────────

// ── TI-01 — ToolResultCache ───────────────────────────────────────────────────

public class ToolResultCacheTests : IDisposable
{
    private readonly ToolResultCache _cache = new(defaultTtl: TimeSpan.FromMinutes(5));

    [Fact]
    public async Task SetThenGet_ReturnsCachedResult()
    {
        var result = new ToolResult(true, "data");
        await _cache.SetAsync("tool_a", null, result);
        var hit = await _cache.TryGetAsync("tool_a", null);
        Assert.NotNull(hit);
        Assert.True(hit!.Success);
    }

    [Fact]
    public async Task Miss_ReturnsNull()
    {
        var hit = await _cache.TryGetAsync("nonexistent_tool", null);
        Assert.Null(hit);
    }

    [Fact]
    public async Task ExpiredEntry_ReturnsNull()
    {
        var result = new ToolResult(true, "ephemeral");
        await _cache.SetAsync("tool_b", null, result, ttl: TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        var hit = await _cache.TryGetAsync("tool_b", null);
        Assert.Null(hit);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesEntry()
    {
        await _cache.SetAsync("tool_c", null, new ToolResult(true, "x"));
        await _cache.InvalidateAsync("tool_c", null);
        var hit = await _cache.TryGetAsync("tool_c", null);
        Assert.Null(hit);
    }

    [Fact]
    public async Task InvalidateAll_ClearsCache()
    {
        await _cache.SetAsync("t1", null, new ToolResult(true, "a"));
        await _cache.SetAsync("t2", null, new ToolResult(true, "b"));
        await _cache.InvalidateAllAsync();
        var stats = await _cache.GetStatsAsync();
        Assert.Equal(0, stats.EntryCount);
    }

    [Fact]
    public async Task Stats_TrackHitMiss()
    {
        await _cache.SetAsync("tool_d", null, new ToolResult(true, "v"));
        await _cache.TryGetAsync("tool_d", null);  // hit
        await _cache.TryGetAsync("miss_tool", null); // miss
        var stats = await _cache.GetStatsAsync();
        Assert.Equal(1, stats.Hits);
        Assert.True(stats.Misses >= 1);
    }

    [Fact]
    public async Task ParameterVariants_UseDifferentKeys()
    {
        var p1 = new Dictionary<string, object> { ["q"] = "hello" };
        var p2 = new Dictionary<string, object> { ["q"] = "world" };
        await _cache.SetAsync("tool_e", p1, new ToolResult(true, "r1"));
        await _cache.SetAsync("tool_e", p2, new ToolResult(true, "r2"));
        var h1 = await _cache.TryGetAsync("tool_e", p1);
        var h2 = await _cache.TryGetAsync("tool_e", p2);
        Assert.Equal("r1", h1!.Data);
        Assert.Equal("r2", h2!.Data);
    }

    public void Dispose() => _cache.Dispose();
}

// ── TI-02 — ToolCallLimiter ───────────────────────────────────────────────────

public class ToolCallLimiterTests
{
    private readonly ToolCallLimiter _limiter = new(new ToolCallLimits(
        MaxTotalCalls: 10,
        MaxConsecutiveErrors: 3,
        MaxCallsPerTool: 5));

    [Fact]
    public void InitialCheck_IsAllowed()
    {
        var result = _limiter.Check("run-1", "my_tool");
        Assert.True(result.Allowed);
        Assert.Equal(ToolCallLimitViolation.None, result.Violation);
    }

    [Fact]
    public void AfterRecordSuccess_CountersUpdate()
    {
        _limiter.RecordSuccess("run-2", "tool_a");
        var stats = _limiter.GetStats("run-2");
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalCalls);
        Assert.Equal(0, stats.ConsecutiveErrors);
    }

    [Fact]
    public void AfterRecordError_ConsecutiveErrorsIncrease()
    {
        _limiter.RecordError("run-3", "tool_a");
        _limiter.RecordError("run-3", "tool_a");
        var stats = _limiter.GetStats("run-3");
        Assert.Equal(2, stats!.ConsecutiveErrors);
    }

    [Fact]
    public void SuccessAfterErrors_ResetsConsecutiveErrors()
    {
        _limiter.RecordError("run-4", "tool_a");
        _limiter.RecordError("run-4", "tool_a");
        _limiter.RecordSuccess("run-4", "tool_a");
        var stats = _limiter.GetStats("run-4");
        Assert.Equal(0, stats!.ConsecutiveErrors);
    }

    [Fact]
    public void TotalCallsExceeded_BlocksFurtherCalls()
    {
        for (int i = 0; i < 10; i++) _limiter.RecordSuccess("run-5", "tool_a");
        var check = _limiter.Check("run-5", "tool_a");
        Assert.False(check.Allowed);
        Assert.Equal(ToolCallLimitViolation.TotalCallsExceeded, check.Violation);
    }

    [Fact]
    public void ConsecutiveErrorsExceeded_BlocksFurtherCalls()
    {
        for (int i = 0; i < 3; i++) _limiter.RecordError("run-6", "tool_a");
        var check = _limiter.Check("run-6", "tool_a");
        Assert.False(check.Allowed);
        Assert.Equal(ToolCallLimitViolation.ConsecutiveErrorsExceeded, check.Violation);
    }

    [Fact]
    public void PerToolCallsExceeded_BlocksThatTool()
    {
        for (int i = 0; i < 5; i++) _limiter.RecordSuccess("run-7", "limited_tool");
        var check = _limiter.Check("run-7", "limited_tool");
        Assert.False(check.Allowed);
        Assert.Equal(ToolCallLimitViolation.PerToolCallsExceeded, check.Violation);
    }
}

// ── TI-03 — StopAfterToolEvaluator ───────────────────────────────────────────

public class StopAfterToolEvaluatorTests
{
    private readonly StopAfterToolEvaluator _eval = new();

    [Fact]
    public void NoRule_ContinuesRun()
    {
        var result = _eval.Evaluate("any_tool", success: true);
        Assert.False(result.ShouldStop);
    }

    [Fact]
    public void AfterAnyCall_StopsAlways()
    {
        _eval.AddRule(new StopAfterToolRule("submit", StopAfterToolMode.AfterAnyCall, "done"));
        Assert.True(_eval.Evaluate("submit", true).ShouldStop);
        Assert.True(_eval.Evaluate("submit", false).ShouldStop);
    }

    [Fact]
    public void AfterSuccess_StopsOnlyOnSuccess()
    {
        _eval.AddRule(new StopAfterToolRule("validate", StopAfterToolMode.AfterSuccess));
        Assert.True(_eval.Evaluate("validate", true).ShouldStop);
        Assert.False(_eval.Evaluate("validate", false).ShouldStop);
    }

    [Fact]
    public void AfterError_StopsOnlyOnError()
    {
        _eval.AddRule(new StopAfterToolRule("risky_op", StopAfterToolMode.AfterError));
        Assert.False(_eval.Evaluate("risky_op", true).ShouldStop);
        Assert.True(_eval.Evaluate("risky_op", false).ShouldStop);
    }

    [Fact]
    public void NeverMode_NeverStops()
    {
        _eval.AddRule(new StopAfterToolRule("noop", StopAfterToolMode.Never));
        Assert.False(_eval.Evaluate("noop", true).ShouldStop);
        Assert.False(_eval.Evaluate("noop", false).ShouldStop);
    }

    [Fact]
    public void RemoveRule_ClearsRule()
    {
        _eval.AddRule(new StopAfterToolRule("submit", StopAfterToolMode.AfterAnyCall));
        _eval.RemoveRule("submit");
        Assert.False(_eval.Evaluate("submit", true).ShouldStop);
    }

    [Fact]
    public void GetRules_ReturnsAllRegistered()
    {
        _eval.AddRule(new StopAfterToolRule("a", StopAfterToolMode.AfterSuccess));
        _eval.AddRule(new StopAfterToolRule("b", StopAfterToolMode.AfterError));
        var rules = _eval.GetRules();
        Assert.Equal(2, rules.Count);
    }
}

// ── TI-04 — ToolFilter ────────────────────────────────────────────────────────

public class ToolFilterTests
{
    private readonly ToolFilter _filter = new();

    [Fact]
    public void NoRule_AllToolsAllowed()
    {
        Assert.True(_filter.IsAllowed("session-1", "any_tool"));
    }

    [Fact]
    public void Allowlist_OnlyListedToolsPass()
    {
        _filter.SetFilter(new ToolFilterRule(
            "session-2",
            ToolFilterMode.Allowlist,
            new HashSet<string> { "allowed_tool" }));
        Assert.True(_filter.IsAllowed("session-2", "allowed_tool"));
        Assert.False(_filter.IsAllowed("session-2", "other_tool"));
    }

    [Fact]
    public void Blocklist_ListedToolsBlocked()
    {
        _filter.SetFilter(new ToolFilterRule(
            "session-3",
            ToolFilterMode.Blocklist,
            new HashSet<string> { "banned_tool" }));
        Assert.False(_filter.IsAllowed("session-3", "banned_tool"));
        Assert.True(_filter.IsAllowed("session-3", "ok_tool"));
    }

    [Fact]
    public void EmptyBlocklist_AllToolsAllowed()
    {
        _filter.SetFilter(new ToolFilterRule(
            "session-4",
            ToolFilterMode.Blocklist,
            new HashSet<string>()));
        Assert.True(_filter.IsAllowed("session-4", "any_tool"));
    }

    [Fact]
    public void ClearFilter_RevertsToAllowAll()
    {
        _filter.SetFilter(new ToolFilterRule(
            "session-5",
            ToolFilterMode.Allowlist,
            new HashSet<string> { "only_this" }));
        _filter.ClearFilter("session-5");
        Assert.True(_filter.IsAllowed("session-5", "any_tool"));
    }

    [Fact]
    public void GetFilter_ReturnsNullWhenAbsent()
    {
        Assert.Null(_filter.GetFilter("no-such-session"));
    }

    [Fact]
    public void GetAllFilters_ReturnsRegisteredRules()
    {
        _filter.SetFilter(new ToolFilterRule("s1", ToolFilterMode.Allowlist, new HashSet<string> { "t" }));
        _filter.SetFilter(new ToolFilterRule("s2", ToolFilterMode.Blocklist, new HashSet<string> { "u" }));
        Assert.Equal(2, _filter.GetAllFilters().Count);
    }
}

// ── TI-05 — CallableFactory ───────────────────────────────────────────────────

// Minimal stub tool for CallableFactory tests
internal sealed class StubExecutableTool : IAgxiomeExecutableTool
{
    public string Name => "stub_search";
    public string Description => "Search for things.";
    public string? Version => "1.0.0";
    public IReadOnlyList<string> Categories => Array.Empty<string>();

    public Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
        => Task.FromResult(new ToolResult(true, null));

    public JsonObject ToJsonSchema() => new()
    {
        ["name"]        = Name,
        ["description"] = Description,
        ["parameters"]  = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Search query" }
            },
            ["required"] = new JsonArray { "query" }
        }
    };
}

public class CallableFactoryTests
{
    private readonly CallableFactory _factory = new();
    private readonly StubExecutableTool _tool = new();

    [Fact]
    public void CreateCallable_OpenAi_HasCorrectStructure()
    {
        var def = _factory.CreateCallable(_tool, LlmCallableFormat.OpenAiFunction);
        Assert.Equal(LlmCallableFormat.OpenAiFunction, def.Format);
        Assert.Equal("function", def.Payload["type"]?.GetValue<string>());
        Assert.NotNull(def.Payload["function"]);
    }

    [Fact]
    public void CreateCallable_Anthropic_HasInputSchema()
    {
        var def = _factory.CreateCallable(_tool, LlmCallableFormat.AnthropicTool);
        Assert.NotNull(def.Payload["input_schema"]);
        Assert.Equal("stub_search", def.Payload["name"]?.GetValue<string>());
    }

    [Fact]
    public void CreateCallable_Gemini_HasParameters()
    {
        var def = _factory.CreateCallable(_tool, LlmCallableFormat.GeminiTool);
        Assert.NotNull(def.Payload["parameters"]);
        Assert.Equal("stub_search", def.Payload["name"]?.GetValue<string>());
    }

    [Fact]
    public void BuildToolsArray_ReturnsCorrectCount()
    {
        var arr = _factory.BuildToolsArray(new[] { _tool, _tool }, LlmCallableFormat.OpenAiFunction);
        // Both are the same tool; factory still emits one entry per input
        Assert.Equal(2, arr.Count);
    }

    [Fact]
    public void ParseToolCall_OpenAi_ParsesCorrectly()
    {
        var node = JsonNode.Parse("""
            {"id":"call_1","type":"function","function":{"name":"stub_search","arguments":"{\"query\":\"test\"}"}}
            """)!;
        var parsed = _factory.ParseToolCall(node, LlmCallableFormat.OpenAiFunction);
        Assert.NotNull(parsed);
        Assert.Equal("stub_search", parsed!.ToolName);
        Assert.Equal("call_1", parsed.ToolCallId);
    }

    [Fact]
    public void ParseToolCall_Anthropic_ParsesCorrectly()
    {
        var node = JsonNode.Parse("""
            {"type":"tool_use","id":"tu_1","name":"stub_search","input":{"query":"hello"}}
            """)!;
        var parsed = _factory.ParseToolCall(node, LlmCallableFormat.AnthropicTool);
        Assert.NotNull(parsed);
        Assert.Equal("stub_search", parsed!.ToolName);
    }

    [Fact]
    public void ParseToolCall_Gemini_ParsesCorrectly()
    {
        var node = JsonNode.Parse("""{"name":"stub_search","args":{"query":"gemini"}}""")!;
        var parsed = _factory.ParseToolCall(node, LlmCallableFormat.GeminiTool);
        Assert.NotNull(parsed);
        Assert.Equal("stub_search", parsed!.ToolName);
    }
}

// ── TI-06 — ToolMetricsCollector ─────────────────────────────────────────────

public class ToolMetricsCollectorTests
{
    private readonly ToolMetricsCollector _collector = new();

    [Fact]
    public void RecordSuccess_IncrementsSuccessCount()
    {
        _collector.Record("tool_x", success: true, latencyMs: 50.0);
        var snap = _collector.GetSnapshot("tool_x");
        Assert.NotNull(snap);
        Assert.Equal(1, snap!.SuccessCount);
        Assert.Equal(0, snap.ErrorCount);
    }

    [Fact]
    public void RecordError_IncrementsErrorCount()
    {
        _collector.Record("tool_y", success: false, latencyMs: 120.0);
        var snap = _collector.GetSnapshot("tool_y");
        Assert.Equal(1, snap!.ErrorCount);
        Assert.NotNull(snap.LastErrorAt);
    }

    [Fact]
    public void AvgLatency_IsCorrect()
    {
        _collector.Record("tool_z", true, 100.0);
        _collector.Record("tool_z", true, 200.0);
        var snap = _collector.GetSnapshot("tool_z");
        Assert.Equal(150.0, snap!.AvgLatencyMs, precision: 1);
    }

    [Fact]
    public void ErrorRate_IsCorrect()
    {
        _collector.Record("tool_w", true, 10.0);
        _collector.Record("tool_w", false, 10.0);
        var snap = _collector.GetSnapshot("tool_w");
        Assert.Equal(0.5, snap!.ErrorRate, precision: 2);
    }

    [Fact]
    public void P95_Returns_MinusOne_WhenFewSamples()
    {
        _collector.Record("tool_p", true, 10.0);
        var snap = _collector.GetSnapshot("tool_p");
        // Ring buffer requires ≥20 samples for p95
        Assert.Equal(-1.0, snap!.P95LatencyMs);
    }

    [Fact]
    public void GetAllSnapshots_IncludesAllRecordedTools()
    {
        _collector.Record("t_a", true, 1.0);
        _collector.Record("t_b", false, 2.0);
        var snaps = _collector.GetAllSnapshots();
        Assert.Contains(snaps, s => s.ToolName == "t_a");
        Assert.Contains(snaps, s => s.ToolName == "t_b");
    }

    [Fact]
    public void Reset_RemovesTool()
    {
        _collector.Record("tool_r", true, 10.0);
        _collector.Reset("tool_r");
        Assert.Null(_collector.GetSnapshot("tool_r"));
    }
}
