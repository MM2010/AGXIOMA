using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace AGXIOME.Core.Routing;

/// <summary>
/// AGXIOMA ModelRouter — intelligently routes tasks to the optimal LLM provider
/// based on real-time ProviderMetrics (latency / cost / capability).
///
/// Differenziatori vs Swarms' ModelRouter:
///   - Data-driven routing: metrics are MEASURED not assumed
///   - CircuitBreaker per provider: failed providers are excluded automatically
///   - RoutingStrategy enum: Cost / Latency / Capability / Hybrid / RoundRobin
///   - FallbackChain: ordered list of providers to try on failure
///   - CapabilityFilter: route only to providers that support vision/tools/streaming
///   - CostBudget: hard limit on cost per request
///   - ProviderMetricsSnapshot: expose current metrics for monitoring
/// </summary>
public sealed class AgxiomeModelRouter
{
    private readonly IReadOnlyList<RouterEntry> _providers;
    private readonly ModelRouterConfig _config;
    private readonly ConcurrentDictionary<string, ProviderCircuitBreaker> _breakers = new();
    private readonly ConcurrentDictionary<string, ProviderMetrics> _metrics = new();

    public AgxiomeModelRouter(
        IReadOnlyList<RouterEntry> providers,
        ModelRouterConfig? config = null)
    {
        if (providers.Count == 0) throw new ArgumentException("At least one provider required.");
        _providers = providers;
        _config = config ?? new ModelRouterConfig();

        // Init breakers and metrics
        foreach (var p in providers)
        {
            _breakers[p.Name] = new ProviderCircuitBreaker(_config.CircuitBreakerThreshold);
            _metrics[p.Name] = new ProviderMetrics(p.Name);
        }
    }

    /// <summary>Route a task to the best provider and run it. Returns the provider used + result.</summary>
    public async Task<RouterResult> RouteAsync(
        RouterRequest request, CancellationToken ct = default)
    {
        var eligible = GetEligibleProviders(request);
        if (eligible.Count == 0)
            throw new InvalidOperationException("No eligible providers available (all breakers open or capability mismatch).");

        var ordered = RankProviders(eligible, request.Strategy ?? _config.DefaultStrategy);
        Exception? lastEx = null;

        foreach (var entry in ordered)
        {
            var breaker = _breakers[entry.Name];
            if (!breaker.CanExecute) continue;

            var sw = Stopwatch.StartNew();
            try
            {
                var result = await entry.ExecuteAsync(request.Task, ct);
                var elapsed = sw.Elapsed;

                // Record success metrics
                RecordSuccess(entry.Name, elapsed, request.EstimatedCost ?? 0);
                breaker.RecordSuccess();

                _config.OnRoute?.Invoke(entry.Name, request.Task[..Math.Min(80, request.Task.Length)], elapsed);
                return new RouterResult(result, entry.Name, elapsed, _metrics[entry.Name]);
            }
            catch (Exception ex)
            {
                breaker.RecordFailure();
                RecordFailure(entry.Name);
                lastEx = ex;

                if (!_config.TryFallback) throw;
            }
        }

        throw new AggregateException($"All {ordered.Count} providers failed.", lastEx!);
    }

    /// <summary>Run multiple tasks concurrently with automatic load balancing across providers.</summary>
    public async Task<IReadOnlyList<RouterResult>> RunConcurrentlyAsync(
        IReadOnlyList<RouterRequest> requests, int maxParallel = 4, CancellationToken ct = default)
    {
        using var sem = new SemaphoreSlim(maxParallel);
        var results = await Task.WhenAll(requests.Select(async req =>
        {
            await sem.WaitAsync(ct);
            try { return await RouteAsync(req, ct); }
            finally { sem.Release(); }
        }));
        return results;
    }

    /// <summary>Expose current provider metrics for monitoring dashboards.</summary>
    public IReadOnlyDictionary<string, ProviderMetrics> GetMetricsSnapshot()
        => _metrics.ToDictionary(k => k.Key, v => v.Value);

    // ─── Private ─────────────────────────────────────────────────────────────

    private List<RouterEntry> GetEligibleProviders(RouterRequest req)
    {
        return _providers.Where(p =>
        {
            if (!_breakers[p.Name].CanExecute) return false;
            if (req.RequiresVision && !p.SupportsVision) return false;
            if (req.RequiresFunctionCalling && !p.SupportsFunctionCalling) return false;
            if (req.MaxCostPerRequest.HasValue && p.CostPer1kTokens > (double)req.MaxCostPerRequest.Value) return false;
            if (req.RequiredCapabilities.Count > 0 && !req.RequiredCapabilities.All(c => p.Capabilities.Contains(c))) return false;
            return true;
        }).ToList();
    }

