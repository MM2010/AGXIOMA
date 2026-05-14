using AGXIOME.Core.Storage;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Agent;

/// <summary>Agent memory manager with pluggable backends. InMemory, MarkdownFiles, Postgres, Hybrid.</summary>
public sealed class AgentMemoryManager : IAgentMemory
{
    private readonly StorageConfig _config;
    private readonly IAgentMemory _inner;
    private readonly List<AgentInteraction> _inMemory = new();
    private readonly Dictionary<string, object> _state = new();

    public AgentMemoryManager(StorageConfig config)
    {
        _config = config;
        _inner = config.MemoryBackend switch
        {
            MemoryBackend.MarkdownFiles => new MarkdownMemoryStore(
                config.MarkdownMemory ?? new MarkdownMemoryConfig()),
            _ => this // InMemory fallback — use self
        };
    }

    /// <summary>Get the inner IAgentMemory implementation (for direct access to MarkdownMemoryStore features).</summary>
    public IAgentMemory Inner => _inner;

    public Task SaveInteractionAsync(AgentInteraction interaction, CancellationToken ct = default)
    {
        if (_config.MemoryBackend != MemoryBackend.InMemory)
            return _inner.SaveInteractionAsync(interaction, ct);
        _inMemory.Add(interaction);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default)
    {
        if (_config.MemoryBackend != MemoryBackend.InMemory)
            return _inner.GetHistoryAsync(agentId, limit, since, ct);
        var query = _inMemory.Where(i => i.AgentId == agentId);
        if (since.HasValue) query = query.Where(i => i.CreatedAt >= since.Value);
        return Task.FromResult<IReadOnlyList<AgentInteraction>>(query.TakeLast(limit).ToList());
    }

    public Task ClearAsync(string agentId, CancellationToken ct = default)
    {
        if (_config.MemoryBackend != MemoryBackend.InMemory)
            return _inner.ClearAsync(agentId, ct);
        _inMemory.Clear();
        return Task.CompletedTask;
    }

    public Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default)
    {
        if (_config.MemoryBackend != MemoryBackend.InMemory)
            return _inner.GetStateAsync<T>(key, ct);
        _state.TryGetValue(key, out var val);
        return Task.FromResult((T?)val);
    }

    public Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (_config.MemoryBackend != MemoryBackend.InMemory)
            return _inner.SetStateAsync(key, value, ttl, ct);
        _state[key] = value!;
        return Task.CompletedTask;
    }
}
