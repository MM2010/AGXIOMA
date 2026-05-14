using AGXIOME.Orchestration.Models;

namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Master orchestrator contract: receives a user request → plans → executes → synthesises.
/// </summary>
public interface IPlanOrchestrator
{
    /// <summary>
    /// Decomposes the request, persists the plan, and starts background execution.
    /// Returns the assigned PlanId immediately — execution is async.
    /// </summary>
    Task<Guid> StartAsync(string request, string sessionId,
        OrchestratorOptions? options = null, CancellationToken ct = default);

    /// <summary>Resumes a paused or interrupted plan from its last known state.</summary>
    Task ResumeAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Pauses a running plan after the current step completes.</summary>
    Task PauseAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Cancels a plan and all active sub-agent jobs.</summary>
    Task CancelAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Returns the current plan state, or null if unknown.</summary>
    Task<PlanState?> GetStateAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Streams orchestration events for the given plan until cancellation.</summary>
    IAsyncEnumerable<OrchestratorEvent> WatchAsync(Guid planId, CancellationToken ct = default);
}

/// <summary>Global execution options for a plan run.</summary>
public record OrchestratorOptions
{
    public int MaxParallelTasks { get; init; } = 4;
    public TimeSpan DefaultTaskTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool EnableAutoResume { get; init; } = true;
    public string? PreferredLLMProvider { get; init; }
    public IReadOnlyList<string>? GlobalToolAllowlist { get; init; }
    public int MaxRetryPerTask { get; init; } = 2;
}

// ── Event hierarchy ────────────────────────────────────────────────────────

public abstract record OrchestratorEvent(Guid PlanId, DateTimeOffset Timestamp);

public sealed record PlanStartedEvent(Guid PlanId, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record PlanDecomposedEvent(Guid PlanId, int TaskCount, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record TaskSpawnedEvent(Guid PlanId, Guid TaskId, int StepIndex, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record TaskCompletedEvent(Guid PlanId, Guid TaskId, bool Success, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record StepAdvancedEvent(Guid PlanId, int OldStep, int NewStep, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record PlanPausedEvent(Guid PlanId, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

/// <summary>
/// Emitted when a task inside an orchestrated plan is paused awaiting human-in-the-loop approval.
/// The orchestrator transitions to WaitingForHITL state until IHitlCompletionSource resolves it.
/// </summary>
public sealed record TaskHitlPausedEvent(Guid PlanId, Guid TaskId, string RequirementId, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record PlanCompletedEvent(Guid PlanId, string Summary, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);

public sealed record PlanFailedEvent(Guid PlanId, string Reason, DateTimeOffset Timestamp)
    : OrchestratorEvent(PlanId, Timestamp);
