using System.Collections.Concurrent;
using System.Text.Json;
using StackExchange.Redis;

namespace AGXIOME.Runtime.Hubs;

/// <summary>
/// Canonical replay buffer for agent run events used by reconnect and last-N replay.
/// The default in-memory implementation keeps local development fast; Redis provides
/// cross-connection and cross-instance durability for the replay window.
/// </summary>
public interface IAgentEventReplayStore
{
    Task<AgentRunEvent> AppendRunEventAsync(
        string runId,
        string agentId,
        string type,
        string payload,
        CancellationToken ct = default);

    Task<IReadOnlyList<AgentRunEvent>> GetRecentRunEventsAsync(
        string runId,
        int lastN,
        CancellationToken ct = default);
}

public sealed class InMemoryAgentEventReplayStore : IAgentEventReplayStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<AgentRunEvent>> _events = new();
    private readonly ConcurrentDictionary<string, long> _sequences = new();

    public Task<AgentRunEvent> AppendRunEventAsync(
        string runId,
        string agentId,
        string type,
        string payload,
        CancellationToken ct = default)
    {
        var seq = _sequences.AddOrUpdate(runId, 1, static (_, current) => current + 1);
        var evt = new AgentRunEvent(runId, agentId, type, payload, seq, DateTimeOffset.UtcNow);
        var queue = _events.GetOrAdd(runId, static _ => new ConcurrentQueue<AgentRunEvent>());
        queue.Enqueue(evt);

        while (queue.Count > 256 && queue.TryDequeue(out _))
        {
        }

        return Task.FromResult(evt);
    }

    public Task<IReadOnlyList<AgentRunEvent>> GetRecentRunEventsAsync(
        string runId,
        int lastN,
        CancellationToken ct = default)
    {
        if (!_events.TryGetValue(runId, out var queue))
        {
            return Task.FromResult<IReadOnlyList<AgentRunEvent>>([]);
        }

        var items = queue.ToArray();
        var take = Math.Clamp(lastN, 1, 256);
        var start = Math.Max(0, items.Length - take);
        return Task.FromResult<IReadOnlyList<AgentRunEvent>>(items[start..]);
    }
}

public sealed class RedisAgentEventReplayStore : IAgentEventReplayStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;

    public RedisAgentEventReplayStore(IConnectionMultiplexer redis, TimeSpan? ttl = null)
    {
        _redis = redis;
        _ttl = ttl ?? TimeSpan.FromHours(1);
    }

    public async Task<AgentRunEvent> AppendRunEventAsync(
        string runId,
        string agentId,
        string type,
        string payload,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var seqKey = SequenceKey(runId);
        var replayKey = ReplayKey(runId);
        var seq = await db.StringIncrementAsync(seqKey).ConfigureAwait(false);
        var evt = new AgentRunEvent(runId, agentId, type, payload, seq, DateTimeOffset.UtcNow);
        var serialized = JsonSerializer.Serialize(evt, JsonOptions);

        await db.SortedSetAddAsync(replayKey, serialized, seq).ConfigureAwait(false);
        await db.KeyExpireAsync(seqKey, _ttl).ConfigureAwait(false);
        await db.KeyExpireAsync(replayKey, _ttl).ConfigureAwait(false);

        var trimBefore = Math.Max(0, seq - 256);
        if (trimBefore > 0)
        {
            await db.SortedSetRemoveRangeByScoreAsync(replayKey, double.NegativeInfinity, trimBefore).ConfigureAwait(false);
        }

        return evt;
    }

    public async Task<IReadOnlyList<AgentRunEvent>> GetRecentRunEventsAsync(
        string runId,
        int lastN,
        CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var take = Math.Clamp(lastN, 1, 256);
        var values = await db.SortedSetRangeByRankAsync(ReplayKey(runId), -take, -1, Order.Ascending).ConfigureAwait(false);

        var results = new List<AgentRunEvent>(values.Length);
        foreach (var value in values)
        {
            if (!value.HasValue)
            {
                continue;
            }

            var evt = JsonSerializer.Deserialize<AgentRunEvent>(value.ToString(), JsonOptions);
            if (evt is not null)
            {
                results.Add(evt);
            }
        }

        return results;
    }

    private static string ReplayKey(string runId) => $"runtime:run:{runId}:events";
    private static string SequenceKey(string runId) => $"runtime:run:{runId}:seq";
}