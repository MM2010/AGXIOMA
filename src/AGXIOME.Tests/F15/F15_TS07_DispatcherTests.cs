// F15-TS07 — Dispatcher Tests (10 tests)
// Validates BackgroundWorkerAgentSpawner: spawn creates jobs, cancel delegates,
// and IsActiveAsync reflects job state correctly.

using AGXIOME.Core.Execution;
using AGXIOME.Orchestration.Dispatcher;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Models;
using NSubstitute;

namespace AGXIOME.Tests.F15;

public sealed class F15_TS07_DispatcherTests
{
    private static TaskSpec MakeSpec(Guid? planId = null) => new()
    {
        TaskId = Guid.NewGuid(),
        PlanId = planId ?? Guid.NewGuid(),
        StepIndex = 0,
        Description = "do the work",
        AllowedToolNames = new List<string> { "ToolA" },
        Temperature = 0.5f,
        MaxTokens = 1024,
        MaxIterations = 5
    };

    private static IBackgroundRunWorker MockWorker(string? jobId = null, BackgroundJobStatus status = BackgroundJobStatus.Running)
    {
        var worker = Substitute.For<IBackgroundRunWorker>();
        var actualJobId = jobId ?? Guid.NewGuid().ToString("N");
        worker.Enqueue(Arg.Any<RunJob>()).Returns(actualJobId);
        worker.GetState(Arg.Any<string>())
              .Returns(new RunJobState(actualJobId, BackgroundJobType.RunExecution, status));
        worker.Cancel(Arg.Any<string>()).Returns(true);
        return worker;
    }

    [Fact] public async Task SpawnAsync_CallsWorkerEnqueue()
    {
        var worker = MockWorker();
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        await spawner.SpawnAsync(MakeSpec());
        worker.Received(1).Enqueue(Arg.Any<RunJob>());
    }

    [Fact] public async Task SpawnAsync_ReturnsJobId()
    {
        var worker = MockWorker("my-job-id");
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var jobId = await spawner.SpawnAsync(MakeSpec());
        Assert.Equal("my-job-id", jobId);
    }

    [Fact] public async Task SpawnAsync_JobIncludesDescription()
    {
        RunJob? captured = null;
        var worker = Substitute.For<IBackgroundRunWorker>();
        worker.Enqueue(Arg.Do<RunJob>(j => captured = j)).Returns("jid");

        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var spec = MakeSpec() with { Description = "unique-description-xyz" };
        await spawner.SpawnAsync(spec);

        Assert.NotNull(captured);
        Assert.Contains("unique-description-xyz", captured!.Prompt);
    }

    [Fact] public async Task SpawnAsync_JobContextContainsTaskId()
    {
        RunJob? captured = null;
        var worker = Substitute.For<IBackgroundRunWorker>();
        worker.Enqueue(Arg.Do<RunJob>(j => captured = j)).Returns("jid");

        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var spec = MakeSpec();
        await spawner.SpawnAsync(spec);

        Assert.NotNull(captured);
        Assert.True(captured!.Context?.ContainsKey("taskId"));
        Assert.Equal(spec.TaskId.ToString(), captured.Context!["taskId"].ToString());
    }

    [Fact] public async Task CancelAsync_CallsWorkerCancel()
    {
        var worker = MockWorker();
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        await spawner.CancelAsync("job-123");
        worker.Received(1).Cancel("job-123");
    }

    [Fact] public async Task IsActiveAsync_RunningStatus_ReturnsTrue()
    {
        var worker = MockWorker(status: BackgroundJobStatus.Running);
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var active = await spawner.IsActiveAsync("j1");
        Assert.True(active);
    }

    [Fact] public async Task IsActiveAsync_CompletedStatus_ReturnsFalse()
    {
        var worker = MockWorker(status: BackgroundJobStatus.Completed);
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var active = await spawner.IsActiveAsync("j1");
        Assert.False(active);
    }

    [Fact] public async Task IsActiveAsync_FailedStatus_ReturnsFalse()
    {
        var worker = MockWorker(status: BackgroundJobStatus.Failed);
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var active = await spawner.IsActiveAsync("j1");
        Assert.False(active);
    }

    [Fact] public async Task IsActiveAsync_CancelledStatus_ReturnsFalse()
    {
        var worker = MockWorker(status: BackgroundJobStatus.Cancelled);
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var active = await spawner.IsActiveAsync("j1");
        Assert.False(active);
    }

    [Fact] public async Task IsActiveAsync_UnknownJobId_ReturnsFalse()
    {
        var worker = Substitute.For<IBackgroundRunWorker>();
        worker.GetState(Arg.Any<string>()).Returns((RunJobState?)null);
        var spawner = new BackgroundWorkerAgentSpawner(worker);
        var active = await spawner.IsActiveAsync("no-such-job");
        Assert.False(active);
    }
}
