namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Agnostic fast transient storage contract (Redis, Hazelcast, Memcached, InMemory...).
/// No concrete types exposed. All values are serialised strings — caller owns serialisation.
/// </summary>
public interface IHotStore
{
    Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default);
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>Atomically increments a counter and returns the new value. Used for step index.</summary>
    Task<long> IncrementAsync(string key, CancellationToken ct = default);

    /// <summary>Adds a string member to a set. Used to track task IDs per plan.</summary>
    Task SetAddAsync(string key, string member, CancellationToken ct = default);

    /// <summary>Returns all members of a set key.</summary>
    Task<IReadOnlySet<string>> SetMembersAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to a pub/sub channel and receive published messages.
    /// Optional: implementations that do not support pub/sub may throw NotSupportedException.
    /// </summary>
    IAsyncEnumerable<string> SubscribeAsync(string channel, CancellationToken ct = default);

    /// <summary>Publish a message to a pub/sub channel.</summary>
    Task PublishAsync(string channel, string message, CancellationToken ct = default);
}
