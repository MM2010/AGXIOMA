namespace AgxiomaLegalEmail.Models;

public enum PipelineState
{
    Ingested,
    Categorized,
    QueryExploded,
    RagCompleted,
    AnalysisDone,
    DraftReady,
    HitlPending,
    Approved,
    Rejected,
    ReplySent,
    AwaitingRevision,
    FollowupDone
}

public record LegalEmail(
    string Id,
    string Sender,
    string Subject,
    string Body,
    List<string>? Attachments,
    DateTime ReceivedAt,
    PipelineState State = PipelineState.Ingested,
    string? AssignedTree = null,
    string? AssignedAgent = null,
    string? DraftResponse = null,
    string? ReviewNotes = null,
    string? LawyerId = null,
    string? Priority = "medium"
);

public record LegalCase(
    string Id,
    string EmailId,
    string Area,
    string Summary,
    string Status,
    DateTime CreatedAt
);

public record LegalReview(
    string Id,
    string EmailId,
    string LawyerId,
    string Decision,
    string? Notes,
    DateTime ReviewedAt
);

public record PipelineTimelineEntry(
    string Phase,
    PipelineState State,
    string Description,
    TimeSpan? Duration,
    DateTime Timestamp
);

public record IngestionState(
    string CodeType,
    string Status,
    string? FileHash,
    int ChunkCount,
    DateTime? CompletedAt
);

public record QueryRequest(string Question, int? TopK, double? MinScore);
public record QueryResponse(string Question, string Answer, List<ChunkReference> Sources, bool ContextFound);
public record ChunkReference(string DocumentId, string Snippet, float Score);

public record IngestEmailRequest(string Sender, string Subject, string Body, List<string>? Attachments);
public record PipelineStatusResponse(string EmailId, PipelineState State, List<PipelineTimelineEntry> Timeline);

public record ReviewApproveRequest(string EmailId, string LawyerId);
public record ReviewRejectRequest(string EmailId, string LawyerId, string Notes);

public record DashboardResponse(int TotalEmails, int InHitl, int Completed, int PendingReview, double AvgResponseSeconds);
