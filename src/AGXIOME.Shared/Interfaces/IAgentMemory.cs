namespace AGXIOME.Shared.Interfaces;

public interface IAgentMemory
{
    Task SaveInteractionAsync(Models.AgentInteraction interaction, CancellationToken ct = default);
    Task<IReadOnlyList<Models.AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default);
    Task ClearAsync(string agentId, CancellationToken ct = default);
    Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default);
    Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default);
}

public interface ISemanticMemory
{
    Task<IReadOnlyList<Models.MemoryEntry>> SemanticSearchAsync(string agentId, string query, int topK = 10, CancellationToken ct = default);
    Task StoreEmbeddingAsync(string agentId, string text, float[] embedding, CancellationToken ct = default);
}
