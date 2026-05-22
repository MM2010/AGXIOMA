// F15 — Toolkit Orchestration Intelligence Tests (CO-A8)
// Covers: CapabilityRegistry, CapabilityManifest, ToolRouter, BudgetAwareExecutor,
//         ToolkitPolicyEnforcer (all three provider flavours), SemanticToolSelector (stub embedder).
// Target: 40+ deterministic, no-network tests.

using AGXIOME.Core.Tools;
using AGXIOME.Toolkits;
using AGXIOME.Toolkits.DataAnalysis;
using AGXIOME.Toolkits.Orchestration;
using IEmbedder = AGXIOME.Shared.Interfaces.IEmbedder;
using EmbedderType = AGXIOME.Shared.Models.EmbedderType;

namespace AGXIOME.Tests.F15;

// ── Helpers ───────────────────────────────────────────────────────────────────

file sealed class StubToolkit(
    string id, string name, ToolkitCategory category, bool configured = true)
    : IToolkit
{
    public string          Id           => id;
    public string          Name         => name;
    public ToolkitCategory Category     => category;
    public bool            IsConfigured => configured;

    public Task<ToolkitResult> ExecuteAsync(
        string action, Dictionary<string, object?> parameters, CancellationToken ct = default)
        => Task.FromResult(ToolkitResult.Success(new { action, id }));
}

file sealed class FailingToolkit(string id) : IToolkit
{
    public string          Id           => id;
    public string          Name         => id;
    public ToolkitCategory Category     => ToolkitCategory.Development;
    public bool            IsConfigured => true;

    public Task<ToolkitResult> ExecuteAsync(
        string action, Dictionary<string, object?> parameters, CancellationToken ct = default)
        => Task.FromResult(ToolkitResult.Fail("intentional failure"));
}

file sealed class SlowToolkit(string id, int delayMs) : IToolkit
{
    public string          Id           => id;
    public string          Name         => id;
    public ToolkitCategory Category     => ToolkitCategory.Development;
    public bool            IsConfigured => true;

    public async Task<ToolkitResult> ExecuteAsync(
        string action, Dictionary<string, object?> parameters, CancellationToken ct = default)
    {
        await Task.Delay(delayMs, ct);
        return ToolkitResult.Success(new { action });
    }
}

file sealed class SupportedActionsToolkit(string id, string[] actions)
    : IToolkit, ISupportedActionsProvider
{
    public string          Id           => id;
    public string          Name         => id;
    public ToolkitCategory Category     => ToolkitCategory.DataAnalysis;
    public bool            IsConfigured => true;
    public IReadOnlyList<string> SupportedActions => actions;

    public Task<ToolkitResult> ExecuteAsync(
        string action, Dictionary<string, object?> parameters, CancellationToken ct = default)
        => Task.FromResult(ToolkitResult.Success(new { action, id }));
}

file sealed class FakeMetrics : IToolMetricsCollector
{
    public List<(string Name, bool Success, double Ms)> Calls { get; } = [];
    public void Record(string toolName, bool success, double latencyMs)
        => Calls.Add((toolName, success, latencyMs));
    public ToolMetricSnapshot? GetSnapshot(string toolName) => null;
    public IReadOnlyList<ToolMetricSnapshot> GetAllSnapshots() => [];
    public void Reset(string toolName) { }
    public void ResetAll() { }
}

// Stub IEmbedder: returns a deterministic fixed-size unit vector per input string.
file sealed class StubEmbedder : IEmbedder
{
    public EmbedderType EmbedderType => EmbedderType.OnnxBertMiniLM;
    public int          Dimensions   => 4;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        // Simple hash-based deterministic vector — different texts → different vectors
        var h = (float)Math.Abs(text.GetHashCode() % 1000) / 1000f;
        var v = new float[] { h, 1f - h, h * 0.5f, (1f - h) * 0.5f };
        Normalize(v);
        return Task.FromResult(v);
    }

    public async Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
            results[i] = await EmbedAsync(texts[i], ct);
        return results;
    }

    private static void Normalize(float[] v)
    {
        float norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm > 0) for (int i = 0; i < v.Length; i++) v[i] /= norm;
    }
}

