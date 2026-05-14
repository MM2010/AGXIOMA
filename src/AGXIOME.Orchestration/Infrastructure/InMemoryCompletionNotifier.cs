using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Orchestration.Infrastructure;

/// <summary>
/// In-memory ICompletionNotifier backed by per-plan unbounded channels.
/// Sub-agents publish; the Master subscribes per plan.
/// Thread-safe. Intended for tests and local dev.
///
/// Also implements IHitlCompletionSource (P15): when HITLManager resolves a paused task,
/// it calls NotifyResolvedAsync() which synthesises a resumed TaskResult and re-publishes it
/// into the plan channel so PollResultsTool can continue.
/// </summary>
public sealed class InMemoryCompletionNotifier : ICompletionNotifier, IHitlCompletionSource
{
    private readonly ConcurrentDictionary<Guid, Channel<TaskResult>> _channels = new();

    private Channel<TaskResult> GetOrCreate(Guid planId) =>
        _channels.GetOrAdd(planId, _ => Channel.CreateUnbounded<TaskResult>());

    public async Task PublishAsync(TaskResult result, CancellationToken ct = default)
    {
        var ch = GetOrCreate(result.PlanId);
        await ch.Writer.WriteAsync(result, ct);
    }

    public async IAsyncEnumerable<TaskResult> SubscribeAsync(Guid planId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ch = GetOrCreate(planId);
        await foreach (var result in ch.Reader.ReadAllAsync(ct))
            yield return result;
    }

    /// <summary>
    /// Called by HITLManager.Resolve() after a HITL requirement is approved or rejected.
    /// Synthesises a TaskResult that resumes (or fails) the paused orchestrator task.
    /// The plan's PollResultsTool subscriber picks it up immediately.
    /// </summary>
    public async Task NotifyResolvedAsync(Guid taskId, Guid planId, string requirementId, bool approved,
        CancellationToken ct = default)
    {
        var resumed = new TaskResult
        {
            TaskId = taskId,
            PlanId = planId,
            Success = approved,
            Output = approved ? "HITL requirement approved — task resumed." : "HITL requirement rejected — task failed.",
            Summary = approved ? "Resumed after HITL approval." : "Failed: HITL rejected.",
            IsHitlPaused = false,
            HitlRequirementId = requirementId,
            ErrorMessage = approved ? null : "HITL requirement was rejected by operator.",
            CompletedAt = DateTimeOffset.UtcNow
        };
        await PublishAsync(resumed, ct);
    }

    /// <summary>
    /// Closes the channel for a plan — signals end-of-stream to all active subscribers.
    /// Call this after the plan reaches a terminal state.
    /// </summary>
    public void Complete(Guid planId)
    {
        if (_channels.TryGetValue(planId, out var ch))
            ch.Writer.TryComplete();
    }

    /// <summary>Clears all channels — for test isolation.</summary>
    public void Clear()
    {
        foreach (var ch in _channels.Values)
            ch.Writer.TryComplete();
        _channels.Clear();
    }
}
