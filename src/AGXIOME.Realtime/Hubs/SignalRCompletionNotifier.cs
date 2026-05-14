using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AGXIOME.Realtime.Hubs;

/// <summary>
/// P19: SignalR-backed ICompletionNotifier.
/// Sub-agents publish TaskResults; the Master subscribes per plan via in-process channel.
/// Simultaneously fans out orchestrator lifecycle events to SignalR plan groups so
/// connected front-end clients receive real-time progress without polling.
///
/// Also implements IHitlCompletionSource (P15) so HITLManager.Resolve() can push
/// resumption events through the same SignalR channel.
///
/// Registration: services.AddSingleton&lt;SignalRCompletionNotifier&gt;()
///               + override ICompletionNotifier + IHitlCompletionSource in DI.
/// </summary>
public sealed class SignalRCompletionNotifier : ICompletionNotifier, IHitlCompletionSource
{
    // In-process channels mirror InMemoryCompletionNotifier for the orchestration polling loop.
    // SignalR fan-out is additive — clients get events; Master gets results from channels.
    private readonly ConcurrentDictionary<Guid, Channel<TaskResult>> _channels = new();
    private readonly IHubContext<AgentHub, IAgentClient> _hub;

    public SignalRCompletionNotifier(IHubContext<AgentHub, IAgentClient> hub)
    {
        _hub = hub;
    }

    private Channel<TaskResult> GetOrCreate(Guid planId) =>
        _channels.GetOrAdd(planId, _ => Channel.CreateUnbounded<TaskResult>());

    // ── ICompletionNotifier ───────────────────────────────────────────────

    public async Task PublishAsync(TaskResult result, CancellationToken ct = default)
    {
        // Write to in-process channel for Master polling loop
        var ch = GetOrCreate(result.PlanId);
        await ch.Writer.WriteAsync(result, ct);

        // Fan out to SignalR group so connected clients know the task status
        var group = _hub.Clients.Group($"plan:{result.PlanId}");
        if (result.IsHitlPaused && result.HitlRequirementId is not null)
        {
            await group.OnPlanWaitingForHITL(
                result.PlanId.ToString(),
                result.TaskId.ToString(),
                result.HitlRequirementId);
        }
        // Task-level completion is surfaced by the orchestrator via WatchAsync events;
        // no duplicate OnTaskSpawned/Completed pushed here to avoid double-counting.
    }

    public async IAsyncEnumerable<TaskResult> SubscribeAsync(Guid planId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ch = GetOrCreate(planId);
        await foreach (var result in ch.Reader.ReadAllAsync(ct))
            yield return result;
    }

    // ── IHitlCompletionSource (P15) ───────────────────────────────────────

    /// <summary>
    /// Resumes a paused orchestrator task after HITL approval/rejection.
    /// Publishes a synthetic TaskResult into the plan channel and pushes a
    /// OnPlanWaitingForHITL → OnPlanStarted sequence to connected clients.
    /// </summary>
    public async Task NotifyResolvedAsync(Guid taskId, Guid planId, string requirementId, bool approved,
        CancellationToken ct = default)
    {
        var resumed = new TaskResult
        {
            TaskId = taskId,
            PlanId = planId,
            Success = approved,
            Output = approved ? "HITL requirement approved — task resumed." : "HITL requirement rejected.",
            Summary = approved ? "Resumed after HITL approval." : "Failed: HITL rejected.",
            IsHitlPaused = false,
            HitlRequirementId = requirementId,
            ErrorMessage = approved ? null : "HITL requirement was rejected by operator.",
            CompletedAt = DateTimeOffset.UtcNow
        };

        // Push into the orchestration channel so PollResultsTool picks it up
        await PublishAsync(resumed, ct);
    }

    /// <summary>
    /// Pushes a plan lifecycle event to the SignalR group for the given plan.
    /// Called by OrchestratorHub when it processes WatchAsync events.
    /// </summary>
    public async Task BroadcastPlanEventAsync(OrchestratorEvent evt, CancellationToken ct = default)
    {
        var group = _hub.Clients.Group($"plan:{evt.PlanId}");
        switch (evt)
        {
            case PlanStartedEvent:
                await group.OnPlanStarted(evt.PlanId.ToString());
                break;
            case PlanDecomposedEvent d:
                await group.OnPlanDecomposed(d.PlanId.ToString(), d.TaskCount);
                break;
            case TaskSpawnedEvent s:
                await group.OnTaskSpawned(s.PlanId.ToString(), s.TaskId.ToString(), s.StepIndex);
                break;
            case StepAdvancedEvent a:
                await group.OnStepAdvanced(a.PlanId.ToString(), a.OldStep, a.NewStep);
                break;
            case TaskHitlPausedEvent h:
                await group.OnPlanWaitingForHITL(h.PlanId.ToString(), h.TaskId.ToString(), h.RequirementId);
                break;
            case PlanCompletedEvent c:
                await group.OnPlanCompleted(c.PlanId.ToString(), c.Summary);
                // Close channel — no more results expected
                if (_channels.TryGetValue(c.PlanId, out var ch))
                    ch.Writer.TryComplete();
                break;
            case PlanFailedEvent f:
                await group.OnPlanFailed(f.PlanId.ToString(), f.Reason);
                if (_channels.TryGetValue(f.PlanId, out var fch))
                    fch.Writer.TryComplete();
                break;
        }
    }
}
