using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.XLink.Bridges;

/// <summary>
/// P18: XLink-backed ICompletionNotifier + IHitlCompletionSource for distributed scenarios.
/// Publishes TaskResult payloads over the XLink routing bus (HMAC-signed envelopes).
/// Enables multi-node orchestration where sub-agents run on remote processes.
///
/// Topic convention:
///   Publish:  "orchestration:results:{planId}"
///   Subscribe: "orchestration:results:{planId}"
///   HITL resolved: "orchestration:hitl:resolved"
///
/// Registration (host DI — AGXIOME.XLink referenced by host):
///   services.AddSingleton&lt;XLinkOrchestrationBridge&gt;();
///   services.AddSingleton&lt;ICompletionNotifier&gt;(sp => sp.GetRequiredService&lt;XLinkOrchestrationBridge&gt;());
///   services.AddSingleton&lt;IHitlCompletionSource&gt;(sp => sp.GetRequiredService&lt;XLinkOrchestrationBridge&gt;());
/// </summary>
public sealed class XLinkOrchestrationBridge : IXLinkCompletionNotifier, IHitlCompletionSource
{
    private readonly IXLinkRouter _router;
    // In-process channels mirror local notifier — subscriber loop feeds from XLink topic.
    private readonly ConcurrentDictionary<Guid, Channel<TaskResultEnvelope>> _channels = new();

    public XLinkOrchestrationBridge(IXLinkRouter router)
    {
        _router = router;
        // Subscribe globally — filter per-plan in SubscribeAsync
        _router.Subscribe<TaskResultEnvelope>("orchestration:results", OnResultReceived);
        _router.Subscribe<HitlResolvedEnvelope>("orchestration:hitl:resolved", OnHitlResolved);
    }

    // ── IXLinkCompletionNotifier (extends ICompletionNotifier concept for XLink) ──

    public async Task PublishAsync(TaskResultEnvelope result, CancellationToken ct = default)
    {
        // Publish to XLink bus — remote subscribers receive it; local channel also fed via subscription
        await _router.PublishAsync($"orchestration:results:{result.PlanId}", result, new XLinkOptions
        {
            RequireChecksum = true
        });
    }

    public async IAsyncEnumerable<TaskResultEnvelope> SubscribeAsync(Guid planId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ch = GetOrCreate(planId);
        await foreach (var result in ch.Reader.ReadAllAsync(ct))
            yield return result;
    }

    // ── IHitlCompletionSource (P15) ──────────────────────────────────────

    public async Task NotifyResolvedAsync(Guid taskId, Guid planId, string requirementId, bool approved,
        CancellationToken ct = default)
    {
        var envelope = new HitlResolvedEnvelope(taskId, planId, requirementId, approved, DateTimeOffset.UtcNow);
        await _router.PublishAsync("orchestration:hitl:resolved", envelope, new XLinkOptions
        {
            RequireChecksum = true
        });
    }

    // ── Internal subscription handlers ───────────────────────────────────

    private Task OnResultReceived(TaskResultEnvelope result)
    {
        var ch = GetOrCreate(result.PlanId);
        ch.Writer.TryWrite(result);
        return Task.CompletedTask;
    }

    private Task OnHitlResolved(HitlResolvedEnvelope env)
    {
        // Synthesise a resumed result and deliver into the plan channel
        var resumed = new TaskResultEnvelope
        {
            TaskId = env.TaskId,
            PlanId = env.PlanId,
            Success = env.Approved,
            Output = env.Approved ? "HITL approved — task resumed via XLink." : "HITL rejected.",
            IsHitlPaused = false,
            HitlRequirementId = env.RequirementId,
            CompletedAt = DateTimeOffset.UtcNow
        };
        var ch = GetOrCreate(env.PlanId);
        ch.Writer.TryWrite(resumed);
        return Task.CompletedTask;
    }

    private Channel<TaskResultEnvelope> GetOrCreate(Guid planId) =>
        _channels.GetOrAdd(planId, _ => Channel.CreateUnbounded<TaskResultEnvelope>());
}

/// <summary>
/// XLink-specific transport envelope for TaskResult.
/// Matches the fields required by ICompletionNotifier but serialisable without AGXIOME.Orchestration reference.
/// The host-side adapter maps this to the real TaskResult record.
/// </summary>
public sealed record TaskResultEnvelope
{
    public Guid TaskId { get; init; }
    public Guid PlanId { get; init; }
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsHitlPaused { get; init; }
    public string? HitlRequirementId { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public string ResultHmac { get; init; } = string.Empty;
}

/// <summary>Transport envelope for HITL resolution events over XLink bus.</summary>
public sealed record HitlResolvedEnvelope(
    Guid TaskId,
    Guid PlanId,
    string RequirementId,
    bool Approved,
    DateTimeOffset ResolvedAt
);

/// <summary>
/// Marker interface so the host can resolve XLinkOrchestrationBridge without knowing
/// whether it acts as ICompletionNotifier (requires AGXIOME.Orchestration) or transport-only.
/// </summary>
public interface IXLinkCompletionNotifier
{
    Task PublishAsync(TaskResultEnvelope result, CancellationToken ct = default);
    IAsyncEnumerable<TaskResultEnvelope> SubscribeAsync(Guid planId, CancellationToken ct = default);
}
