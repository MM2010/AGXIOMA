using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Agent;

/// <summary>In-memory agent memory manager (for dev/test). Replaced by Postgres in production.</summary>
public sealed class AgentMemoryManager : IAgentMemory
{
    private readonly StorageConfig _config;
    private readonly List<AgentInteraction> _interactions = new();
    private readonly Dictionary<string, object> _state = new();

    public AgentMemoryManager(StorageConfig config) => _config = config;

    public Task SaveInteractionAsync(AgentInteraction interaction, CancellationToken ct = default)
    {
        _interactions.Add(interaction);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default)
    {
        var query = _interactions.Where(i => i.AgentId == agentId);
        if (since.HasValue) query = query.Where(i => i.CreatedAt >= since.Value);
        return Task.FromResult<IReadOnlyList<AgentInteraction>>(query.TakeLast(limit).ToList());
    }

    public Task ClearAsync(string agentId, CancellationToken ct = default)
    {
        _interactions.Clear();
        return Task.CompletedTask;
    }

    public Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default)
    {
        _state.TryGetValue(key, out var val);
        return Task.FromResult((T?)val);
    }

    public Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        _state[key] = value!;
        return Task.CompletedTask;
    }
}
