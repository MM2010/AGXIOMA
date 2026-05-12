// F15-TS08 — Integration Tests (10 tests)
// End-to-end: SaveResult → NotifyCompletion → PollResults pipeline using InMemory stores.
// Also validates DI extension registration and HotKeys naming uniqueness.

using AGXIOME.Orchestration;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Tools.SubAgent;
using AGXIOME.Orchestration.Tools.Master;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace AGXIOME.Tests.F15;

// ═══════════════════════════════════════════════════════════════════
// TS08-A  HotKeys uniqueness (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS08_HotKeysTests
{
    [Fact] public void HotKeys_DifferentTaskIds_ProduceDifferentKeys()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        Assert.NotEqual(HotKeys.TaskResult(id1), HotKeys.TaskResult(id2));
        Assert.NotEqual(HotKeys.TaskStatus(id1), HotKeys.TaskStatus(id2));
        Assert.NotEqual(HotKeys.TaskJobId(id1), HotKeys.TaskJobId(id2));
    }

    [Fact] public void HotKeys_DifferentPlanIds_ProduceDifferentKeys()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        Assert.NotEqual(HotKeys.PlanMeta(id1), HotKeys.PlanMeta(id2));
        Assert.NotEqual(HotKeys.PlanStatus(id1), HotKeys.PlanStatus(id2));
        Assert.NotEqual(HotKeys.PlanStep(id1), HotKeys.PlanStep(id2));
    }

    [Fact] public void HotKeys_TaskResultAndTaskStatus_HaveDifferentPrefixes()
    {
        var id = Guid.NewGuid();
        Assert.NotEqual(HotKeys.TaskResult(id), HotKeys.TaskStatus(id));
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS08-B  Save + Notify + Poll pipeline (4 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS08_SaveNotifyPollPipelineTests
{
    private static (InMemoryHotStore hot, InMemoryOrchestrationStore cold, InMemoryCompletionNotifier notifier) Fresh()
    {
        var hot = new InMemoryHotStore(); hot.Clear();
        var cold = new InMemoryOrchestrationStore(); cold.Clear();
        var notifier = new InMemoryCompletionNotifier(); notifier.Clear();
        return (hot, cold, notifier);
    }

    [Fact] public async Task SaveThenNotify_PollReceivesResult()
    {
        var (hot, cold, notifier) = Fresh();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        // Step 1: Save
        var saveTool = new SaveResultTool(hot, cold);
        var saveCtx = new ToolContext("agent", "run", Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString(),
            ["output"] = "great output",
            ["summary"] = "brief",
            ["success"] = true
        });
        var saveResult = await saveTool.ExecuteAsync(saveCtx, CancellationToken.None);
        Assert.True(saveResult.Success);

        // Step 2: Notify
        var notifyTool = new NotifyCompletionTool(notifier, hot);
        var notifyCtx = new ToolContext("agent", "run", Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString()
        });
        var notifyResult = await notifyTool.ExecuteAsync(notifyCtx, CancellationToken.None);
        Assert.True(notifyResult.Success);

        // Step 3: Poll — subscribe and check result arrives
        var received = new List<TaskResult>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var r in notifier.SubscribeAsync(planId, cts.Token))
        {
            received.Add(r);
            break;
        }
        Assert.Single(received);
        Assert.Equal(taskId, received[0].TaskId);
    }

    [Fact] public async Task SaveThenNotify_FailedTask_PollReceivesFailure()
    {
        var (hot, cold, notifier) = Fresh();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var saveTool = new SaveResultTool(hot, cold);
        var saveCtx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString(),
            ["output"] = "failed output",
            ["success"] = false,
            ["errorMessage"] = "something went wrong"
        });
        await saveTool.ExecuteAsync(saveCtx, CancellationToken.None);

        var notifyTool = new NotifyCompletionTool(notifier, hot);
        await notifyTool.ExecuteAsync(new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString()
        }), CancellationToken.None);

        var received = new List<TaskResult>();
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var r in notifier.SubscribeAsync(planId, cts2.Token))
        {
            received.Add(r);
            break;
        }
        Assert.Single(received);
        Assert.False(received[0].Success);
    }

    [Fact] public async Task PollResults_TimesOut_WhenNoResultPublished()
    {
        var (hot, cold, notifier) = Fresh();
        var planId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var pollTool = new PollResultsTool(notifier, hot, cold);
        var ctx = new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["planId"] = planId.ToString(),
            ["expectedTaskIds"] = (IReadOnlyList<Guid>)new List<Guid> { taskId },
            ["timeoutSeconds"] = 1 // very short
        });
        var result = await pollTool.ExecuteAsync(ctx, CancellationToken.None);
        // Should succeed (tool itself succeeds) but with timedOut tasks
        Assert.True(result.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        Assert.Contains("timedOutTasks", json);
    }

    [Fact] public async Task ColdStore_SaveResult_PersistsAcrossRequeries()
    {
        var (hot, cold, _) = Fresh();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var saveTool = new SaveResultTool(hot, cold);
        await saveTool.ExecuteAsync(new ToolContext("a", "r", Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString(),
            ["output"] = "persisted",
            ["success"] = true
        }), CancellationToken.None);

        var loaded = await cold.GetResultAsync(taskId);
        Assert.NotNull(loaded);
        Assert.Equal("persisted", loaded!.Output);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS08-C  DI extension sanity (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS08_DIExtensionTests
{
    [Fact] public void AddAgxiomeOrchestration_RegistersIHotStore()
    {
        var llm = Substitute.For<ILLMProvider>();
        var worker = Substitute.For<AGXIOME.Core.Execution.IBackgroundRunWorker>();
        var services = new ServiceCollection();
        services.AddSingleton(llm);
        services.AddSingleton(worker);
        // Logging
        services.AddLogging();

        services.AddAgxiomeOrchestration();
        var provider = services.BuildServiceProvider();
        var hotStore = provider.GetRequiredService<IHotStore>();
        Assert.NotNull(hotStore);
    }

    [Fact] public void AddAgxiomeOrchestration_RegistersIOrchestrationStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ILLMProvider>());
        services.AddSingleton(Substitute.For<AGXIOME.Core.Execution.IBackgroundRunWorker>());
        services.AddLogging();
        services.AddAgxiomeOrchestration();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IOrchestrationStore>());
    }

    [Fact] public void AddAgxiomeOrchestration_RegistersICompletionNotifier()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ILLMProvider>());
        services.AddSingleton(Substitute.For<AGXIOME.Core.Execution.IBackgroundRunWorker>());
        services.AddLogging();
        services.AddAgxiomeOrchestration();
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ICompletionNotifier>());
    }
}
