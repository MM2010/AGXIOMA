using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-06 — Workflow HITL (Human-In-The-Loop)
//
// Provides pause/resume of workflow execution awaiting human approval or input.
//
// Design:
//   • WorkflowPauseRecord — serializable pause checkpoint capturing which step
//     triggered the pause, the pending requirement, and optional context snapshot.
//   • IWorkflowHitlStore — typed contract:
//       PauseAsync   — persist a pause checkpoint; emits a pending requirement
//       ResumeAsync  — record approval/rejection + unblock the checkpoint
//       GetPendingAsync — list all open (Pending) checkpoints for a workflow
//       GetAsync     — retrieve a specific checkpoint by ID
//       ExpireAsync  — mark timed-out checkpoints as Expired
//   • WorkflowHitlStore — backed by ICommandStore + in-memory index.
//
// Security:
//   • Resolution payloads are stored pseudonymized (resolverId handled upstream).
//   • Checkpoint IDs are server-generated (Guid.CreateVersion7).
//   • Approval resolution must be authorized server-side before calling ResumeAsync.
// ─────────────────────────────────────────────────────────────────────────────

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>Serializable pause checkpoint for a workflow HITL step.</summary>
public record WorkflowPauseRecord
{
    /// <summary>Unique checkpoint ID (server-generated).</summary>
    public string CheckpointId { get; init; } = string.Empty;

    /// <summary>ID of the paused workflow run.</summary>
    public string RunId        { get; init; } = string.Empty;

    /// <summary>ID of the workflow definition.</summary>
    public string WorkflowId   { get; init; } = string.Empty;

    /// <summary>Name of the step that triggered the pause.</summary>
    public string StepName     { get; init; } = string.Empty;

    /// <summary>Type of human interaction required.</summary>
    public HITLRequirementType RequirementType { get; init; }

    /// <summary>Current status of this checkpoint.</summary>
    public HITLStatus Status   { get; init; } = HITLStatus.Pending;

    /// <summary>Serialized payload the approver must act on.</summary>
    public string PayloadJson  { get; init; } = "{}";

    /// <summary>Resolution input provided by the approver (null until resolved).</summary>
    public string? ResolutionJson { get; init; }

    /// <summary>Pseudonymized resolver identifier (filled on resolution).</summary>
    public string? ResolverId  { get; init; }

    public DateTimeOffset CreatedAt   { get; init; }
    public DateTimeOffset? ExpiresAt  { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }

    public string SchemaVersion { get; init; } = "1";
}

/// <summary>Input for resolving (approving or rejecting) a HITL checkpoint.</summary>
public record HitlResolution
{
    public bool   Approved    { get; init; }
    public string? Notes      { get; init; }
    public object? InputValue { get; init; }

    // Pseudonymized on write — upstream caller strips/hashes real identity.
    public string? ResolverId { get; init; }
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Storage contract for workflow HITL pause/resume checkpoints.</summary>
public interface IWorkflowHitlStore
{
    /// <summary>Persist a new pause checkpoint (status = Pending).</summary>
    Task<WorkflowPauseRecord> PauseAsync(
        string runId, string workflowId, string stepName,
        HITLRequirementType requirementType,
        object payload,
        TimeSpan? expiry = null,
        CancellationToken ct = default);

    /// <summary>Record the human resolution and mark the checkpoint as Approved/Rejected.</summary>
    Task<WorkflowPauseRecord> ResumeAsync(
        string checkpointId, HitlResolution resolution, CancellationToken ct = default);

    /// <summary>Retrieve a specific checkpoint by ID. Returns null if not found.</summary>
    Task<WorkflowPauseRecord?> GetAsync(string checkpointId, CancellationToken ct = default);

    /// <summary>List all Pending checkpoints for a given workflow run.</summary>
    Task<IReadOnlyList<WorkflowPauseRecord>> GetPendingAsync(
        string runId, CancellationToken ct = default);

