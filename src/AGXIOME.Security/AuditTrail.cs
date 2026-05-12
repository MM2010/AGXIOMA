// SA-02 — Audit Trail
// Records who performed what action on which resource and when.
// Identities are pseudonymized (SHA-256 prefix) by default.
// Designed for append-only writes; no mutation of past entries.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AGXIOME.Security.Audit;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum AuditAction
{
    AgentCreated    = 100, AgentUpdated    = 101, AgentDeleted    = 102, AgentRun        = 103,
    SessionCreated  = 200, SessionDeleted  = 201,
    TeamCreated     = 300, TeamDeleted     = 301,
    WorkflowStarted = 400, WorkflowEnded   = 401,
    KnowledgeIngest = 500, KnowledgeDelete = 501,
    AuthLogin       = 600, AuthLogout      = 601, AuthFailed      = 602,
    ApiKeyCreated   = 700, ApiKeyRevoked   = 701,
    KeyRotated      = 800,
    ConfigChanged   = 900,
    SecurityAlert   = 999
}

public enum AuditSeverity { Info = 0, Warning = 1, Error = 2, Critical = 3 }

// ─── Value Objects ────────────────────────────────────────────────────────────

/// <summary>
/// Immutable audit record. SubjectHash is pseudonymized (first 16 chars of SHA-256).
/// Never log raw subject identifiers, tokens, or secrets.
/// </summary>
public sealed record AuditEntry(
    Guid             EntryId,
    DateTimeOffset   Timestamp,
    AuditAction      Action,
    AuditSeverity    Severity,
    string           SubjectHash,    // pseudonymized — SHA-256(subjectId)[0..15]
    string           ResourceType,
    string?          ResourceId,
    string?          OutcomeDetail,  // e.g. "success", "forbidden", "invalid_input"
    IReadOnlyDictionary<string, string>? Metadata = null);

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IAuditTrail
{
    ValueTask RecordAsync(AuditEntry entry, CancellationToken ct = default);
    IAsyncEnumerable<AuditEntry> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

public sealed record AuditQuery(
    DateTimeOffset? From           = null,
    DateTimeOffset? To             = null,
    AuditAction?    Action         = null,
    AuditSeverity?  MinSeverity    = null,
    string?         SubjectHash    = null,
    string?         ResourceType   = null,
    int             MaxResults     = 200);

// ─── In-Memory Implementation ─────────────────────────────────────────────────

/// <summary>
/// Thread-safe in-memory audit trail. For production, replace with Postgres/Seq sink.
/// Entries are stored in insertion order; bounded to MaxCapacity to avoid unbounded growth.
/// </summary>
public sealed class InMemoryAuditTrail(int maxCapacity = 10_000) : IAuditTrail
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private int _count;

    public ValueTask RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        _entries.Enqueue(entry);
        // Evict oldest when capacity exceeded
        if (Interlocked.Increment(ref _count) > maxCapacity && _entries.TryDequeue(out _))
            Interlocked.Decrement(ref _count);
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<AuditEntry> QueryAsync(AuditQuery query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        int yielded = 0;
        foreach (var e in _entries)
        {
            if (ct.IsCancellationRequested) yield break;
            if (yielded >= query.MaxResults) yield break;

            if (query.From.HasValue     && e.Timestamp < query.From) continue;
            if (query.To.HasValue       && e.Timestamp > query.To) continue;
            if (query.Action.HasValue   && e.Action != query.Action) continue;
            if (query.MinSeverity.HasValue && e.Severity < query.MinSeverity) continue;
            if (query.SubjectHash is not null && e.SubjectHash != query.SubjectHash) continue;
            if (query.ResourceType is not null && e.ResourceType != query.ResourceType) continue;

            yield return e;
            yielded++;
            await Task.Yield();
        }
    }
}

// ─── Audit Logger Helper ─────────────────────────────────────────────────────

/// <summary>
/// Convenience wrapper. Pseudonymizes subject IDs before writing entries.
/// Usage: await _audit.LogAsync(AuditAction.AgentRun, AuditSeverity.Info, subjectId, "Agent", agentId)
/// </summary>
public sealed class AuditLogger(IAuditTrail trail)
{
    public async ValueTask LogAsync(
        AuditAction action,
        AuditSeverity severity,
        string subjectId,
        string resourceType,
        string? resourceId = null,
        string? outcomeDetail = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        var entry = new AuditEntry(
            EntryId:      Guid.NewGuid(),
            Timestamp:    DateTimeOffset.UtcNow,
            Action:       action,
            Severity:     severity,
            SubjectHash:  Pseudonymize(subjectId),
            ResourceType: resourceType,
            ResourceId:   resourceId,
            OutcomeDetail: outcomeDetail,
            Metadata:     metadata);
        await trail.RecordAsync(entry, ct);
    }

    /// <summary>Returns first 16 hex chars of SHA-256 of the input — stable but non-reversible.</summary>
    public static string Pseudonymize(string rawIdentity)
    {
        var bytes  = SHA256.HashData(Encoding.UTF8.GetBytes(rawIdentity));
        return Convert.ToHexString(bytes)[..16];
    }
}