static file class Helpers
{
    public static CapabilityRegistry MakeRegistry(params IToolkit[] toolkits)
    {
        var r = new CapabilityRegistry();
        foreach (var t in toolkits) r.Register(t);
        return r;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CapabilityRegistry Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class CapabilityRegistryTests
{
    [Fact]
    public void Register_SingleToolkit_CountIsOne()
    {
        var r = new CapabilityRegistry();
        r.Register(new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis));
        Assert.Equal(1, r.Count);
    }

    [Fact]
    public void Register_DuplicateId_ReplacesExisting()
    {
        var r = new CapabilityRegistry();
        r.Register(new StubToolkit("csv", "CSV v1", ToolkitCategory.DataAnalysis));
        r.Register(new StubToolkit("csv", "CSV v2", ToolkitCategory.DataAnalysis));
        Assert.Equal(1, r.Count);
        Assert.Equal("CSV v2", r.FindById("csv")!.Name);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        var r = Helpers.MakeRegistry(
            new StubToolkit("a", "A", ToolkitCategory.DataAnalysis),
            new StubToolkit("b", "B", ToolkitCategory.Communication));
        Assert.Equal(2, r.GetAll().Count);
    }

    [Fact]
    public void GetConfigured_FiltersUnconfigured()
    {
        var r = Helpers.MakeRegistry(
            new StubToolkit("ok", "OK",  ToolkitCategory.DataAnalysis, configured: true),
            new StubToolkit("no", "No",  ToolkitCategory.DataAnalysis, configured: false));
        var configured = r.GetConfigured();
        Assert.Single(configured);
        Assert.Equal("ok", configured[0].Id);
    }

    [Fact]
    public void GetByCategory_ReturnsOnlyMatchingCategory()
    {
        var r = Helpers.MakeRegistry(
            new StubToolkit("sqlite_query", "SQLite", ToolkitCategory.DataAnalysis),
            new StubToolkit("slack",        "Slack",  ToolkitCategory.Communication));
        var data = r.GetByCategory(ToolkitCategory.DataAnalysis);
        Assert.Single(data);
        Assert.Equal("sqlite_query", data[0].Id);
    }

    [Fact]
    public void FindById_ExistingId_ReturnsManifest()
    {
        var r = Helpers.MakeRegistry(new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis));
        var m = r.FindById("csv");
        Assert.NotNull(m);
        Assert.Equal("CSV", m.Name);
        Assert.Equal(ToolkitCategory.DataAnalysis, m.Category);
    }

    [Fact]
    public void FindById_UnknownId_ReturnsNull()
        => Assert.Null(new CapabilityRegistry().FindById("nonexistent"));

    [Fact]
    public void FindById_CaseInsensitive()
    {
        var r = Helpers.MakeRegistry(new StubToolkit("CSV", "CSV", ToolkitCategory.DataAnalysis));
        Assert.NotNull(r.FindById("csv"));
        Assert.NotNull(r.FindById("CSV"));
    }

    [Fact]
    public void ResolveToolkit_ReturnsLiveInstance()
    {
        var toolkit = new StubToolkit("json_data", "JSON", ToolkitCategory.DataAnalysis);
        var r = Helpers.MakeRegistry(toolkit);
        var resolved = r.ResolveToolkit("json_data");
        Assert.Same(toolkit, resolved);
    }

    [Fact]
    public void ManifestFromSupportedActionsToolkit_IncludesActions()
    {
        var t = new SupportedActionsToolkit("sqlite_query", ["query", "execute", "schema"]);
        var r = Helpers.MakeRegistry(t);
        var m = r.FindById("sqlite_query");
        Assert.NotNull(m);
        Assert.Contains("query", m!.SupportedActions);
        Assert.Contains("execute", m.SupportedActions);
    }

    [Fact]
    public void ScanAssembly_DiscoversSqliteToolkit()
    {
        var r = new CapabilityRegistry();
        r.ScanAssembly(typeof(SqliteToolkit).Assembly,
            new ToolkitConfig("sqlite_query", new Dictionary<string, string> { ["enabled"] = "true" }));
        // SqliteToolkit should be discovered
        var m = r.FindById("sqlite_query");
        Assert.NotNull(m);
    }

    [Fact]
    public void ManifestIsConfigured_ReflectsToolkitIsConfigured()
    {
        var r = Helpers.MakeRegistry(
            new StubToolkit("t1", "T1", ToolkitCategory.DataAnalysis, configured: true),
            new StubToolkit("t2", "T2", ToolkitCategory.DataAnalysis, configured: false));
        Assert.True(r.FindById("t1")!.IsConfigured);
        Assert.False(r.FindById("t2")!.IsConfigured);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ToolRouter Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ToolRouterTests
{
    [Fact]
    public void Route_ExactId_ConfiguredToolkit_ReturnsIt()
    {
        var toolkit = new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis, configured: true);
        var router  = new ToolRouter(Helpers.MakeRegistry(toolkit));
        Assert.Same(toolkit, router.Route("csv"));
    }

    [Fact]
    public void Route_FallbackAlias_Sql_ReturnsSqlite()
    {
        var sqlite = new StubToolkit("sqlite_query", "SQLite", ToolkitCategory.DataAnalysis, configured: true);
        var router  = new ToolRouter(Helpers.MakeRegistry(sqlite));
        var result  = router.Route("sql");
        Assert.NotNull(result);
        Assert.Equal("sqlite_query", result!.Id);
    }

    [Fact]
    public void Route_FallbackAlias_Spreadsheet_ReturnsExcelFirst()
    {
        var excel = new StubToolkit("excel", "Excel", ToolkitCategory.DataAnalysis, configured: true);
        var csv   = new StubToolkit("csv",   "CSV",   ToolkitCategory.DataAnalysis, configured: true);
        var router = new ToolRouter(Helpers.MakeRegistry(excel, csv));
        Assert.Equal("excel", router.Route("spreadsheet")!.Id);
    }

    [Fact]
    public void Route_FallbackAlias_SkipsUnconfigured_ReturnsNextConfigured()
    {
        var excel = new StubToolkit("excel", "Excel", ToolkitCategory.DataAnalysis, configured: false);
        var csv   = new StubToolkit("csv",   "CSV",   ToolkitCategory.DataAnalysis, configured: true);
        var router = new ToolRouter(Helpers.MakeRegistry(excel, csv));
        var result = router.Route("spreadsheet");
        Assert.Equal("csv", result!.Id);
    }

    [Fact]
    public void Route_ConfiguredOnly_True_IgnoresUnconfigured()
    {
        var t = new StubToolkit("sqlite_query", "SQLite", ToolkitCategory.DataAnalysis, configured: false);
        var router = new ToolRouter(Helpers.MakeRegistry(t));
        Assert.Null(router.Route("sql", configuredOnly: true));
    }

    [Fact]
    public void Route_ConfiguredOnly_False_ReturnsUnconfiguredAsFallback()
    {
        var t = new StubToolkit("sqlite_query", "SQLite", ToolkitCategory.DataAnalysis, configured: false);
        var router = new ToolRouter(Helpers.MakeRegistry(t));
        Assert.NotNull(router.Route("sql", configuredOnly: false));
    }

    [Fact]
    public void Route_CategoryName_ReturnsFirstConfiguredInCategory()
    {
        var t = new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis, configured: true);
        var router = new ToolRouter(Helpers.MakeRegistry(t));
        Assert.Equal("csv", router.Route("DataAnalysis")!.Id);
    }

    [Fact]
    public void Route_UnknownGoal_ReturnsNull()
    {
        var router = new ToolRouter(new CapabilityRegistry());
        Assert.Null(router.Route("this_does_not_exist"));
    }

    [Fact]
    public void RouteToManifest_ReturnsManifest()
    {
        var t = new StubToolkit("sqlite_query", "SQLite", ToolkitCategory.DataAnalysis);
        var router = new ToolRouter(Helpers.MakeRegistry(t));
        var m = router.RouteToManifest("sql");
        Assert.NotNull(m);
        Assert.Equal("sqlite_query", m!.Id);
    }

    [Fact]
    public void KnownAliases_ContainsSqlAlias()
        => Assert.Contains("sql", ToolRouter.KnownAliases);
}

