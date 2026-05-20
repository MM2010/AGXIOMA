using System.Collections.Concurrent;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Cache;

/// <summary>
/// In-memory semantic fact store. Default implementation.
///
/// Production: replace with RedisFactStore (L2) or PgVectorFactStore (L3).
/// This store is volatile — facts are lost on restart.
///
/// Thread-safe. Uses ConcurrentDictionary partition per agent.
/// Semantic search uses simple TF-IDF fallback (no embedding).
/// </summary>
public sealed class InMemoryFactStore : ISemanticFactStore, IDisposable
{
    private readonly ConcurrentDictionary<string, List<SemanticFact>> _facts = new();
    private readonly ILogger<InMemoryFactStore> _log;
    private readonly ReaderWriterLockSlim _lock = new();

    private const int MaxFactsPerAgent = 1000;
    private int _totalFacts;

    public InMemoryFactStore(ILogger<InMemoryFactStore> log)
    {
        _log = log;
    }

    public Task StoreAsync(SemanticFact fact, CancellationToken ct = default)
    {
        var list = _facts.GetOrAdd(fact.AgentId, _ => new List<SemanticFact>(64));

        _lock.EnterWriteLock();
        try
        {
            if (list.Count >= MaxFactsPerAgent)
                list.RemoveAt(0); // evict oldest
            list.Add(fact);
            Interlocked.Increment(ref _totalFacts);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SemanticFact>> GetFactsAsync(
        string agentId, string? entity = null,
        int maxResults = 10, CancellationToken ct = default)
    {
        if (!_facts.TryGetValue(agentId, out var list))
            return Task.FromResult<IReadOnlyList<SemanticFact>>(Array.Empty<SemanticFact>());

        _lock.EnterReadLock();
        try
        {
            var query = list.AsEnumerable();

            if (!string.IsNullOrEmpty(entity))
                query = query.Where(f =>
                    f.Entity.Contains(entity, StringComparison.OrdinalIgnoreCase));

            var results = query
                .OrderByDescending(f => f.CreatedAt)
                .Take(maxResults)
                .ToArray();

            return Task.FromResult<IReadOnlyList<SemanticFact>>(results);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task<IReadOnlyList<SemanticFact>> SearchFactsAsync(
        string agentId, string query,
        int maxResults = 5, float minScore = 0.3f,
        CancellationToken ct = default)
    {
        if (!_facts.TryGetValue(agentId, out var list))
            return Task.FromResult<IReadOnlyList<SemanticFact>>(Array.Empty<SemanticFact>());

        _lock.EnterReadLock();
        try
        {
            var queryLower = query.ToLowerInvariant();
            var terms = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var scored = new List<(SemanticFact Fact, float Score)>(list.Count);
            foreach (var fact in list)
            {
                var score = ScoreFact(fact, terms);
                if (score >= minScore)
                    scored.Add((fact, score));
            }

            var results = scored
                .OrderByDescending(s => s.Score)
                .Take(maxResults)
                .Select(s => s.Fact with { Score = s.Score })
                .ToArray();

            return Task.FromResult<IReadOnlyList<SemanticFact>>(results);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task DeleteSessionFactsAsync(string sessionId, CancellationToken ct = default)
    {
        _lock.EnterWriteLock();
        try
        {
            foreach (var kv in _facts)
            {
                kv.Value.RemoveAll(f => f.SessionId == sessionId);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        return Task.CompletedTask;
    }

    public ValueTask<int> CountAsync(string agentId, CancellationToken ct = default)
    {
        if (_facts.TryGetValue(agentId, out var list))
            return new ValueTask<int>(list.Count);
        return new ValueTask<int>(0);
    }

    // ── Simple TF-IDF scoring (no embedding needed) ────────────────

    private static float ScoreFact(SemanticFact fact, string[] queryTerms)
    {
        if (queryTerms.Length == 0) return 0;

        var text = string.Concat(
            fact.Entity, " ", fact.Predicate, " ", fact.Object, " ", fact.Content)
            .ToLowerInvariant();

        var matches = queryTerms.Count(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
        return (float)matches / queryTerms.Length;
    }

    public void Dispose() => _lock.Dispose();
}