    /// <summary>Mark all checkpoints past their ExpiresAt as Expired.</summary>
    Task<int> ExpireAsync(CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// HITL checkpoint store backed by <see cref="ICommandStore"/> and an in-memory index.
/// Suitable for single-process deployments; for distributed use add a Redis or DB projection.
/// </summary>
public sealed class WorkflowHitlStore : IWorkflowHitlStore
{
    private readonly ICommandStore _commands;

    // in-memory index: checkpointId → record
    private readonly Dictionary<string, WorkflowPauseRecord> _byId = new();

    // secondary index: runId → list of checkpointIds
    private readonly Dictionary<string, List<string>> _byRun = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    public WorkflowHitlStore(ICommandStore commandStore)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        _commands = commandStore;
    }

    // ── IWorkflowHitlStore ────────────────────────────────────────────────────

    public async Task<WorkflowPauseRecord> PauseAsync(
        string runId, string workflowId, string stepName,
        HITLRequirementType requirementType,
        object payload,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        ArgumentNullException.ThrowIfNull(payload);

        var now    = DateTimeOffset.UtcNow;
        var record = new WorkflowPauseRecord
        {
            CheckpointId    = Guid.CreateVersion7().ToString(),
            RunId           = runId,
            WorkflowId      = workflowId,
            StepName        = stepName,
            RequirementType = requirementType,
            Status          = HITLStatus.Pending,
            PayloadJson     = JsonSerializer.Serialize(payload, JsonOpts),
            CreatedAt       = now,
            ExpiresAt       = expiry.HasValue ? now.Add(expiry.Value) : null,
        };

        await _commands.SaveAsync(record, ct);
        _byId[record.CheckpointId] = record;

        if (!_byRun.TryGetValue(runId, out var list))
        {
            list = new List<string>();
            _byRun[runId] = list;
        }
        list.Add(record.CheckpointId);

        return record;
    }

    public async Task<WorkflowPauseRecord> ResumeAsync(
        string checkpointId, HitlResolution resolution, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        ArgumentNullException.ThrowIfNull(resolution);

        if (!_byId.TryGetValue(checkpointId, out var existing))
            throw new KeyNotFoundException($"Checkpoint '{checkpointId}' not found.");

        if (existing.Status != HITLStatus.Pending)
            throw new InvalidOperationException(
                $"Checkpoint '{checkpointId}' is already {existing.Status}.");

        var updated = existing with
        {
            Status        = resolution.Approved ? HITLStatus.Approved : HITLStatus.Rejected,
            ResolutionJson = resolution.InputValue is not null
                                 ? JsonSerializer.Serialize(resolution.InputValue, JsonOpts)
                                 : resolution.Notes,
            ResolverId    = resolution.ResolverId,
            ResolvedAt    = DateTimeOffset.UtcNow,
        };

        await _commands.UpdateAsync(updated, ct);
        _byId[checkpointId] = updated;

        return updated;
    }

    public Task<WorkflowPauseRecord?> GetAsync(string checkpointId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        _byId.TryGetValue(checkpointId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<WorkflowPauseRecord>> GetPendingAsync(
        string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);

        if (!_byRun.TryGetValue(runId, out var ids))
            return Task.FromResult<IReadOnlyList<WorkflowPauseRecord>>(Array.Empty<WorkflowPauseRecord>());

        var result = ids
            .Select(id => _byId[id])
            .Where(r => r.Status == HITLStatus.Pending)
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowPauseRecord>>(result);
    }

    public async Task<int> ExpireAsync(CancellationToken ct = default)
    {
        var now     = DateTimeOffset.UtcNow;
        var expired = _byId.Values
            .Where(r => r.Status == HITLStatus.Pending
                     && r.ExpiresAt.HasValue
                     && r.ExpiresAt.Value <= now)
            .ToList();

        foreach (var record in expired)
        {
            var updated = record with { Status = HITLStatus.Expired };
            await _commands.UpdateAsync(updated, ct);
            _byId[record.CheckpointId] = updated;
        }

        return expired.Count;
    }
}
