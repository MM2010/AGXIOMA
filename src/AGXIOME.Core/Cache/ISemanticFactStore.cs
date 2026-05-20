using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Lightweight interface for cross-session semantic fact storage and retrieval.
///
/// Implementations:
///   InMemoryFactStore    — dev/test, volatile
///   RedisFactStore       — production hot cache (L2)
///   PgVectorFactStore    — production persistent (L3, with pgvector)
///
/// All methods are designed for minimal allocations.
/// Results use pooled arrays internally where possible.
/// </summary>
public interface ISemanticFactStore
{
    /// <summary>
    /// Store a fact extracted from a turn.
    /// </summary>
    Task StoreAsync(SemanticFact fact, CancellationToken ct = default);

    /// <summary>
    /// Retrieve facts by agent ID, optionally filtered by entity.
    /// Returns up to <paramref name="maxResults"/> facts, newest first.
    /// </summary>
    Task<IReadOnlyList<SemanticFact>> GetFactsAsync(
        string agentId, string? entity = null,
        int maxResults = 10, CancellationToken ct = default);

    /// <summary>
    /// Semantic search: embed query, find nearest facts by cosine similarity.
    /// Uses pgvector or in-memory cosine similarity.
    /// </summary>
    Task<IReadOnlyList<SemanticFact>> SearchFactsAsync(
        string agentId, string query,
        int maxResults = 5, float minScore = 0.3f,
        CancellationToken ct = default);

    /// <summary>
    /// Delete facts for a session (cleanup).
    /// </summary>
    Task DeleteSessionFactsAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Get fact count for an agent (monitoring).
    /// </summary>
    ValueTask<int> CountAsync(string agentId, CancellationToken ct = default);
}
