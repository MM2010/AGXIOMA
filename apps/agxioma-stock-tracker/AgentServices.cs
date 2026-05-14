using AGXIOME.Core;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Models;

namespace AgxiomaStockTracker;

// ── Configuration ─────────────────────────────────────────────────────────────

public record AgentSettings
{
    public string? Model        { get; init; }
    public string? DatabasePath { get; init; } = "stock_tracker.db";
    public int WorkerCount      { get; init; } = 3;
}

// ── Market data ───────────────────────────────────────────────────────────────

public record QuoteResult(
    string Ticker,
    decimal Price,
    decimal Change,
    decimal ChangePercent,
    long Volume,
    string? MarketCap,
    string Source
);

// ── Analysis records ──────────────────────────────────────────────────────────

public record AnalysisResult(
    string Ticker,
    string AssetClass,
    string RoutedAgent,
    List<string> Questions,
    List<string> Findings,
    string Analysis,
    List<string> Alternatives,
    string Synthesis,
    Dictionary<string, TimeSpan> Timing
);

// ── Job store ─────────────────────────────────────────────────────────────────

public enum JobStatus { Processing, Completed, Failed }

public record AnalysisJob(
    string JobId,
    string Ticker,
    string Type,
    JobStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    AnalysisResult? Result,
    string? Error
);

public sealed class AnalysisJobStore
{
    private readonly Dictionary<string, AnalysisJob> _jobs = new();
    private readonly Lock _lock = new();

    public void Start(string jobId, string ticker, string type)
    {
        lock (_lock) _jobs[jobId] = new AnalysisJob(jobId, ticker, type, JobStatus.Processing,
            DateTime.UtcNow, null, null, null);
    }

    public void Complete(string jobId, AnalysisResult result)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var j))
                _jobs[jobId] = j with { Status = JobStatus.Completed, CompletedAt = DateTime.UtcNow, Result = result };
        }
    }

    public void Fail(string jobId, string error)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var j))
                _jobs[jobId] = j with { Status = JobStatus.Failed, CompletedAt = DateTime.UtcNow, Error = error };
        }
    }

    public AnalysisJob? Get(string jobId)
    {
        lock (_lock) { return _jobs.GetValueOrDefault(jobId); }
    }
}
