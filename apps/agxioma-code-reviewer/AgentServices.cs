namespace AgxiomaCodeReviewer;

// ── Configuration ─────────────────────────────────────────────────────────────

public record AgentSettings
{
    public string Id        { get; init; } = "MY_AGENT_NAME";
    public string? Provider { get; init; }
    public string? Model    { get; init; }
    public int MaxLoops     { get; init; } = 1;
}

// ── Requests ──────────────────────────────────────────────────────────────────

public record ReviewRequest(
    string Code,
    string? Language = null,
    string? Context  = null
);

public record ReviewFileRequest(
    string FilePath,
    string? Context = null
);

public record DiffReviewRequest(
    string Diff,
    string? PrTitle     = null,
    string? Description = null
);

// ── Response ──────────────────────────────────────────────────────────────────

public record ReviewResponse(
    string Language,
    string? Context,
    string HarperFindings,
    string BenjaminFindings,
    string LucasFindings,
    string FinalReport,
    Dictionary<string, string> PhaseTiming
);
