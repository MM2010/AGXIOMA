using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Agent;

/// <summary>Agent memory manager with pluggable backends.</summary>
public sealed class AgentMemoryManager : IAgentMemory
{
    private readonly IAgentMemory _inner;

    /// <summary>
    /// Creates a memory manager. If <paramref name=""backend""/> is null,
    /// uses in-memory storage (volatile, for testing).
    /// </summary>
    public AgentMemoryManager(IAgentMemory? backend = null)
    {
        _inner = backend ?? this;
    }

    public IAgentMemory Inner => _inner;

    public Task SaveInteractionAsync(AgentInteraction interaction, CancellationToken ct = default)
        => _inner.SaveInteractionAsync(interaction, ct);

    public Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default)
        => _inner.GetHistoryAsync(agentId, limit, since, ct);

    public Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default)
        => _inner.GetStateAsync<T>(key, ct);

    public Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        => _inner.SetStateAsync(key, value, ttl, ct);

    public Task ClearAsync(string agentId, CancellationToken ct = default)
        => _inner.ClearAsync(agentId, ct);
}