using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Workflow;

// ─────────────────────────────────────────────────────────────────────────────
// SS-04 — Workflow History
//
// Persists execution runs of workflows including per-step results.
// Design:
//   • WorkflowHistoryEntry — flat DTO, one row per execution run.
//     StepResults and Metadata serialized to JSON blobs (stable across schema evolution).
//   • IWorkflowHistoryStore — typed contract:
//       AppendAsync  — record a completed execution
//       GetAsync     — retrieve a specific run by runId
//       ListByWorkflowAsync — chronological run list for a workflow
//       GetStepAsync — extract a single step result from a stored run
//       DeleteAsync  — remove a run record
//   • WorkflowHistoryStore — backed by ICommandStore + in-memory index.
//   • WorkflowHistoryEntry uses WorkflowStatus enum — no magic strings.
//
// Security note: runIds must be server-generated (Guid.CreateVersion7).
// ─────────────────────────────────────────────────────────────────────────────

// ── Storage DTO ──────────────────────────────────────────────────────────────

/// <summary>Flat DTO representing one workflow execution run.</summary>
public record WorkflowHistoryEntry
{
    /// <summary>Unique run ID (server-generated, stable across retries).</summary>
    public string RunId        { get; init; } = string.Empty;

    /// <summary>ID of the workflow definition that produced this run.</summary>
    public string WorkflowId   { get; init; } = string.Empty;

    public WorkflowStatus Status { get; init; }

    /// <summary>Serialized IReadOnlyList&lt;StepResult&gt; as JSON.</summary>
    public string StepResultsJson  { get; init; } = "[]";

    /// <summary>Serialized WorkflowMetadata as JSON (nullable).</summary>
    public string? MetadataJson { get; init; }

    public DateTimeOffset StartedAt  { get; init; }
    public DateTimeOffset FinishedAt { get; init; }

    /// <summary>Optional session that triggered this run.</summary>
    public string? SessionId { get; init; }

    /// <summary>Optional caller identity for audit trail (pseudonymized at logging layer).</summary>
    public string? UserId { get; init; }

    public string SchemaVersion { get; init; } = "1";
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Typed storage contract for workflow execution history.</summary>
public interface IWorkflowHistoryStore
{
    /// <summary>Append a completed workflow run to history.</summary>
    Task AppendAsync(WorkflowHistoryEntry entry, CancellationToken ct = default);

    /// <summary>Retrieve a specific run by its ID. Returns null if not found.</summary>
    Task<WorkflowHistoryEntry?> GetAsync(string runId, CancellationToken ct = default);

    /// <summary>
    /// List execution runs for a given workflow in reverse-chronological order.
    /// Returns at most <paramref name="limit"/> entries.
    /// </summary>
    Task<IReadOnlyList<WorkflowHistoryEntry>> ListByWorkflowAsync(
        string workflowId, int limit = 50, CancellationToken ct = default);

    /// <summary>
    /// Extract a single step result from a stored run.
    /// Returns null if the run or step is not found.
    /// </summary>
    Task<StepResult?> GetStepAsync(string runId, string stepName, CancellationToken ct = default);

    /// <summary>Delete a run record. No-op if not found.</summary>
    Task DeleteAsync(string runId, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Workflow history store backed by any <see cref="ICommandStore"/> and an in-memory
/// index for fast lookups. Suitable for single-process deployments.
/// </summary>
public sealed class WorkflowHistoryStore : IWorkflowHistoryStore
{
    private readonly ICommandStore _commands;

    // In-memory index: runId → entry (full payload cached to enable GetStepAsync without
    // a round-trip to the backing store — acceptable for single-process deployments).
    private readonly Dictionary<string, WorkflowHistoryEntry> _byRunId = new();

    // Secondary index: workflowId → ordered list of runIds (newest-first for ListByWorkflow).
    private readonly Dictionary<string, List<string>> _byWorkflow = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    public WorkflowHistoryStore(ICommandStore commandStore)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        _commands = commandStore;
    }

    // ── IWorkflowHistoryStore ─────────────────────────────────────────────────

    public async Task AppendAsync(WorkflowHistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.RunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.WorkflowId);

        await _commands.SaveAsync(entry, ct);

        _byRunId[entry.RunId] = entry;

        if (!_byWorkflow.TryGetValue(entry.WorkflowId, out var runs))
        {
            runs = new List<string>();
            _byWorkflow[entry.WorkflowId] = runs;
        }
        // Newest first: insert at position 0.
        runs.Insert(0, entry.RunId);
    }

    public Task<WorkflowHistoryEntry?> GetAsync(string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        _byRunId.TryGetValue(runId, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IReadOnlyList<WorkflowHistoryEntry>> ListByWorkflowAsync(
        string workflowId, int limit = 50, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);

        if (!_byWorkflow.TryGetValue(workflowId, out var runIds))
            return Task.FromResult<IReadOnlyList<WorkflowHistoryEntry>>(Array.Empty<WorkflowHistoryEntry>());

        var result = runIds
            .Take(limit)
            .Select(id => _byRunId[id])
            .ToList();

        return Task.FromResult<IReadOnlyList<WorkflowHistoryEntry>>(result);
    }

    public Task<StepResult?> GetStepAsync(string runId, string stepName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);

        if (!_byRunId.TryGetValue(runId, out var entry))
            return Task.FromResult<StepResult?>(null);

        // Deserialize step results on demand — avoid keeping deserialized lists in memory.
        var steps = JsonSerializer.Deserialize<List<StepResult>>(entry.StepResultsJson, JsonOpts);
        var step  = steps?.FirstOrDefault(s => s.StepName == stepName);
        return Task.FromResult(step);
    }

    public async Task DeleteAsync(string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        await _commands.DeleteAsync<WorkflowHistoryEntry>(runId, ct);

        if (_byRunId.TryGetValue(runId, out var entry))
        {
            _byRunId.Remove(runId);
            if (_byWorkflow.TryGetValue(entry.WorkflowId, out var runs))
                runs.Remove(runId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Build a <see cref="WorkflowHistoryEntry"/> from a <see cref="WorkflowResult"/>.
    /// Call this helper on the server side after each workflow execution completes.
    /// </summary>
    public static WorkflowHistoryEntry FromResult(
        WorkflowResult result,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        string? sessionId = null,
        string? userId    = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new WorkflowHistoryEntry
        {
            RunId           = Guid.CreateVersion7().ToString(),
            WorkflowId      = result.WorkflowId,
            Status          = result.Status,
            StepResultsJson = JsonSerializer.Serialize(result.StepResults, JsonOpts),
            MetadataJson    = result.Metadata is not null
                                  ? JsonSerializer.Serialize(result.Metadata, JsonOpts)
                                  : null,
            StartedAt       = startedAt,
            FinishedAt      = finishedAt,
            SessionId       = sessionId,
            UserId          = userId,
        };
    }
}
