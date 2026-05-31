using AGXIOME.Core.Hooks;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Xunit;

namespace AGXIOME.Tests.F18;

// ── Happy Path ────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS03_Hooks_HappyPath
{
    [Fact]
    public void DelegateHook_Properties_ReflectConstructorArgs()
    {
        var hook = new DelegateHook(
            name:       "test-hook",
            point:      HookPoint.PostRun,
            handler:    (_, _) => Task.CompletedTask,
            order:      5,
            background: true);

        Assert.Equal("test-hook",      hook.Name);
        Assert.Equal(HookPoint.PostRun, hook.Point);
        Assert.Equal(5,                hook.Order);
        Assert.True(hook.RunInBackground);
    }

    [Fact]
    public async Task DelegateHook_ExecuteAsync_InvokesHandler()
    {
        bool invoked = false;
        var hook = new DelegateHook("h", HookPoint.PreRun,
            handler: (ctx, _) => { invoked = true; return Task.CompletedTask; });

        var ctx = new HookContext("agent1", "run1", HookPoint.PreRun);
        await hook.ExecuteAsync(ctx);

        Assert.True(invoked);
    }

    [Fact]
    public async Task DelegateHook_HandlerReceivesCorrectContext()
    {
        HookContext? received = null;
        var hook = new DelegateHook("h", HookPoint.PreTool,
            handler: (ctx, _) => { received = ctx; return Task.CompletedTask; });

        var ctx = new HookContext("agentX", "runY", HookPoint.PreTool, Input: "hello");
        await hook.ExecuteAsync(ctx);

        Assert.NotNull(received);
        Assert.Equal("agentX",        received!.AgentId);
        Assert.Equal("runY",          received.RunId);
        Assert.Equal("hello",         received.Input);
        Assert.Equal(HookPoint.PreTool, received.Point);
    }

    [Fact]
    public void DelegateHook_DefaultOrder_IsZero()
    {
        var hook = new DelegateHook("h", HookPoint.PostTool, (_, _) => Task.CompletedTask);

        Assert.Equal(0, hook.Order);
    }

    [Fact]
    public void DelegateHook_DefaultBackground_IsFalse()
    {
        var hook = new DelegateHook("h", HookPoint.StreamChunk, (_, _) => Task.CompletedTask);

        Assert.False(hook.RunInBackground);
    }

    [Fact]
    public void HookContext_AllProperties_Accessible()
    {
        var ex  = new InvalidOperationException("boom");
        var ctx = new HookContext(
            AgentId:      "a1",
            RunId:        "r1",
            Point:        HookPoint.BackgroundPost,
            Input:        "in",
            Output:       "out",
            Error:        ex,
            SessionState: null);

        Assert.Equal("a1",                    ctx.AgentId);
        Assert.Equal("r1",                    ctx.RunId);
        Assert.Equal(HookPoint.BackgroundPost, ctx.Point);
        Assert.Equal("in",                    ctx.Input);
        Assert.Equal("out",                   ctx.Output);
        Assert.Same(ex,                       ctx.Error);
        Assert.Null(ctx.SessionState);
    }

    [Fact]
    public void HookPoint_CoversBothPreAndPostVariants()
    {
        var values = Enum.GetValues<HookPoint>();

        Assert.Contains(HookPoint.PreRun,       values);
        Assert.Contains(HookPoint.PostRun,      values);
        Assert.Contains(HookPoint.PreTool,      values);
        Assert.Contains(HookPoint.PostTool,     values);
        Assert.Contains(HookPoint.StreamChunk,  values);
        Assert.Contains(HookPoint.BackgroundPost, values);
    }
}

// ── Offensive ─────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS03_Hooks_Offensive
{
    [Fact]
    public async Task DelegateHook_HandlerThrows_PropagatesException()
    {
        var hook = new DelegateHook("h", HookPoint.PreRun,
            handler: (_, _) => throw new InvalidOperationException("hook-error"));

        var ctx = new HookContext("a", "r", HookPoint.PreRun);
        await Assert.ThrowsAsync<InvalidOperationException>(() => hook.ExecuteAsync(ctx));
    }

    [Fact]
    public async Task DelegateHook_CancellationRequested_StopImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var hook = new DelegateHook("h", HookPoint.PostRun,
            handler: async (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(1000, ct);
            });

        var ctx = new HookContext("a", "r", HookPoint.PostRun);
        await Assert.ThrowsAsync<OperationCanceledException>(() => hook.ExecuteAsync(ctx, cts.Token));
    }

    [Fact]
    public void DelegateHook_NullName_AcceptedByRuntime()
    {
        // The interface contract requires Name, but DelegateHook stores what is given.
        // Runtime callers must validate non-null; the class itself is permissive.
        var hook = new DelegateHook(null!, HookPoint.PreRun, (_, _) => Task.CompletedTask);
        Assert.Null(hook.Name);
    }

    [Fact]
    public async Task MultipleDelegateHooks_OrderPreservedWhenSortedExternally()
    {
        var order = new List<int>();
        var hooks = new[]
        {
            new DelegateHook("h3", HookPoint.PostRun, (_, _) => { order.Add(3); return Task.CompletedTask; }, order: 3),
            new DelegateHook("h1", HookPoint.PostRun, (_, _) => { order.Add(1); return Task.CompletedTask; }, order: 1),
            new DelegateHook("h2", HookPoint.PostRun, (_, _) => { order.Add(2); return Task.CompletedTask; }, order: 2),
        };

        var ctx     = new HookContext("a", "r", HookPoint.PostRun);
        var sorted  = hooks.OrderBy(h => h.Order).ToArray();

        foreach (var h in sorted)
            await h.ExecuteAsync(ctx);

        Assert.Equal(new[] { 1, 2, 3 }, order);
    }

    [Fact]
    public void IToolHook_ConcreteImplementation_SatisfiesInterface()
    {
        // Verify that a concrete class implementing IToolHook compiles and satisfies the contract.
        IToolHook hook = new NoOpToolHook();

        Assert.NotNull(hook);
    }
}

// ── Aggressive ────────────────────────────────────────────────────────────────

[Trait("Phase", "F18")]
public class F18_TS03_Hooks_Aggressive
{
    [Fact]
    public async Task DelegateHook_HighConcurrency_AllInvocations()
    {
        int count = 0;
        var hook = new DelegateHook("h", HookPoint.PreRun,
            handler: (_, _) => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        var ctx   = new HookContext("a", "r", HookPoint.PreRun);
        var tasks = Enumerable.Range(0, 200)
            .Select(_ => hook.ExecuteAsync(ctx))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(200, count);
    }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

/// <summary>Concrete no-op IToolHook for interface contract tests.</summary>
internal sealed class NoOpToolHook : IToolHook
{
    public Task BeforeExecuteAsync(ToolContext ctx, CancellationToken ct = default) => Task.CompletedTask;
    public Task AfterExecuteAsync(ToolContext ctx, ToolResult result, CancellationToken ct = default) => Task.CompletedTask;
}
