// MU-07 — Approval UI Service (HITL — Human-In-The-Loop)
// Manages approval requests raised by agents during workflow execution.
// HITL pattern: agent pauses, emits ApprovalRequest, resumes after approve/reject.

namespace AGXIOME.Runtime.UI;

// ── Domain records ────────────────────────────────────────────────────────────

public enum ApprovalStatus { Pending, Approved, Rejected, Expired }

public sealed record ApprovalRequest(
    string RequestId,
    string AgentId,
    string RunId,
    string Action,
    string Context,
    ApprovalStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record ApprovalResult(
    bool Success,
    string RequestId,
    ApprovalStatus NewStatus,
    string? Comment = null);

// ── Service contract ──────────────────────────────────────────────────────────

public interface IApprovalService
{
    /// <summary>Returns all requests with Pending status, optionally filtered by agent.</summary>
    Task<IReadOnlyList<ApprovalRequest>> GetPendingApprovalsAsync(string? agentId = null, CancellationToken ct = default);

    /// <summary>Marks a request as Approved and signals the waiting run to resume.</summary>
    Task<ApprovalResult> ApproveAsync(string requestId, string approverComment = "", CancellationToken ct = default);

    /// <summary>Marks a request as Rejected; the run receives a rejection signal.</summary>
    Task<ApprovalResult> RejectAsync(string requestId, string reason, CancellationToken ct = default);

    /// <summary>Returns a single approval request by ID, or null if not found.</summary>
    Task<ApprovalRequest?> GetApprovalRequestAsync(string requestId, CancellationToken ct = default);

    /// <summary>Returns approval history (approved + rejected), ordered by creation desc.</summary>
    Task<IReadOnlyList<ApprovalRequest>> GetApprovalHistoryAsync(string? agentId = null, int limit = 20, CancellationToken ct = default);
}

// ── In-memory implementation ──────────────────────────────────────────────────

public sealed class ApprovalService : IApprovalService
{
    // Thread-safe dictionary keyed by requestId
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ApprovalRequest> _store = new();

    /// <summary>Creates a new pending approval request (called by the agent runtime).</summary>
    public ApprovalRequest CreateRequest(string agentId, string runId, string action, string context,
        TimeSpan? ttl = null)
    {
        var req = new ApprovalRequest(
            RequestId:  Guid.NewGuid().ToString("N")[..16],
            AgentId:    agentId,
            RunId:      runId,
            Action:     action,
            Context:    context,
            Status:     ApprovalStatus.Pending,
            CreatedAt:  DateTimeOffset.UtcNow,
            ExpiresAt:  DateTimeOffset.UtcNow + (ttl ?? TimeSpan.FromMinutes(30)));
        _store[req.RequestId] = req;
        return req;
    }

    public Task<IReadOnlyList<ApprovalRequest>> GetPendingApprovalsAsync(string? agentId = null, CancellationToken ct = default)
    {
        var result = _store.Values
            .Where(r => r.Status == ApprovalStatus.Pending && (agentId is null || r.AgentId == agentId))
            .OrderBy(r => r.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<ApprovalRequest>>(result);
    }

    public Task<ApprovalResult> ApproveAsync(string requestId, string approverComment = "", CancellationToken ct = default)
        => TransitionAsync(requestId, ApprovalStatus.Approved, approverComment);

    public Task<ApprovalResult> RejectAsync(string requestId, string reason, CancellationToken ct = default)
        => TransitionAsync(requestId, ApprovalStatus.Rejected, reason);

    public Task<ApprovalRequest?> GetApprovalRequestAsync(string requestId, CancellationToken ct = default)
    {
        _store.TryGetValue(requestId, out var req);
        return Task.FromResult(req);
    }

    public Task<IReadOnlyList<ApprovalRequest>> GetApprovalHistoryAsync(string? agentId = null, int limit = 20, CancellationToken ct = default)
    {
        var result = _store.Values
            .Where(r => r.Status != ApprovalStatus.Pending && (agentId is null || r.AgentId == agentId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<ApprovalRequest>>(result);
    }

    private Task<ApprovalResult> TransitionAsync(string requestId, ApprovalStatus newStatus, string comment)
    {
        if (!_store.TryGetValue(requestId, out var req))
            return Task.FromResult(new ApprovalResult(false, requestId, ApprovalStatus.Pending, "Request not found"));

        if (req.Status != ApprovalStatus.Pending)
            return Task.FromResult(new ApprovalResult(false, requestId, req.Status, "Request is not pending"));

        var updated = req with { Status = newStatus };
        _store[requestId] = updated;
        return Task.FromResult(new ApprovalResult(true, requestId, newStatus, comment));
    }
}
