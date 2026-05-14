using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Orchestration.Monitoring;
using AGXIOME.Orchestration.Tools.Master;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Orchestration.Orchestrator;

/// <summary>
/// Master orchestrator — implements the Plan-and-Execute loop.
/// Decomposes the user request → persists plan → loops per step:
///   GenerateAgents → SpawnAgents → PollResults → AdvancePlan (repeat until Complete/Fail)
///
/// All plan execution is async and fire-and-forget from the caller's perspective.
/// Events are streamed via WatchAsync for real-time observability.
/// </summary>
public sealed class PlanOrchestrator : IPlanOrchestrator
{
    private readonly IOrchestrationStore _cold;
    private readonly IHotStore _hot;
    private readonly ICompletionNotifier _notifier;
    private readonly IAgentSpawner _spawner;
    private readonly ILLMProvider _llm;
    private readonly ILogger<PlanOrchestrator> _log;
    // P20: optional — null means crypto/audit not wired (dev/test)
    private readonly IAgxiomeCrypto? _crypto;
    private readonly IAuditLogger? _audit;

    // Per-plan event channels for WatchAsync subscribers
    private readonly ConcurrentDictionary<Guid, Channel<OrchestratorEvent>> _eventChannels = new();
    // Per-plan pause signals
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _pauseSignals = new();
    // Per-plan cancellation tokens
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _ctsSources = new();

    public PlanOrchestrator(
        IOrchestrationStore cold,
        IHotStore hot,
        ICompletionNotifier notifier,
        IAgentSpawner spawner,
        ILLMProvider llm,
        ILogger<PlanOrchestrator> log,
        IAgxiomeCrypto? crypto = null,
        IAuditLogger? audit = null)
    {
        _cold = cold;
        _hot = hot;
        _notifier = notifier;
        _spawner = spawner;
        _llm = llm;
        _log = log;
        _crypto = crypto;
        _audit = audit;
    }

    public async Task<Guid> StartAsync(string request, string sessionId,
        OrchestratorOptions? options = null, CancellationToken ct = default)
    {
        options ??= new OrchestratorOptions();
        var planId = Guid.NewGuid();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ctsSources[planId] = cts;
        _ = GetOrCreateEventChannel(planId);

        // P21: track plan start
        OrchestratorMetrics.PlansStarted.Add(1);
        OrchestratorMetrics.ActivePlans.Add(1);

        // P20: audit plan start (pseudonymised — sessionId is not a PII, planId is random)
        if (_audit is not null)
            await _audit.LogAsync(new AuditEvent(
                Id: Guid.NewGuid().ToString(),
                EventType: "orchestration",
                ActorId: sessionId,
                TargetId: planId.ToString(),
                Action: "plan.started",
                Payload: new { request_length = request.Length },
                ClientIp: null,
                Success: true,
                FailureReason: null,
                Timestamp: DateTime.UtcNow), ct);

        // Fire-and-forget — returns planId immediately
        _ = Task.Run(() => RunPlanLoopAsync(planId, request, sessionId, options, cts.Token), cts.Token);
        return planId;
    }

    public async Task ResumeAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await _cold.GetPlanAsync(planId, ct);
        if (plan is null) return;

        if (_pauseSignals.TryGetValue(planId, out var tcs))
            tcs.TrySetResult();