    private List<RouterEntry> RankProviders(List<RouterEntry> eligible, RoutingStrategy strategy)
    {
        return strategy switch
        {
            RoutingStrategy.Cost => eligible
                .OrderBy(p => p.CostPer1kTokens)
                .ThenBy(p => _metrics[p.Name].AvgLatencyMs)
                .ToList(),

            RoutingStrategy.Latency => eligible
                .OrderBy(p => _metrics[p.Name].AvgLatencyMs == 0 ? double.MaxValue : _metrics[p.Name].AvgLatencyMs)
                .ThenBy(p => p.CostPer1kTokens)
                .ToList(),

            RoutingStrategy.Capability => eligible
                .OrderByDescending(p => p.CapabilityScore)
                .ToList(),

            RoutingStrategy.Hybrid => eligible
                .OrderBy(p => HybridScore(p))
                .ToList(),

            RoutingStrategy.RoundRobin => eligible
                .OrderBy(_ => Guid.NewGuid())
                .ToList(),

            _ => eligible
        };
    }

    private double HybridScore(RouterEntry p)
    {
        var metrics = _metrics[p.Name];
        var normalizedCost = p.CostPer1kTokens / 10.0;
        var normalizedLatency = metrics.AvgLatencyMs / 5000.0;
        var normalizedCapability = 1.0 - (p.CapabilityScore / 10.0);
        var errorPenalty = metrics.ErrorRate * 2.0;
        return normalizedCost * _config.CostWeight
             + normalizedLatency * _config.LatencyWeight
             + normalizedCapability * _config.CapabilityWeight
             + errorPenalty;
    }

    private void RecordSuccess(string name, TimeSpan elapsed, decimal cost)
    {
        _metrics.AddOrUpdate(name,
            _ => new ProviderMetrics(name) with { TotalRequests = 1, AvgLatencyMs = elapsed.TotalMilliseconds },
            (_, existing) => existing with
            {
                TotalRequests = existing.TotalRequests + 1,
                AvgLatencyMs = (existing.AvgLatencyMs * existing.TotalRequests + elapsed.TotalMilliseconds) / (existing.TotalRequests + 1),
                TotalCost = existing.TotalCost + cost,
                ErrorRate = (double)existing.FailedRequests / (existing.TotalRequests + 1)
            });
    }

    private void RecordFailure(string name)
    {
        _metrics.AddOrUpdate(name,
            _ => new ProviderMetrics(name) with { TotalRequests = 1, FailedRequests = 1, ErrorRate = 1.0 },
            (_, existing) => existing with
            {
                TotalRequests = existing.TotalRequests + 1,
                FailedRequests = existing.FailedRequests + 1,
                ErrorRate = (double)(existing.FailedRequests + 1) / (existing.TotalRequests + 1)
            });
    }
}

// ─── Circuit Breaker ─────────────────────────────────────────────────────────

internal sealed class ProviderCircuitBreaker(int threshold)
{
    private int _failures;
    private DateTime _openedAt;
    private static readonly TimeSpan HalfOpenAfter = TimeSpan.FromSeconds(30);

    public bool CanExecute
    {
        get
        {
            if (_failures < threshold) return true;
            if (DateTime.UtcNow - _openedAt > HalfOpenAfter) { _failures = threshold - 1; return true; }
            return false;
        }
    }

    public void RecordSuccess() => Interlocked.Exchange(ref _failures, 0);

    public void RecordFailure()
    {
        if (Interlocked.Increment(ref _failures) >= threshold)
            _openedAt = DateTime.UtcNow;
    }
}

// ─── Records & Enums ─────────────────────────────────────────────────────────

public enum RoutingStrategy { Cost, Latency, Capability, Hybrid, RoundRobin }

public sealed record RouterEntry(
    string Name,
    Func<string, CancellationToken, Task<string>> ExecuteAsync,
    double CostPer1kTokens = 0.01,
    double CapabilityScore = 5.0,
    bool SupportsVision = false,
    bool SupportsFunctionCalling = true,
    IReadOnlyList<string>? Capabilities = null
)
{
    public IReadOnlyList<string> Capabilities { get; init; } = Capabilities ?? Array.Empty<string>();
}

public sealed record RouterRequest(
    string Task,
    RoutingStrategy? Strategy = null,
    bool RequiresVision = false,
    bool RequiresFunctionCalling = false,
    decimal? MaxCostPerRequest = null,
    decimal? EstimatedCost = null,
    IReadOnlyList<string>? RequiredCapabilities = null
)
{
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = RequiredCapabilities ?? Array.Empty<string>();
}

public sealed record ModelRouterConfig
{
    public RoutingStrategy DefaultStrategy { get; init; } = RoutingStrategy.Hybrid;
    public bool TryFallback { get; init; } = true;
    public int CircuitBreakerThreshold { get; init; } = 3;
    public double CostWeight { get; init; } = 0.4;
    public double LatencyWeight { get; init; } = 0.35;
    public double CapabilityWeight { get; init; } = 0.25;
    public Action<string, string, TimeSpan>? OnRoute { get; init; }
}

public record ProviderMetrics(
    string ProviderName,
    long TotalRequests = 0,
    long FailedRequests = 0,
    double AvgLatencyMs = 0,
    decimal TotalCost = 0,
    double ErrorRate = 0
);

public record RouterResult(
    string Output,
    string ProviderUsed,
    TimeSpan Duration,
    ProviderMetrics Metrics
);
