namespace AGXIOME.Orchestration.Models;

/// <summary>
/// Immutable result produced by a sub-agent at task completion.
/// Serialised to JSON for both hot store and canonical store.
/// </summary>
public sealed record TaskResult
{
    public required Guid TaskId { get; init; }
    public required Guid PlanId { get; init; }
    public required bool Success { get; init; }

    /// <summary>Raw output of the sub-agent (free-text or structured JSON).</summary>
    public required string Output { get; init; }

    /// <summary>Short synthesis — used by AdvancePlanTool to build PriorContext for subsequent steps.</summary>
    public string? Summary { get; init; }

    /// <summary>Optional typed data the sub-agent exports for downstream tasks.</summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    public string? ErrorMessage { get; init; }
    public string? ErrorType { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.UtcNow;
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>Token consumption for budget tracking.</summary>
    public int? TokensUsed { get; init; }

    // ── P15: HITL pause support ────────────────────────────────────────────

    /// <summary>
    /// True when the sub-agent published this result to signal a HITL pause, not a real completion.
    /// PollResultsTool transitions the plan to WaitingForHITL when it receives this.
    /// After HITL resolution, IHitlCompletionSource publishes a resumed result with IsHitlPaused=false.
    /// </summary>
    public bool IsHitlPaused { get; init; }

    /// <summary>HITL requirement ID that caused the pause. Null when IsHitlPaused=false.</summary>
    public string? HitlRequirementId { get; init; }

    // ── P20: Integrity / audit ─────────────────────────────────────────────

    /// <summary>
    /// HMAC-SHA256 hex of the canonical result payload, computed by SaveResultTool using IAgxiomeCrypto.
    /// Empty string when crypto is not wired (dev/test). Verified by Replay Worker.
    /// </summary>
    public string ResultHmac { get; init; } = string.Empty;
}
