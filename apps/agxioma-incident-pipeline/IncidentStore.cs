using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.VectorStores;

namespace AgxiomaIncidentPipeline;

// ══════════════════════════════════════════════════════════════════════════════
// Incident domain models
// ══════════════════════════════════════════════════════════════════════════════

public enum IncidentStatus
{
    Received,
    Classifying,
    InvestigatingTools,
    Analyzing,
    GeneratingPostmortem,
    AwaitingApproval,
    Approved,
    Rejected,
    Completed
}

public enum IncidentSeverity { Sev1, Sev2, Sev3, Unknown }

public sealed class IncidentRecord
{
    public string Id               { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string RawInput         { get; set; } = string.Empty;
    public IncidentStatus Status   { get; set; } = IncidentStatus.Received;
    public IncidentSeverity Severity { get; set; } = IncidentSeverity.Unknown;
    public DateTime CreatedAt      { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt   { get; set; }

    // ── Pipeline outputs ──────────────────────────────────────────────────────
    public ClassificationResult? Classification  { get; set; }
    public ToolCollectionResult? ToolData        { get; set; }
    public InvestigationResult?  Investigation   { get; set; }
    public PostmortemResult?     Postmortem       { get; set; }

    // ── HITL ──────────────────────────────────────────────────────────────────
    public List<ProposedAction>  ProposedActions  { get; set; } = [];
    public List<ExecutedAction>  ExecutedActions  { get; set; } = [];
    public string? ApproverNote  { get; set; }

    // ── Timeline ──────────────────────────────────────────────────────────────
    public List<TimelineEntry> Timeline { get; set; } = [];

    public void AddTimeline(string phase, string detail)
        => Timeline.Add(new TimelineEntry(DateTime.UtcNow, phase, detail));
}

public record ClassificationResult(
    IncidentSeverity Severity,
    string IncidentType,
    string[] AffectedServices,
    string EscalationPath,
    string Summary
);

public record ToolCollectionResult(
    object? Logs,
    object? Metrics,
    object? K8sPods,
    object? RecentDeploys,
    object? GitCommits
);

public record InvestigationResult(
    string RootCause,
    string BlastRadius,
    string Timeline,
    string Mitigation,
    string[] RecommendedActions,
    double ConfidenceScore,
    string FullSynthesis
);

public record PostmortemResult(
    string Title,
    string MarkdownContent,
    string[] RemediationChecklist,
    string[] PreventiveActions
);

public record ProposedAction(
    string ActionId,
    string Type,        // create_ticket | update_status_page | suggest_rollback | notify_slack
    string Description,
    object? Payload
);

public record ExecutedAction(
    string ActionId,
    string Type,
    bool Success,
    object? Result,
    DateTime ExecutedAt
);

public record TimelineEntry(DateTime Ts, string Phase, string Detail);

// ══════════════════════════════════════════════════════════════════════════════
// IncidentStore — holds in-flight incidents + wraps KnowledgeBase for memory
// ══════════════════════════════════════════════════════════════════════════════

public sealed class IncidentStore
{
    private readonly ConcurrentDictionary<string, IncidentRecord> _incidents = new();
    private readonly KnowledgeBase _kb;

    public IncidentStore(KnowledgeBase kb) => _kb = kb;

    public IncidentRecord Create(string rawInput)
    {
        var record = new IncidentRecord { RawInput = rawInput };
        _incidents[record.Id] = record;
        return record;
    }

    public IncidentRecord? Get(string id)
        => _incidents.TryGetValue(id, out var r) ? r : null;

    public IReadOnlyCollection<IncidentRecord> All()
        => _incidents.Values.OrderByDescending(r => r.CreatedAt).ToList();

    /// <summary>Embed and store the postmortem into the vector knowledge base for future retrieval.</summary>
    public async Task IndexPostmortemAsync(IncidentRecord record, CancellationToken ct = default)
    {
        if (record.Postmortem is null) return;

        var text = $"""
            INCIDENT: {record.Id}
            TYPE: {record.Classification?.IncidentType}
            SEVERITY: {record.Classification?.Severity}
            ROOT CAUSE: {record.Investigation?.RootCause}
            BLAST RADIUS: {record.Investigation?.BlastRadius}
            MITIGATION: {record.Investigation?.Mitigation}

            POSTMORTEM:
            {record.Postmortem.MarkdownContent}
            """;

        var doc = new Document(
            Id:       $"incident-{record.Id}",
            Content:  text,
            Type:     DocumentType.Markdown,
            Metadata: new Dictionary<string, string>
            {
                ["incidentId"]   = record.Id,
                ["severity"]     = record.Severity.ToString(),
                ["incidentType"] = record.Classification?.IncidentType ?? "unknown",
                ["createdAt"]    = record.CreatedAt.ToString("o")
            }
        );

        await _kb.IngestAsync(doc, ct);
    }

    /// <summary>Search past postmortems for similar incidents.</summary>
    public Task<IReadOnlyList<KnowledgeChunk>> SearchSimilarAsync(string query, CancellationToken ct = default)
        => _kb.SearchAsync(query, new KnowledgeSearchOptions(TopK: 3, MinScore: 0.4f), ct);
}
