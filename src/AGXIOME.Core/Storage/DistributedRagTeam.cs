using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-10 — Distributed RAG Team
//
// Orchestrates parallel knowledge retrieval across multiple IKnowledgeBase
// sources and merges results into a ranked unified list.
//
// Design:
//   • RagSourceDescriptor — named registration of one knowledge source.
//   • RagSourceStatus enum — lifecycle state of a registered source.
//   • DistributedRagQuery — typed query contract; SearchType enum — no strings.
//   • DistributedRagResult — merged result with per-source breakdown.
//   • IDistributedRagTeam — interface:
//       RegisterSourceAsync / UnregisterSourceAsync / ListSourcesAsync
//       SearchAsync — fan-out to all (or subset of) sources in parallel,
//                     merge + re-rank by score, return top-K results
//   • DistributedRagTeam — implementation using Task.WhenAll for parallel fan-out.
//     Sources are resolved via a factory delegate to avoid tight coupling.
//
// Performance note: fan-out is bounded by MaxParallelSources (default 8) to
// prevent resource exhaustion; slower sources are soft-cancelled after SourceTimeout.
// ─────────────────────────────────────────────────────────────────────────────

// ── Enums ─────────────────────────────────────────────────────────────────────

/// <summary>Lifecycle state of a registered RAG source.</summary>
public enum RagSourceStatus
{
    Active,
    Paused,     // temporarily excluded from fan-out queries
    Degraded,   // included but marked as having reduced reliability
    Removed,
}

// ── Records ───────────────────────────────────────────────────────────────────

/// <summary>Registration descriptor for one knowledge source.</summary>
public record RagSourceDescriptor
{
    public string Id         { get; init; } = string.Empty;
    public string Name       { get; init; } = string.Empty;
    public string? Namespace { get; init; }
    public RagSourceStatus Status { get; init; } = RagSourceStatus.Active;
    public Dictionary<string, string>? Metadata { get; init; }
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Fan-out query issued to all active sources.</summary>
public record DistributedRagQuery
{
    public string Query             { get; init; } = string.Empty;
    public SearchType SearchType    { get; init; } = SearchType.Vector;
    public int TopKPerSource        { get; init; } = 5;
    public int TopKTotal            { get; init; } = 10;
    public double MinScore          { get; init; } = 0.60;

    /// <summary>If set, only these source IDs are queried.</summary>
    public IReadOnlyList<string>? SourceFilter { get; init; }

    public Dictionary<string, string>? MetadataFilter { get; init; }
}

/// <summary>Merged result from a distributed RAG query.</summary>
public record DistributedRagResult
{
    public IReadOnlyList<KnowledgeChunk> Chunks { get; init; }
        = Array.Empty<KnowledgeChunk>();

    /// <summary>Per-source breakdown: sourceId → chunks returned from that source.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<KnowledgeChunk>> PerSource { get; init; }
        = new Dictionary<string, IReadOnlyList<KnowledgeChunk>>();

    public int TotalSourcesQueried { get; init; }
    public int SourcesWithResults  { get; init; }
    public TimeSpan QueryDuration  { get; init; }
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>
/// Distributed RAG team that fans out search queries to multiple knowledge bases
/// and merges results into a single ranked response.
/// </summary>
public interface IDistributedRagTeam
{
    Task RegisterSourceAsync(RagSourceDescriptor descriptor, CancellationToken ct = default);
    Task UnregisterSourceAsync(string sourceId, CancellationToken ct = default);
    Task<IReadOnlyList<RagSourceDescriptor>> ListSourcesAsync(CancellationToken ct = default);
    Task UpdateSourceStatusAsync(string sourceId, RagSourceStatus status, CancellationToken ct = default);

