using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// In-memory storage for agent conversation history and key-value state.
/// Implementations must be thread-safe. Typically backed by a
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> or Redis.
/// </summary>
/// <remarks>
/// <para><b>Persistence</b>: Default implementations are in-memory only.
/// For persistent storage, wire the session manager (<see cref="ISessionManager"/>) which
/// serializes agent memory to the configured database provider.</para>
/// <para><b>TTL</b>: State entries can have an optional time-to-live.
/// Implementations should lazily evict expired keys on access.</para>
/// </remarks>
public interface IAgentMemory
{
    /// <summary>
    /// Persists a single interaction (user message + assistant response) to the agent's history.
    /// </summary>
    /// <param name="interaction">The interaction record to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveInteractionAsync(Models.AgentInteraction interaction, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the interaction history for the specified agent, ordered newest-first.
    /// </summary>
    /// <param name="agentId">The agent whose history to fetch.</param>
    /// <param name="limit">Maximum number of entries to return (1..500). Default 50.</param>
    /// <param name="since">Optional cutoff — only entries after this timestamp are returned.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of past interactions.</returns>
    Task<IReadOnlyList<Models.AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes all history and state for the given agent. Irreversible.
    /// </summary>
    Task ClearAsync(string agentId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a typed value from the agent's key-value state store.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The state key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The value, or <c>default(T)</c> if not found or expired.</returns>
    Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Stores a typed value in the agent's key-value state store.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The state key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">Optional time-to-live. If null, the entry persists until explicitly removed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
}

/// <summary>
/// Semantic (vector-based) memory for agents. Enables similarity search over past
/// interactions using embedding vectors rather than exact keyword matching.
/// </summary>
/// <remarks>
/// Requires an <see cref="IEmbedder"/> to generate embeddings for stored texts and queries.
/// Implementations delegate to a <see cref="IVectorStore"/> for the actual vector search.
/// </remarks>
public interface ISemanticMemory
{
    /// <summary>
    /// Searches the agent's memory for entries semantically similar to the query.
    /// </summary>
    /// <param name="agentId">The agent whose memory to search.</param>
    /// <param name="query">Natural language query.</param>
    /// <param name="topK">Number of results to return. Default 10.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Ranked list of most similar memory entries.</returns>
    Task<IReadOnlyList<Models.MemoryEntry>> SemanticSearchAsync(string agentId, string query, int topK = 10, CancellationToken ct = default);

    /// <summary>
    /// Stores a text along with its pre-computed embedding vector for later semantic search.
    /// </summary>
    /// <param name="agentId">The agent to associate the embedding with.</param>
    /// <param name="text">The original text (stored for retrieval).</param>
    /// <param name="embedding">The pre-computed embedding vector.</param>
    /// <param name="ct">Cancellation token.</param>
    Task StoreEmbeddingAsync(string agentId, string text, float[] embedding, CancellationToken ct = default);
}
