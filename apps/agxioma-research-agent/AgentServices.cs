using AGXIOME.Shared.Models;

namespace AgxiomaResearchAgent;

// ── Configuration ─────────────────────────────────────────────────────────────

public record AgentSettings
{
    public string Id                 { get; init; } = "MY_AGENT_NAME";
    public string? Provider          { get; init; }
    public string? OrchestratorModel { get; init; }
    public string? WorkerModel       { get; init; }
    public int WorkerCount           { get; init; } = 3;
}

// ── Request / Response ────────────────────────────────────────────────────────

public record ResearchRequest(string Topic);

// ── Job store for async research tasks ───────────────────────────────────────

public enum JobStatus { Pending, Running, Completed, Failed }

public sealed class ResearchJob
{
    public string JobId         { get; init; } = string.Empty;
    public string Topic         { get; init; } = string.Empty;
    public JobStatus Status     { get; set; }
    public HeavySwarmResult? Result { get; set; }
    public string? Error        { get; set; }
    public DateTime StartedAt   { get; init; } = DateTime.UtcNow;
    public DateTime? EndedAt    { get; set; }
    public TimeSpan? Duration   => EndedAt.HasValue ? EndedAt - StartedAt : null;
}

public sealed class ResearchJobStore
{
    private readonly Dictionary<string, ResearchJob> _jobs = new();
    private readonly Lock _lock = new();

    public void Start(string jobId, string topic)
    {
        lock (_lock)
            _jobs[jobId] = new ResearchJob { JobId = jobId, Topic = topic, Status = JobStatus.Running };
    }

    public void Complete(string jobId, HeavySwarmResult result)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status  = JobStatus.Completed;
                job.Result  = result;
                job.EndedAt = DateTime.UtcNow;
            }
        }
    }

    public void Fail(string jobId, string error)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status  = JobStatus.Failed;
                job.Error   = error;
                job.EndedAt = DateTime.UtcNow;
            }
        }
    }

    public ResearchJob? Get(string jobId)
    {
        lock (_lock) { _jobs.TryGetValue(jobId, out var j); return j; }
    }

    public IReadOnlyList<ResearchJob> All()
    {
        lock (_lock) { return _jobs.Values.OrderByDescending(j => j.StartedAt).ToList(); }
    }
}
