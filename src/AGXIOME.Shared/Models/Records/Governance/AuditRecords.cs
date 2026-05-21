namespace AGXIOME.Shared.Models;

/// <summary>An immutable audit event recording a security or compliance-relevant action.</summary>
public record AuditEvent(
    string Id,
    string EventType,
    string ActorId,
    string? TargetId,
    string Action,
    object? Payload,
    string? ClientIp,
    bool Success,
    string? FailureReason,
    DateTime Timestamp
);

/// <summary>Query parameters for filtering the audit event log.</summary>
public record AuditQuery(
    string? EventType = null,
    string? ActorId = null,
    DateTime? From = null,
    DateTime? To = null,
    int Limit = 100
);