// ─────────────────────────────────────────────────────────────────────────────
// BudgetAwareExecutor Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class BudgetAwareExecutorTests
{
    [Fact]
    public async Task Execute_SuccessfulCall_RecordsMetric()
    {
        var metrics  = new FakeMetrics();
        var executor = new BudgetAwareExecutor(metrics);
        var toolkit  = new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis);

        await executor.ExecuteAsync(toolkit, "read", []);

        Assert.Single(metrics.Calls);
        Assert.Equal("csv", metrics.Calls[0].Name);
        Assert.True(metrics.Calls[0].Success);
    }

    [Fact]
    public async Task Execute_FailingToolkit_RecordsFailureMetric()
    {
        var metrics  = new FakeMetrics();
        var executor = new BudgetAwareExecutor(metrics);
        var toolkit  = new FailingToolkit("broken");

        await executor.ExecuteAsync(toolkit, "action", []);

        Assert.Single(metrics.Calls);
        Assert.False(metrics.Calls[0].Success);
    }

    [Fact]
    public async Task Execute_ExceedsRateLimit_ReturnsError()
    {
        var metrics  = new FakeMetrics();
        var executor = new BudgetAwareExecutor(metrics);
        var toolkit  = new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis);
        var budget   = ToolkitBudget.PerMinute(2);

        var r1 = await executor.ExecuteAsync(toolkit, "read", [], budget);
        var r2 = await executor.ExecuteAsync(toolkit, "read", [], budget);
        var r3 = await executor.ExecuteAsync(toolkit, "read", [], budget); // should be blocked

        Assert.Equal(ToolkitResultStatus.Ok,    r1.Status);
        Assert.Equal(ToolkitResultStatus.Ok,    r2.Status);
        Assert.Equal(ToolkitResultStatus.Error, r3.Status);
        Assert.Contains("Rate limit", r3.Error);
    }

    [Fact]
    public async Task Execute_ExceedsTotalCalls_ReturnsError()
    {
        var executor = new BudgetAwareExecutor(new FakeMetrics());
        var toolkit  = new StubToolkit("t", "T", ToolkitCategory.DataAnalysis);
        var budget   = new ToolkitBudget { MaxTotalCalls = 1 };

        var r1 = await executor.ExecuteAsync(toolkit, "a", [], budget);
        var r2 = await executor.ExecuteAsync(toolkit, "a", [], budget); // over cap

        Assert.Equal(ToolkitResultStatus.Ok,    r1.Status);
        Assert.Equal(ToolkitResultStatus.Error, r2.Status);
        Assert.Contains("budget", r2.Error);
    }

    [Fact]
    public async Task Execute_Timeout_ReturnsErrorWithMessage()
    {
        var executor = new BudgetAwareExecutor(new FakeMetrics());
        var toolkit  = new SlowToolkit("slow", delayMs: 500);
        var budget   = ToolkitBudget.WithTimeout(TimeSpan.FromMilliseconds(50));

        var r = await executor.ExecuteAsync(toolkit, "action", [], budget);

        Assert.Equal(ToolkitResultStatus.Error, r.Status);
        Assert.Contains("exceeded max duration", r.Error);
    }

    [Fact]
    public async Task TotalCallCount_TracksAllCalls()
    {
        var executor = new BudgetAwareExecutor(new FakeMetrics());
        var toolkit  = new StubToolkit("t", "T", ToolkitCategory.DataAnalysis);

        await executor.ExecuteAsync(toolkit, "a", []);
        await executor.ExecuteAsync(toolkit, "b", []);
        await executor.ExecuteAsync(toolkit, "c", []);

        Assert.Equal(3, executor.TotalCallCount);
    }

    [Fact]
    public void UnlimitedBudget_IsDefault()
    {
        var budget = ToolkitBudget.Unlimited;
        Assert.Equal(int.MaxValue, budget.MaxCallsPerMinute);
        Assert.Equal(int.MaxValue, budget.MaxTotalCalls);
        Assert.Null(budget.MaxDuration);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ToolkitPolicyEnforcer Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ToolkitPolicyEnforcerTests
{
    // ── AllowAll ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AllowAllProvider_PermitsAnyToolkit()
    {
        var enforcer = new ToolkitPolicyEnforcer(AllowAllPolicyProvider.Instance);
        Assert.True(await enforcer.IsAllowedAsync("agent-1", "sqlite_query", ToolkitCategory.DataAnalysis));
    }

    [Fact]
    public async Task AllowAllProvider_EnforceAndExecute_ReturnsOk()
    {
        var enforcer = new ToolkitPolicyEnforcer(AllowAllPolicyProvider.Instance);
        var toolkit  = new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis);
        var r = await enforcer.EnforceAndExecuteAsync(toolkit, "read", [], "agent-1");
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }

    // ── AllowedToolkitIds ─────────────────────────────────────────────────────

    [Fact]
    public async Task AllowedIds_PermittsListedId()
    {
        var provider = new InMemoryPolicyProvider().Set("a1", new ToolkitPolicy
        {
            SubjectId = "a1",
            AllowedToolkitIds = ["csv", "excel"]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        Assert.True(await enforcer.IsAllowedAsync("a1", "csv", ToolkitCategory.DataAnalysis));
    }

    [Fact]
    public async Task AllowedIds_BlocksUnlistedId()
    {
        var provider = new InMemoryPolicyProvider().Set("a1", new ToolkitPolicy
        {
            SubjectId = "a1",
            AllowedToolkitIds = ["csv"]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        Assert.False(await enforcer.IsAllowedAsync("a1", "sqlite_query", ToolkitCategory.DataAnalysis));
    }

    // ── AllowedCategories ─────────────────────────────────────────────────────

    [Fact]
    public async Task AllowedCategories_PermitsMatchingCategory()
    {
        var provider = new InMemoryPolicyProvider().Set("a2", new ToolkitPolicy
        {
            SubjectId = "a2",
            AllowedCategories = [ToolkitCategory.DataAnalysis]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        Assert.True(await enforcer.IsAllowedAsync("a2", "pandas_like", ToolkitCategory.DataAnalysis));
    }

    [Fact]
    public async Task AllowedCategories_BlocksNonMatchingCategory()
    {
        var provider = new InMemoryPolicyProvider().Set("a2", new ToolkitPolicy
        {
            SubjectId = "a2",
            AllowedCategories = [ToolkitCategory.DataAnalysis]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        Assert.False(await enforcer.IsAllowedAsync("a2", "slack", ToolkitCategory.Communication));
    }

    // ── BlockedIds ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BlockedIds_AlwaysBlock_EvenIfCategoryAllowed()
    {
        var provider = new InMemoryPolicyProvider().Set("a3", new ToolkitPolicy
        {
            SubjectId = "a3",
            AllowedCategories = [ToolkitCategory.DataAnalysis],
            BlockedToolkitIds = ["pandas_like"]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        Assert.False(await enforcer.IsAllowedAsync("a3", "pandas_like", ToolkitCategory.DataAnalysis));
    }

    // ── DefaultDeny ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultDeny_BlocksEverythingNotListed()
    {
        var provider = new InMemoryPolicyProvider().Set("a4", new ToolkitPolicy
        {
            SubjectId = "a4",
            DefaultDeny = true
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        Assert.False(await enforcer.IsAllowedAsync("a4", "csv", ToolkitCategory.DataAnalysis));
    }

    // ── EnforceAndExecute ─────────────────────────────────────────────────────

    [Fact]
    public async Task EnforceAndExecute_Blocked_ReturnsUnauthorized()
    {
        var provider = new InMemoryPolicyProvider().Set("ag", new ToolkitPolicy
        {
            SubjectId = "ag",
            BlockedToolkitIds = ["csv"]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);
        var toolkit  = new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis);
        var r = await enforcer.EnforceAndExecuteAsync(toolkit, "read", [], "ag");
        Assert.Equal(ToolkitResultStatus.Unauthorized, r.Status);
        Assert.Contains("not permitted", r.Error);
    }

    // ── FilterAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Filter_RemovesBlockedManifests()
    {
        var provider = new InMemoryPolicyProvider().Set("ag", new ToolkitPolicy
        {
            SubjectId = "ag",
            BlockedToolkitIds = ["slack"]
        });
        var enforcer = new ToolkitPolicyEnforcer(provider);

        var manifests = new[]
        {
            new CapabilityManifest("csv",   "CSV",   ToolkitCategory.DataAnalysis,  true, []),
            new CapabilityManifest("slack", "Slack", ToolkitCategory.Communication, true, []),
        };

        var allowed = await enforcer.FilterAsync(manifests, "ag");
        Assert.Single(allowed);
        Assert.Equal("csv", allowed[0].Id);
    }

    [Fact]
    public async Task Filter_DefaultDeny_ReturnsEmpty()
    {
        var provider = new InMemoryPolicyProvider().Set("ag", new ToolkitPolicy
        {
            SubjectId = "ag", DefaultDeny = true
        });
        var enforcer  = new ToolkitPolicyEnforcer(provider);
        var manifests = new[]
        {
            new CapabilityManifest("csv", "CSV", ToolkitCategory.DataAnalysis, true, [])
        };

        var allowed = await enforcer.FilterAsync(manifests, "ag");
        Assert.Empty(allowed);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SemanticToolSelector Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class SemanticToolSelectorTests
{
    private static CapabilityRegistry MakeRichRegistry()
    {
        return Helpers.MakeRegistry(
            new SupportedActionsToolkit("sqlite_query", ["query", "execute", "pragma"]),
            new SupportedActionsToolkit("csv",          ["read", "write", "info"]),
            new StubToolkit("slack",     "Slack",    ToolkitCategory.Communication),
            new StubToolkit("pandas_like","Pandas",  ToolkitCategory.DataAnalysis));
    }

    [Fact]
    public async Task SelectAsync_ReturnsNonNull_ForValidGoal()
    {
        await using var selector = new SemanticToolSelector(MakeRichRegistry(), new StubEmbedder());
        var m = await selector.SelectAsync("analyse tabular data");
        Assert.NotNull(m);
    }

    [Fact]
    public async Task SelectAsync_WithCategoryFilter_ReturnsOnlyFromCategory()
    {
        await using var selector = new SemanticToolSelector(MakeRichRegistry(), new StubEmbedder());
        var m = await selector.SelectAsync("data analysis", filterCategory: ToolkitCategory.Communication);
        Assert.NotNull(m);
        Assert.Equal(ToolkitCategory.Communication, m!.Category);
    }

    [Fact]
    public async Task SelectAsync_ConfiguredOnly_ExcludesUnconfigured()
    {
        var registry = Helpers.MakeRegistry(
            new StubToolkit("t-configured",   "T1", ToolkitCategory.DataAnalysis, configured: true),
            new StubToolkit("t-unconfigured", "T2", ToolkitCategory.DataAnalysis, configured: false));
        await using var selector = new SemanticToolSelector(registry, new StubEmbedder());
        var m = await selector.SelectAsync("data", configuredOnly: true);
        Assert.NotNull(m);
        Assert.True(m!.IsConfigured);
    }

    [Fact]
    public async Task SelectAsync_EmptyRegistry_ReturnsNull()
    {
        await using var selector = new SemanticToolSelector(new CapabilityRegistry(), new StubEmbedder());
        Assert.Null(await selector.SelectAsync("anything"));
    }

    [Fact]
    public async Task SelectTopAsync_ReturnsSortedByScore()
    {
        await using var selector = new SemanticToolSelector(MakeRichRegistry(), new StubEmbedder());
        var top = await selector.SelectTopAsync("query database", topN: 3);
        Assert.True(top.Count <= 3);
        // Scores must be non-increasing
        for (int i = 0; i < top.Count - 1; i++)
            Assert.True(top[i].Score >= top[i + 1].Score);
    }

    [Fact]
    public async Task InvalidateIndex_ForcesRebuild()
    {
        var registry = Helpers.MakeRegistry(
            new StubToolkit("csv", "CSV", ToolkitCategory.DataAnalysis));
        await using var selector = new SemanticToolSelector(registry, new StubEmbedder());

        // First call builds index
        await selector.SelectAsync("data");

        // Add new toolkit + invalidate
        registry.Register(new StubToolkit("excel", "Excel", ToolkitCategory.DataAnalysis));
        selector.InvalidateIndex();

        // Should now include excel
        var top = await selector.SelectTopAsync("spreadsheet", topN: 10);
        Assert.Contains(top, x => x.Manifest.Id == "excel");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// InMemoryPolicyProvider Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class InMemoryPolicyProviderTests
{
    [Fact]
    public async Task GetPolicy_UnknownSubject_ReturnsAllowAll()
    {
        var provider = new InMemoryPolicyProvider();
        var policy   = await provider.GetPolicyAsync("nobody");
        Assert.Empty(policy.AllowedToolkitIds);
        Assert.Empty(policy.BlockedToolkitIds);
        Assert.False(policy.DefaultDeny);
    }

    [Fact]
    public async Task SetAndGet_ReturnsSamePolicy()
    {
        var policy   = new ToolkitPolicy { SubjectId = "x", BlockedToolkitIds = ["slack"] };
        var provider = new InMemoryPolicyProvider().Set("x", policy);
        var loaded   = await provider.GetPolicyAsync("x");
        Assert.Contains("slack", loaded.BlockedToolkitIds);
    }

    [Fact]
    public async Task Set_OverwritesPreviousPolicy()
    {
        var provider = new InMemoryPolicyProvider()
            .Set("x", new ToolkitPolicy { SubjectId = "x", BlockedToolkitIds = ["a"] })
            .Set("x", new ToolkitPolicy { SubjectId = "x", BlockedToolkitIds = ["b"] });
        var loaded = await provider.GetPolicyAsync("x");
        Assert.DoesNotContain("a", loaded.BlockedToolkitIds);
        Assert.Contains("b", loaded.BlockedToolkitIds);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CapabilityManifest Record Tests
// ─────────────────────────────────────────────────────────────────────────────

public sealed class CapabilityManifestTests
{
    [Fact]
    public void Manifest_Equality_SameValues_AreEqual()
    {
        // IReadOnlyList<string> uses reference equality inside records —
        // reuse the same list instance to guarantee structural equality.
        IReadOnlyList<string> actions = ["read"];
        var a = new CapabilityManifest("csv", "CSV", ToolkitCategory.DataAnalysis, true, actions);
        var b = new CapabilityManifest("csv", "CSV", ToolkitCategory.DataAnalysis, true, actions);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Manifest_With_CreatesNewInstance()
    {
        var a = new CapabilityManifest("csv", "CSV", ToolkitCategory.DataAnalysis, true,  []);
        var b = a with { IsConfigured = false };
        Assert.True(a.IsConfigured);
        Assert.False(b.IsConfigured);
    }

    [Fact]
    public void Manifest_SupportedActions_EmptyByDefault()
    {
        var m = new CapabilityManifest("t", "T", ToolkitCategory.DataAnalysis, true, []);
        Assert.Empty(m.SupportedActions);
    }
}