        if (plan.Status == OrchestratorPlanStatus.Paused)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ctsSources[planId] = cts;
            _ = Task.Run(() => ContinuePlanLoopAsync(plan, new OrchestratorOptions(), cts.Token), cts.Token);
        }
    }

    public async Task PauseAsync(Guid planId, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _pauseSignals[planId] = tcs;
        await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.Paused, ct: ct);
        await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Paused.ToString(),
            TimeSpan.FromHours(72), ct);
        await EmitAsync(planId, new PlanPausedEvent(planId, DateTimeOffset.UtcNow), ct);
    }

    public async Task CancelAsync(Guid planId, CancellationToken ct = default)
    {
        if (_ctsSources.TryGetValue(planId, out var cts))
            cts.Cancel();
        await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.Cancelled, ct: ct);
        await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Cancelled.ToString(),
            TimeSpan.FromHours(24), ct);
        if (_eventChannels.TryGetValue(planId, out var ch))
            ch.Writer.TryComplete();
    }

    public Task<PlanState?> GetStateAsync(Guid planId, CancellationToken ct = default) =>
        _cold.GetPlanAsync(planId, ct);

    public async IAsyncEnumerable<OrchestratorEvent> WatchAsync(Guid planId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ch = GetOrCreateEventChannel(planId);
        await foreach (var evt in ch.Reader.ReadAllAsync(ct))
            yield return evt;
    }

    // ── Plan loop ─────────────────────────────────────────────────────────

    private async Task RunPlanLoopAsync(Guid planId, string request, string sessionId,
        OrchestratorOptions options, CancellationToken ct)
    {
        await EmitAsync(planId, new PlanStartedEvent(planId, DateTimeOffset.UtcNow), ct);
        try
        {
            // Step 1: Decompose
            var decompTool = BuildDecomposeTool();
            var decompCtx = new ToolContext("master", planId.ToString(), Parameters: new Dictionary<string, object>
            {
                ["request"] = request,
                ["sessionId"] = sessionId,
                ["planId"] = planId.ToString(),
                ["maxTasks"] = options.GlobalToolAllowlist?.Count > 0 ? 8 : 8
            });
            var decompResult = await decompTool.ExecuteAsync(decompCtx, ct);
            if (!decompResult.Success)
            {
                await FailPlan(planId, $"Decomposition failed: {decompResult.ErrorMessage}", ct);
                return;
            }

            // Extract tasks from decompose result
            var tasks = ExtractTasksFromDecomposeResult(decompResult, planId);
            if (tasks.Count == 0)
            {
                await FailPlan(planId, "Decomposition produced zero tasks", ct);
                return;
            }

            var plan = new PlanState
            {
                PlanId = planId,
                OriginalRequest = request,
                SessionId = sessionId,
                Tasks = tasks,
                CurrentStep = 0,
                Status = OrchestratorPlanStatus.Running
            };

            // Step 2: Persist
            var persistTool = new PersistPlanTool(_hot, _cold);
            var persistCtx = new ToolContext("master", planId.ToString(), Parameters: new Dictionary<string, object>
            {
                ["plan"] = plan
            });
            await persistTool.ExecuteAsync(persistCtx, ct);

            await EmitAsync(planId, new PlanDecomposedEvent(planId, tasks.Count, DateTimeOffset.UtcNow), ct);

            // Track in session
            await _hot.SetAddAsync(HotKeys.SessionPlans(sessionId), planId.ToString(), ct);

            // Execute steps
            await ContinuePlanLoopAsync(plan, options, ct);
        }
        catch (OperationCanceledException) { /* normal cancellation */ }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception in plan loop {PlanId}", planId);
            await FailPlan(planId, ex.Message, ct);
        }
    }

    private async Task ContinuePlanLoopAsync(PlanState plan, OrchestratorOptions options, CancellationToken ct)
    {
        var planId = plan.PlanId;
        var maxStep = plan.MaxStepIndex;

        while (!ct.IsCancellationRequested)
        {
            // Reload latest plan state from cold store (source of truth)
            plan = await _cold.GetPlanAsync(planId, ct) ?? plan;

            if (plan.Status is OrchestratorPlanStatus.Completed or
                OrchestratorPlanStatus.Failed or
                OrchestratorPlanStatus.Cancelled)
                break;

            // Check pause signal
            if (_pauseSignals.TryGetValue(planId, out var pauseTcs))
            {
                await pauseTcs.Task.WaitAsync(ct);
                _pauseSignals.TryRemove(planId, out _);
            }

            var stepTasks = plan.CurrentStepTasks;
            if (stepTasks.Count == 0) break;

            // Step 3: Generate agent configs
            var genTool = new GenerateAgentsTool(_llm);
            var genCtx = MakeContext(planId, new Dictionary<string, object>
            {
                ["tasks"] = (IReadOnlyList<TaskSpec>)stepTasks,
                ["availableTools"] = (IReadOnlyList<string>)(options.GlobalToolAllowlist ?? Array.Empty<string>()),
                ["llmProviders"] = (IReadOnlyList<string>)Array.Empty<string>()
            });
            var genResult = await genTool.ExecuteAsync(genCtx, ct);

            var agentConfigs = genResult.Success && genResult.Data is { } gd
                ? ExtractAgentConfigs(gd)
                : Array.Empty<AgentConfiguration>();

            // Step 4: Spawn
            var spawnTool = new SpawnAgentTool(_spawner, _hot, _cold);
            var spawnCtx = MakeContext(planId, new Dictionary<string, object>
            {
                ["tasks"] = (IReadOnlyList<TaskSpec>)stepTasks,
                ["agentConfigurations"] = (IReadOnlyList<AgentConfiguration>)agentConfigs,
                ["executionMode"] = StepExecutionMode.Parallel
            });
            await spawnTool.ExecuteAsync(spawnCtx, ct);

            foreach (var t in stepTasks)
                await EmitAsync(planId, new TaskSpawnedEvent(planId, t.TaskId, plan.CurrentStep, DateTimeOffset.UtcNow), ct);

            // Step 5: Poll
            var pollTool = new PollResultsTool(_notifier, _hot, _cold);
            var pollCtx = MakeContext(planId, new Dictionary<string, object>
            {
                ["planId"] = planId.ToString(),
                ["expectedTaskIds"] = (IReadOnlyList<Guid>)stepTasks.Select(t => t.TaskId).ToList(),
                ["timeoutSeconds"] = (int)options.DefaultTaskTimeout.TotalSeconds
            });
            var pollResult = await pollTool.ExecuteAsync(pollCtx, ct);

            // Collect results
            var stepResults = new List<Models.TaskResult>();
            foreach (var taskSpec in stepTasks)
            {
                var r = await _cold.GetResultAsync(taskSpec.TaskId, ct);
                if (r is not null) stepResults.Add(r);
                if (r is not null)
                    await EmitAsync(planId, new TaskCompletedEvent(planId, taskSpec.TaskId, r.Success, DateTimeOffset.UtcNow), ct);
            }

            // Step 6: Advance
            var advTool = new AdvancePlanTool(_llm, _cold, _hot, _spawner);
            var advCtx = MakeContext(planId, new Dictionary<string, object>
            {
                ["planId"] = planId.ToString(),
                ["completedResults"] = (IReadOnlyList<Models.TaskResult>)stepResults,
                ["isLastStep"] = plan.IsLastStep,
                ["currentStep"] = plan.CurrentStep
            });
            var advResult = await advTool.ExecuteAsync(advCtx, ct);

            if (!advResult.Success) { await FailPlan(planId, advResult.ErrorMessage ?? "AdvancePlan failed", ct); break; }

            // Reload to get updated step
            var prevStep = plan.CurrentStep;
            plan = await _cold.GetPlanAsync(planId, ct) ?? plan;

            if (plan.CurrentStep != prevStep)
                await EmitAsync(planId, new StepAdvancedEvent(planId, prevStep, plan.CurrentStep, DateTimeOffset.UtcNow), ct);

            if (plan.Status == OrchestratorPlanStatus.Completed)
            {
                await EmitAsync(planId, new PlanCompletedEvent(planId, plan.FinalSummary ?? "", DateTimeOffset.UtcNow), ct);
                // P21: metrics
                OrchestratorMetrics.PlansCompleted.Add(1);
                OrchestratorMetrics.ActivePlans.Add(-1);
                // P20: audit
                if (_audit is not null)
                    await _audit.LogAsync(new AuditEvent(
                        Id: Guid.NewGuid().ToString(),
                        EventType: "orchestration",
                        ActorId: "system",
                        TargetId: planId.ToString(),
                        Action: "plan.completed",
                        Payload: new { summary_length = (plan.FinalSummary ?? "").Length },
                        ClientIp: null,
                        Success: true,
                        FailureReason: null,
                        Timestamp: DateTime.UtcNow), ct);
                break;
            }
            if (plan.Status == OrchestratorPlanStatus.Failed)
            {
                await EmitAsync(planId, new PlanFailedEvent(planId, "Plan marked failed by AdvancePlan", DateTimeOffset.UtcNow), ct);
                break;
            }
        }

        // Close event stream for this plan
        if (_eventChannels.TryGetValue(planId, out var ch))
            ch.Writer.TryComplete();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task FailPlan(Guid planId, string reason, CancellationToken ct)
    {
        await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.Failed, ct: ct);
        await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Failed.ToString(), TimeSpan.FromHours(24), ct);
        await EmitAsync(planId, new PlanFailedEvent(planId, reason, DateTimeOffset.UtcNow), CancellationToken.None);
        if (_eventChannels.TryGetValue(planId, out var ch)) ch.Writer.TryComplete();
        // P21: metrics
        OrchestratorMetrics.PlansFailed.Add(1);
        OrchestratorMetrics.ActivePlans.Add(-1);
        // P20: audit
        if (_audit is not null)
            await _audit.LogAsync(new AuditEvent(
                Id: Guid.NewGuid().ToString(),
                EventType: "orchestration",
                ActorId: "system",
                TargetId: planId.ToString(),
                Action: "plan.failed",
                Payload: new { reason },
                ClientIp: null,
                Success: false,
                FailureReason: reason,
                Timestamp: DateTime.UtcNow), CancellationToken.None);
    }

    private async Task EmitAsync(Guid planId, OrchestratorEvent evt, CancellationToken ct)
    {
        if (_eventChannels.TryGetValue(planId, out var ch))
            await ch.Writer.WriteAsync(evt, ct);
    }

    private Channel<OrchestratorEvent> GetOrCreateEventChannel(Guid planId) =>
        _eventChannels.GetOrAdd(planId, _ => Channel.CreateUnbounded<OrchestratorEvent>());

    private static ToolContext MakeContext(Guid planId, Dictionary<string, object> parameters) =>
        new("master", planId.ToString(), Parameters: parameters);

    private DecomposeRequestTool BuildDecomposeTool() => new(_llm);

    private static List<TaskSpec> ExtractTasksFromDecomposeResult(ToolResult result, Guid planId)
    {
        // The data contains an anonymous object; we re-serialise/parse for safety
        if (result.Data is null) return new List<TaskSpec>();
        var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tasks", out var tasksEl)) return new List<TaskSpec>();

        return System.Text.Json.JsonSerializer.Deserialize<List<TaskSpec>>(tasksEl.GetRawText())
               ?? new List<TaskSpec>();
    }

    private static IReadOnlyList<AgentConfiguration> ExtractAgentConfigs(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("agentConfigurations", out var el))
            return Array.Empty<AgentConfiguration>();
        return System.Text.Json.JsonSerializer.Deserialize<List<AgentConfiguration>>(el.GetRawText())
               ?? new List<AgentConfiguration>();
    }
}
