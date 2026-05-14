namespace AGXIOME.Orchestration.Models;

/// <summary>
/// Immutable specification of a task assigned by the Master to a sub-agent.
/// Everything a sub-agent needs is self-contained here — no external dependencies.
/// </summary>
public sealed record TaskSpec
{
    public required Guid TaskId { get; init; }
    public required Guid PlanId { get; init; }
    public required int StepIndex { get; init; }

    /// <summary>Natural-language description — used directly as the sub-agent prompt.</summary>
    public required string Description { get; init; }

    /// <summary>Synthesized context from prior steps, injected by AdvancePlanTool.</summary>
    public string? PriorContext { get; init; }

    /// <summary>Exact allowlist of tool names the sub-agent may invoke.</summary>
    public IReadOnlyList<string> AllowedToolNames { get; init; } = [];

    // ── LLM parameters calibrated per task type ────────────────────────────
    public float Temperature { get; init; } = 0.7f;
    public int? TopK { get; init; }
    public float? TopP { get; init; }
    public int MaxTokens { get; init; } = 4096;
    public int MaxIterations { get; init; } = 10;

    /// <summary>Preferred LLM provider shorthand (e.g. "openai:gpt-4o"). null = system default.</summary>
    public string? PreferredLLMProvider { get; init; }

    /// <summary>System-level instructions for the sub-agent role (produced by GenerateAgentsTool).</summary>
    public string? SystemPrompt { get; init; }

    /// <summary>Arbitrary metadata the Master can forward to the sub-agent.</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    public OrchestratorTaskStatus Status { get; init; } = OrchestratorTaskStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; init; }
    public TimeSpan? Timeout { get; init; }

    // ── P16: Skill binding ─────────────────────────────────────────────────

    /// <summary>
    /// Skill IDs to apply to the spawned sub-agent before execution.
    /// SpawnAgentTool calls ISkillManager.ApplySkillToAgent for each ID.
    /// Empty list means no extra skills beyond the agent's defaults.
    /// </summary>
    public IReadOnlyList<string> SkillIds { get; init; } = [];

    // ── P20: Integrity / audit ─────────────────────────────────────────────

    /// <summary>
    /// ECDSA/HMAC signature of the serialized TaskSpec, computed by SpawnAgentTool using IAgxiomeCrypto.
    /// Empty string when crypto is not wired (dev/test). Verified server-side before dispatch.
    /// </summary>
    public string TaskSpecSignature { get; init; } = string.Empty;
}
