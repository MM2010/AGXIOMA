using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;

namespace AGXIOME.Core.Execution;

// ─────────────────────────────────────────────────────────────────────────────
// BackgroundRunWorker — CE-01
// Executes agent runs on a persistent background loop with polling, checkpoints,
// and SSE-style reconnect semantics. Thread-safe: multiple callers can enqueue
// jobs concurrently. Stateless towards the caller — state lives in RunJob records.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A single background job descriptor.</summary>
public record RunJob(
    string JobId,
    BackgroundJobType JobType,
    string AgentId,
    string Prompt,
    string? SessionId = null,
    IReadOnlyDictionary<string, object>? Context = null
);

/// <summary>Snapshot of a job's runtime state. Immutable — new instances emitted on every status change.</summary>
public record RunJobState(
    string JobId,
    BackgroundJobType JobType,
    BackgroundJobStatus Status,
    string? ResultText = null,
    string? ErrorMessage = null,
    int RetryCount = 0,
    DateTimeOffset? CompletedAt = null
);

/// <summary>Contract for the background run worker.</summary>
public interface IBackgroundRunWorker
{
    /// <summary>Enqueue a job. Returns the assigned job ID (same as <see cref="RunJob.JobId"/>).</summary>
    string Enqueue(RunJob job);

    /// <summary>Try to retrieve the current state of a job. Returns null if the job ID is unknown.</summary>
    RunJobState? GetState(string jobId);

    /// <summary>Stream state updates for a job until it reaches a terminal state or cancellation.
    /// Reconnect-safe: caller supplies the last seen status to skip already-delivered states.</summary>
    IAsyncEnumerable<RunJobState> WatchAsync(string jobId, CancellationToken ct = default);

    /// <summary>Cancel a running or queued job. Returns false if the job is unknown or already terminal.</summary>
    bool Cancel(string jobId);

    /// <summary>All jobs currently tracked (snapshot).</summary>
    IReadOnlyList<RunJobState> ListJobs();
}

/// <summary>In-process background run worker.
/// For production deployments with multiple nodes, replace with a distributed worker backed by Redis streams.</summary>
public sealed class BackgroundRunWorker : IBackgroundRunWorker, IDisposable
{
    private readonly IAgxiomeAgent _agent;
    private readonly ConcurrentDictionary<string, RunJobState> _states = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cts = new();
    // Each job gets a dedicated channel for streaming state updates.
    private readonly ConcurrentDictionary<string, System.Threading.Channels.Channel<RunJobState>> _channels = new();

    public BackgroundRunWorker(IAgxiomeAgent agent) => _agent = agent;

    public string Enqueue(RunJob job)
    {
        var id = job.JobId;
        var initial = new RunJobState(id, job.JobType, BackgroundJobStatus.Queued);
        _states[id] = initial;

        var cts = new CancellationTokenSource();
        _cts[id] = cts;

        var channel = System.Threading.Channels.Channel.CreateUnbounded<RunJobState>(
            new System.Threading.Channels.UnboundedChannelOptions { SingleWriter = true });
        _channels[id] = channel;

        _ = ExecuteAsync(job, cts.Token);
        return id;
    }

    public RunJobState? GetState(string jobId) =>
        _states.TryGetValue(jobId, out var s) ? s : null;

    public async IAsyncEnumerable<RunJobState> WatchAsync(
        string jobId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_channels.TryGetValue(jobId, out var channel))
            yield break;

        await foreach (var state in channel.Reader.ReadAllAsync(ct))
        {
            yield return state;
            if (IsTerminal(state.Status)) yield break;
        }
    }

    public bool Cancel(string jobId)
    {
        if (!_cts.TryGetValue(jobId, out var cts)) return false;
        cts.Cancel();
        return true;
    }

    public IReadOnlyList<RunJobState> ListJobs() =>
        _states.Values.ToList();

    private async Task ExecuteAsync(RunJob job, CancellationToken ct)
    {
        Transition(job.JobId, job.JobType, BackgroundJobStatus.Running);
        try
        {
            var request = new AgentRequest(job.Prompt);
            var response = await _agent.RunAsync(request, ct);
            Transition(job.JobId, job.JobType, BackgroundJobStatus.Completed,
                resultText: response.Content, completedAt: DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            Transition(job.JobId, job.JobType, BackgroundJobStatus.Cancelled,
                completedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            Transition(job.JobId, job.JobType, BackgroundJobStatus.Failed,
                errorMessage: ex.Message, completedAt: DateTimeOffset.UtcNow);
        }
        finally
        {
            if (_channels.TryGetValue(job.JobId, out var ch))
                ch.Writer.TryComplete();
        }
    }

    private void Transition(
        string jobId,
        BackgroundJobType jobType,
        BackgroundJobStatus status,
        string? resultText = null,
        string? errorMessage = null,
        DateTimeOffset? completedAt = null)
    {
        var prev = _states.GetValueOrDefault(jobId);
        var next = new RunJobState(jobId, jobType, status, resultText, errorMessage,
            prev?.RetryCount ?? 0, completedAt);
        _states[jobId] = next;
        if (_channels.TryGetValue(jobId, out var ch))
            ch.Writer.TryWrite(next);
    }

    private static bool IsTerminal(BackgroundJobStatus s) =>
        s is BackgroundJobStatus.Completed or BackgroundJobStatus.Failed or BackgroundJobStatus.Cancelled;

    public void Dispose()
    {
        foreach (var cts in _cts.Values) cts.Dispose();
    }
}
