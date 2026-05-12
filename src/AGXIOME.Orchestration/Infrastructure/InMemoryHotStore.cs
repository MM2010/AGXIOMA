using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AGXIOME.Orchestration.Abstractions;

namespace AGXIOME.Orchestration.Infrastructure;

/// <summary>
/// In-memory IHotStore backed by ConcurrentDictionary with sliding TTL.
/// Pub/sub uses per-channel Channel&lt;string&gt; instances.
/// Intended for tests and local dev — no persistence across process restarts.
/// </summary>
public sealed class InMemoryHotStore : IHotStore
{
    private sealed record Entry(string Value, DateTimeOffset? Expires);

    private readonly ConcurrentDictionary<string, Entry> _store = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<Channel<string>>> _channels = new();

    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var expires = ttl.HasValue ? DateTimeOffset.UtcNow.Add(ttl.Value) : (DateTimeOffset?)null;
        _store[key] = new Entry(value, expires);
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.Expires.HasValue && entry.Expires.Value < DateTimeOffset.UtcNow)
            {
                _store.TryRemove(key, out _);
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>(entry.Value);
        }
        return Task.FromResult<string?>(null);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.Expires.HasValue && entry.Expires.Value < DateTimeOffset.UtcNow)
            {
                _store.TryRemove(key, out _);
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<long> IncrementAsync(string key, CancellationToken ct = default)
    {
        // Spin-retry for atomic increment without external lock
        while (true)
        {
            var current = _store.TryGetValue(key, out var e) && long.TryParse(e.Value, out var v) ? v : 0L;
            var next = current + 1;
            var expected = e;
            var newEntry = new Entry(next.ToString(), null);
            if (expected is null)
            {
                if (_store.TryAdd(key, newEntry))
                    return Task.FromResult(next);
            }
            else
            {
                if (_store.TryUpdate(key, newEntry, expected))
                    return Task.FromResult(next);
            }
        }
    }

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _sets = new();

    public Task SetAddAsync(string key, string member, CancellationToken ct = default)
    {
        _sets.GetOrAdd(key, _ => new ConcurrentDictionary<string, byte>())
             .TryAdd(member, 0);
        return Task.CompletedTask;
    }

    public Task<IReadOnlySet<string>> SetMembersAsync(string key, CancellationToken ct = default)
    {
        if (_sets.TryGetValue(key, out var set))
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(set.Keys));
        return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }

    public async IAsyncEnumerable<string> SubscribeAsync(string channel,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ch = Channel.CreateUnbounded<string>();
        var bag = _channels.GetOrAdd(channel, _ => new ConcurrentBag<Channel<string>>());
        bag.Add(ch);
        try
        {
            await foreach (var msg in ch.Reader.ReadAllAsync(ct))
                yield return msg;
        }
        finally
        {
            // Remove the channel from the bag on exit (best-effort)
            if (_channels.TryGetValue(channel, out var b))
            {
                var remaining = new ConcurrentBag<Channel<string>>(b.Where(x => x != ch));
                _channels.TryUpdate(channel, remaining, b);
            }
        }
    }

    public async Task PublishAsync(string channel, string message, CancellationToken ct = default)
    {
        if (!_channels.TryGetValue(channel, out var bag)) return;
        foreach (var ch in bag)
            await ch.Writer.WriteAsync(message, ct);
    }

    /// <summary>Clears all stored data — for test isolation.</summary>
    public void Clear()
    {
        _store.Clear();
        _sets.Clear();
        _channels.Clear();
    }
}
