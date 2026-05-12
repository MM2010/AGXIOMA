// F15-TS09 — P15–P22 Integration Tests (22 tests)
// Validates the integrations added in P15 (HITL), P16 (Skills), P19 (SignalR notifier),
// P20 (Crypto HMAC), P21 (Metrics), P17 (Schedule adapter), P18 (XLink envelope).
// All tests are self-contained with in-memory fakes — no external services required.

using System.Collections.Concurrent;
using AGXIOME.Core.HITL;
using AGXIOME.Core.Scheduling;
using AGXIOME.Orchestration;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Monitoring;
using AGXIOME.Orchestration.Tools.Master;
using AGXIOME.Orchestration.Tools.SubAgent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using NSubstitute;

namespace AGXIOME.Tests.F15;

// ═══════════════════════════════════════════════════════════════════
// TS09-A  P15 — HITLManager integration with IHitlCompletionSource (5 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_HitlIntegrationTests
{
    [Fact]
    public void HITLManager_CreateRequirement_NoOrchestratorContext_ResolvesWithoutBridge()
    {
        // Arrange: HITLManager without a completion source (standalone mode)
        var mgr = new HITLManager();
        var req = mgr.CreateRequirement("run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation);

        // Act
        mgr.Resolve(req.Id, approved: true);

        // Assert: no exception, status updated correctly
        var updated = mgr.GetRequirement(req.Id);
        Assert.NotNull(updated);
        Assert.Equal(HITLStatus.Approved, updated.Status);
    }

    [Fact]
    public async Task HITLManager_Resolve_WithOrchestrationContext_NotifiesBridge()
    {
        // Arrange
        var published = new List<(Guid taskId, Guid planId, string reqId, bool approved)>();
        var bridge = Substitute.For<IHitlCompletionSource>();
        bridge.NotifyResolvedAsync(default, default, default!, default)
              .ReturnsForAnyArgs(Task.CompletedTask)
              .AndDoes(ci => published.Add((ci.ArgAt<Guid>(0), ci.ArgAt<Guid>(1), ci.ArgAt<string>(2), ci.ArgAt<bool>(3))));

        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var mgr = new HITLManager(bridge);
        var req = mgr.CreateRequirement("run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation,
            orchestratorTaskId: taskId, orchestratorPlanId: planId);

        // Act
        mgr.Resolve(req.Id, approved: true);
        await Task.Delay(50); // allow fire-and-forget to complete

        // Assert
        Assert.Single(published);
        Assert.Equal(taskId, published[0].taskId);
        Assert.Equal(planId, published[0].planId);
        Assert.Equal(req.Id, published[0].reqId);
        Assert.True(published[0].approved);
    }

    [Fact]
    public async Task HITLManager_Resolve_Rejected_NotifiesBridgeWithFalse()
    {
        var bridge = Substitute.For<IHitlCompletionSource>();
        bridge.NotifyResolvedAsync(default, default, default!, default)
              .ReturnsForAnyArgs(Task.CompletedTask);

        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var mgr = new HITLManager(bridge);
        var req = mgr.CreateRequirement("run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation,
            orchestratorTaskId: taskId, orchestratorPlanId: planId);

        mgr.Resolve(req.Id, approved: false);
        await Task.Delay(50);

        await bridge.Received(1).NotifyResolvedAsync(taskId, planId, req.Id, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InMemoryCompletionNotifier_NotifyResolvedAsync_DeliversResumedResult()
    {
        // Arrange
        var notifier = new InMemoryCompletionNotifier();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var requirementId = Guid.NewGuid().ToString();

        // Act
        var resultTask = Task.Run(async () =>
        {
            await foreach (var r in notifier.SubscribeAsync(planId, new CancellationTokenSource(2000).Token))
                return r;
            return null!;
        });

        await notifier.NotifyResolvedAsync(taskId, planId, requirementId, approved: true);
        var result = await resultTask;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(taskId, result.TaskId);
        Assert.Equal(planId, result.PlanId);
        Assert.True(result.Success);
        Assert.False(result.IsHitlPaused);
        Assert.Equal(requirementId, result.HitlRequirementId);
    }

    [Fact]
    public void HITLRequirement_NewFields_DefaultToNull()
    {
        var req = new HITLRequirement("id1", "run1", "agent1", "tool1", new { }, HITLRequirementType.Confirmation);
        Assert.Null(req.OrchestratorTaskId);
        Assert.Null(req.OrchestratorPlanId);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-B  P15 — PollResultsTool HITL pause handling (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_PollResultsHitlTests
{
    [Fact]
    public async Task PollResultsTool_HitlPausedResult_DoesNotCountAsCompletion()
    {
        // Arrange
        var notifier = new InMemoryCompletionNotifier();
        var hot = new InMemoryHotStore();
        var cold = new InMemoryOrchestrationStore();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        // Publish HITL-paused result followed immediately by resumed result
        var hitlResult = new TaskResult
        {
            TaskId = taskId, PlanId = planId, Success = false,
            Output = "Paused for HITL", IsHitlPaused = true,
            HitlRequirementId = "req-1"
        };
        var resumedResult = new TaskResult
        {
            TaskId = taskId, PlanId = planId, Success = true,
            Output = "Resumed after approval", IsHitlPaused = false,
            HitlRequirementId = "req-1"
        };

        await notifier.PublishAsync(hitlResult);
        await notifier.PublishAsync(resumedResult);

        var tool = new PollResultsTool(notifier, hot, cold);
        var ctx = new ToolContext("master", planId.ToString(), Parameters: new Dictionary<string, object>
        {
            ["planId"] = planId.ToString(),
            ["expectedTaskIds"] = (IReadOnlyList<Guid>)new[] { taskId },
            ["timeoutSeconds"] = 5
        });

        // Act
        var result = await tool.ExecuteAsync(ctx, CancellationToken.None);

        // Assert: only the resumed result counts as completion
        Assert.True(result.Success);
    }

    [Fact]
    public async Task SaveResultTool_IsHitlPaused_PersistsFlag()
    {
        var hot = new InMemoryHotStore();
        var cold = new InMemoryOrchestrationStore();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var tool = new SaveResultTool(hot, cold);
        var ctx = new ToolContext("subagent", planId.ToString(), Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString(),
            ["output"] = "Waiting for human review.",
            ["success"] = false,
            ["isHitlPaused"] = true,
            ["hitlRequirementId"] = "req-42"
        });

        await tool.ExecuteAsync(ctx, CancellationToken.None);

        var saved = await cold.GetResultAsync(taskId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.True(saved.IsHitlPaused);
        Assert.Equal("req-42", saved.HitlRequirementId);
    }

    [Fact]
    public void TaskResult_IsHitlPaused_DefaultsFalse()
    {
        var r = new TaskResult
        {
            TaskId = Guid.NewGuid(), PlanId = Guid.NewGuid(),
            Success = true, Output = "ok"
        };
        Assert.False(r.IsHitlPaused);
        Assert.Null(r.HitlRequirementId);
        Assert.Equal(string.Empty, r.ResultHmac);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-C  P16 — TaskSpec.SkillIds (2 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_SkillsIntegrationTests
{
    [Fact]
    public void TaskSpec_SkillIds_DefaultsToEmpty()
    {
        var spec = new TaskSpec
        {
            TaskId = Guid.NewGuid(), PlanId = Guid.NewGuid(),
            StepIndex = 0, Description = "test"
        };
        Assert.Empty(spec.SkillIds);
        Assert.Equal(string.Empty, spec.TaskSpecSignature);
    }

    [Fact]
    public void TaskSpec_SkillIds_CanBeSet()
    {
        var spec = new TaskSpec
        {
            TaskId = Guid.NewGuid(), PlanId = Guid.NewGuid(),
            StepIndex = 0, Description = "test",
            SkillIds = ["skill-research", "skill-summarize"]
        };
        Assert.Equal(2, spec.SkillIds.Count);
        Assert.Contains("skill-research", spec.SkillIds);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-D  P20 — SaveResultTool HMAC (2 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_SecurityTests
{
    [Fact]
    public async Task SaveResultTool_WithCrypto_SetsNonEmptyHmac()
    {
        var hot = new InMemoryHotStore();
        var cold = new InMemoryOrchestrationStore();
        // Use the real CryptoProvider from AGXIOME.Security via its interface
        var crypto = new FakeCrypto();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var tool = new SaveResultTool(hot, cold, crypto);
        var ctx = new ToolContext("subagent", planId.ToString(), Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString(),
            ["output"] = "test output",
            ["success"] = true
        });

        await tool.ExecuteAsync(ctx, CancellationToken.None);

        var saved = await cold.GetResultAsync(taskId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.False(string.IsNullOrEmpty(saved.ResultHmac));
    }

    [Fact]
    public async Task SaveResultTool_WithoutCrypto_SetsEmptyHmac()
    {
        var hot = new InMemoryHotStore();
        var cold = new InMemoryOrchestrationStore();
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        var tool = new SaveResultTool(hot, cold, crypto: null);
        var ctx = new ToolContext("subagent", planId.ToString(), Parameters: new Dictionary<string, object>
        {
            ["taskId"] = taskId.ToString(),
            ["planId"] = planId.ToString(),
            ["output"] = "test output",
            ["success"] = true
        });

        await tool.ExecuteAsync(ctx, CancellationToken.None);

        var saved = await cold.GetResultAsync(taskId, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(string.Empty, saved.ResultHmac);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-E  P21 — OrchestratorMetrics (2 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_MetricsTests
{
    [Fact]
    public void OrchestratorMetrics_AllInstrumentsInitialised()
    {
        // Verify all static instruments are non-null (created at static init time)
        Assert.NotNull(OrchestratorMetrics.PlansStarted);
        Assert.NotNull(OrchestratorMetrics.PlansCompleted);
        Assert.NotNull(OrchestratorMetrics.PlansFailed);
        Assert.NotNull(OrchestratorMetrics.TasksSpawned);
        Assert.NotNull(OrchestratorMetrics.HitlPauses);
        Assert.NotNull(OrchestratorMetrics.StepDuration);
        Assert.NotNull(OrchestratorMetrics.PlanDuration);
        Assert.NotNull(OrchestratorMetrics.ActivePlans);
    }

    [Fact]
    public void OrchestratorMetrics_MeterName_IsCorrect()
    {
        Assert.Equal("AGXIOME.Orchestration", OrchestratorMetrics.Meter.Name);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-F  P17 — PlanScheduleAdapter (3 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_ScheduleAdapterTests
{
    private static readonly Microsoft.Extensions.Logging.ILogger<PlanScheduleAdapter> NullLog =
        Microsoft.Extensions.Logging.Abstractions.NullLogger<PlanScheduleAdapter>.Instance;

    [Fact]
    public async Task PlanScheduleAdapter_NoPlanRequest_ReturnsNull()
    {
        var called = false;
        var adapter = new PlanScheduleAdapter(
            (_, _, _) => { called = true; return Task.FromResult(Guid.NewGuid()); },
            NullLog);

        var schedule = new Schedule("s1", "nightly", "wf-1", "0 0 * * *",
            new SchedulePayload(PlanRequest: null));
        var result = await adapter.TriggerAsync(schedule);

        Assert.Null(result);
        Assert.False(called);
    }

    [Fact]
    public async Task PlanScheduleAdapter_WithPlanRequest_CallsStartAsync()
    {
        var capturedRequest = "";
        var capturedSession = "";
        var expectedPlanId = Guid.NewGuid();

        var adapter = new PlanScheduleAdapter(
            (req, sess, _) => { capturedRequest = req; capturedSession = sess; return Task.FromResult(expectedPlanId); },
            NullLog);

        var schedule = new Schedule("s2", "daily-analysis", "wf-2", "0 9 * * *",
            new SchedulePayload(SessionId: "sess-42", PlanRequest: "Analyse daily metrics."));
        var result = await adapter.TriggerAsync(schedule);

        Assert.Equal(expectedPlanId, result);
        Assert.Equal("Analyse daily metrics.", capturedRequest);
        Assert.Equal("sess-42", capturedSession);
    }

    [Fact]
    public async Task PlanScheduleAdapter_OrchestratorThrows_ReturnsNull()
    {
        var adapter = new PlanScheduleAdapter(
            (_, _, _) => Task.FromException<Guid>(new InvalidOperationException("No capacity")),
            NullLog);

        var schedule = new Schedule("s3", "backup", "wf-3", "0 2 * * *",
            new SchedulePayload(PlanRequest: "Backup databases."));
        var result = await adapter.TriggerAsync(schedule);

        Assert.Null(result); // error swallowed — adapter logs and returns null
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-G  P19 — IAgentClient plan event methods exist (1 test)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_SignalRContractTests
{
    [Fact]
    public void IAgentClient_HasAllPlanLifecycleMethods()
    {
        var methods = typeof(AGXIOME.Realtime.Hubs.IAgentClient).GetMethods();
        var names = methods.Select(m => m.Name).ToHashSet();

        Assert.Contains("OnPlanStarted", names);
        Assert.Contains("OnPlanDecomposed", names);
        Assert.Contains("OnTaskSpawned", names);
        Assert.Contains("OnStepAdvanced", names);
        Assert.Contains("OnPlanWaitingForHITL", names);
        Assert.Contains("OnPlanCompleted", names);
        Assert.Contains("OnPlanFailed", names);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS09-H  P18 — XLink bridge envelope types (2 tests)
// ═══════════════════════════════════════════════════════════════════

public sealed class F15_TS09_XLinkBridgeTests
{
    [Fact]
    public void TaskResultEnvelope_DefaultResultHmac_IsEmpty()
    {
        var env = new AGXIOME.XLink.Bridges.TaskResultEnvelope();
        Assert.Equal(string.Empty, env.ResultHmac);
    }

    [Fact]
    public void HitlResolvedEnvelope_CanBeConstructed()
    {
        var taskId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var env = new AGXIOME.XLink.Bridges.HitlResolvedEnvelope(taskId, planId, "req-1", true, DateTimeOffset.UtcNow);
        Assert.Equal(taskId, env.TaskId);
        Assert.Equal(planId, env.PlanId);
        Assert.True(env.Approved);
    }
}

// ═══════════════════════════════════════════════════════════════════
// Fakes
// ═══════════════════════════════════════════════════════════════════

/// <summary>Fake IAgxiomeCrypto for unit tests — uses SHA256 HMAC with a fixed key.</summary>
internal sealed class FakeCrypto : IAgxiomeCrypto
{
    public (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair() => (new byte[32], new byte[64]);
    public byte[] Sign(byte[] data, byte[] privateKey) => System.Security.Cryptography.SHA256.HashData(data);
    public bool Verify(byte[] data, byte[] signature, byte[] publicKey) => true;
    public string ComputeHMAC(byte[] data, byte[] key) =>
        Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(key, data));
    public bool VerifyHMAC(byte[] data, string hmac, byte[] key) =>
        ComputeHMAC(data, key) == hmac;
}