    /// <summary>
    /// Fan-out to all active (non-Paused/Removed) sources in parallel, merge, and re-rank results.
    /// </summary>
    Task<DistributedRagResult> SearchAsync(
        DistributedRagQuery query, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Distributed RAG team implementation.
/// Sources are resolved via <paramref name="sourceResolver"/> — a factory that
/// returns an IKnowledgeBase for a given RagSourceDescriptor. This keeps the
/// registry decoupled from specific knowledge-base implementations.
/// </summary>
public sealed class DistributedRagTeam : IDistributedRagTeam
{
    private readonly Func<RagSourceDescriptor, IKnowledgeBase> _sourceResolver;
    private readonly int _maxParallelSources;
    private readonly TimeSpan _sourceTimeout;

    // in-memory registry: sourceId → descriptor
    private readonly Dictionary<string, RagSourceDescriptor> _sources = new();

    /// <param name="sourceResolver">
    ///   Factory that returns the IKnowledgeBase for a registered source.
    ///   Called at query time — can return a cached or newly constructed instance.
    /// </param>
    /// <param name="maxParallelSources">Upper bound on concurrent fan-out queries (default 8).</param>
    /// <param name="sourceTimeout">Per-source query timeout; slower sources are cancelled (default 10s).</param>
    public DistributedRagTeam(
        Func<RagSourceDescriptor, IKnowledgeBase> sourceResolver,
        int maxParallelSources = 8,
        TimeSpan? sourceTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(sourceResolver);
        _sourceResolver     = sourceResolver;
        _maxParallelSources = Math.Max(1, maxParallelSources);
        _sourceTimeout      = sourceTimeout ?? TimeSpan.FromSeconds(10);
    }

    // ── IDistributedRagTeam ───────────────────────────────────────────────────

    public Task RegisterSourceAsync(RagSourceDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Id);
        _sources[descriptor.Id] = descriptor;
        return Task.CompletedTask;
    }

    public Task UnregisterSourceAsync(string sourceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        _sources.Remove(sourceId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RagSourceDescriptor>> ListSourcesAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RagSourceDescriptor>>(_sources.Values.ToList());

    public Task UpdateSourceStatusAsync(
        string sourceId, RagSourceStatus status, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (_sources.TryGetValue(sourceId, out var existing))
            _sources[sourceId] = existing with { Status = status };
        return Task.CompletedTask;
    }

    public async Task<DistributedRagResult> SearchAsync(
        DistributedRagQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Query);

        var started = DateTimeOffset.UtcNow;

        // Select active sources, respecting optional filter.
        IEnumerable<RagSourceDescriptor> candidates = _sources.Values
            .Where(s => s.Status is RagSourceStatus.Active or RagSourceStatus.Degraded);

        if (query.SourceFilter is { Count: > 0 })
            candidates = candidates.Where(s => query.SourceFilter.Contains(s.Id));

        var activeSources = candidates.ToList();

        // Fan-out in bounded parallel batches.
        var opts = new KnowledgeSearchOptions(
            TopK:           query.TopKPerSource,
            MinScore:       query.MinScore,
            MetadataFilter: query.MetadataFilter,
            SearchType:     query.SearchType);

        var perSource = new Dictionary<string, IReadOnlyList<KnowledgeChunk>>();

        // Process in batches of _maxParallelSources to bound concurrency.
        for (int i = 0; i < activeSources.Count; i += _maxParallelSources)
        {
            var batch = activeSources.Skip(i).Take(_maxParallelSources);

            var tasks = batch.Select(async src =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_sourceTimeout);

                try
                {
                    var kb = _sourceResolver(src);
                    var chunks = await kb.SearchAsync(query.Query, opts, cts.Token);
                    return (src.Id, chunks);
                }
                catch (OperationCanceledException)
                {
                    // Source timed out or outer cancellation — return empty results.
                    return (src.Id, (IReadOnlyList<KnowledgeChunk>)Array.Empty<KnowledgeChunk>());
                }
            });

            var batchResults = await Task.WhenAll(tasks);
            foreach (var (id, chunks) in batchResults)
                perSource[id] = chunks;
        }

        // Merge and re-rank all chunks by score descending, deduplicating by DocumentId.
        var merged = perSource.Values
            .SelectMany(c => c)
            .GroupBy(c => c.DocumentId)
            .Select(g => g.MaxBy(c => c.Score)!)  // keep highest-score instance per doc
            .Where(c => c.Score >= (float)query.MinScore)
            .OrderByDescending(c => c.Score)
            .Take(query.TopKTotal)
            .ToList();

        return new DistributedRagResult
        {
            Chunks              = merged,
            PerSource           = perSource,
            TotalSourcesQueried = activeSources.Count,
            SourcesWithResults  = perSource.Count(kv => kv.Value.Count > 0),
            QueryDuration       = DateTimeOffset.UtcNow - started,
        };
    }
}
