using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using StackExchange.Redis;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// SS-01 — Redis Session Manager
//
// Full ISessionManager implementation backed by Redis.
// Distinct from DB-15 RedisSessionStore (which implements ICommandStore for
// generic entity storage). This class manages structured session lifecycle:
// sessions, message history, typed state, summaries, and user-scoped indexing.
//
// Key layout (all namespaced under KeyPrefix):
//   {prefix}:s:{sessionId}        → Session JSON (with TTL = SessionTtl)
//   {prefix}:m:{sessionId}        → Redis List of AgentMessage JSON strings
//   {prefix}:st:{sessionId}:{T}   → State JSON keyed by type name
//   {prefix}:sum:{sessionId}      → Summary plain string
//   {prefix}:u:{userId}           → Sorted Set (score=unix_ts, member=sessionId)
//
// Security note: SessionIds are generated server-side via Guid.CreateVersion7()
// to prevent client-controlled ID injection.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Options for <see cref="RedisSessionManager"/>.</summary>
public sealed record RedisSessionManagerOptions
{
    /// <summary>Redis key namespace prefix. Default: "agxioma:sm".</summary>
    public string KeyPrefix { get; init; } = "agxioma:sm";

    /// <summary>TTL for session keys (session, messages, state, summary).
    /// Refreshed on each write. Default: 24 hours.</summary>
    public TimeSpan SessionTtl { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Maximum message history entries per session kept in Redis List.
    /// Older entries are trimmed via LTRIM. Default: 1000.</summary>
    public int MaxHistoryMessages { get; init; } = 1000;
}

/// <summary>
/// Redis-backed <see cref="ISessionManager"/>.
/// Accepts an injected <see cref="IConnectionMultiplexer"/> for testability;
/// use the connection-string constructor for production.
/// </summary>
public sealed class RedisSessionManager : ISessionManager, IDisposable
{
    private readonly IDatabase _db;
    private readonly RedisSessionManagerOptions _opts;
    // Only set when this instance owns the multiplexer (connection-string ctor).
    private readonly IConnectionMultiplexer? _ownedMux;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented      = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Constructors ──────────────────────────────────────────────────────────

    /// <summary>Production constructor: opens a Redis connection from the connection string.</summary>
    /// <param name="connectionString">StackExchange.Redis connection string.
    /// Use "localhost:6379,abortConnect=false" for dev/resilient defaults.</param>
    public RedisSessionManager(string connectionString, RedisSessionManagerOptions? opts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _opts     = opts ?? new RedisSessionManagerOptions();
        _ownedMux = ConnectionMultiplexer.Connect(connectionString);
        _db       = _ownedMux.GetDatabase();
    }

    /// <summary>DI / testing constructor: injects an existing multiplexer.</summary>
    public RedisSessionManager(IConnectionMultiplexer mux, RedisSessionManagerOptions? opts = null)
    {
        ArgumentNullException.ThrowIfNull(mux);
        _opts = opts ?? new RedisSessionManagerOptions();
        _db   = mux.GetDatabase();
        // _ownedMux stays null — we don't dispose a multiplexer we don't own.
    }

    // ── ISessionManager ───────────────────────────────────────────────────────

    public async Task<Session> CreateSessionAsync(
        string userId, string componentId, SessionType type,
        SessionOptions? options = null, CancellationToken ct = default)
    {
        var session = new Session(
            Id:          Guid.CreateVersion7().ToString(),
            UserId:      userId,
            ComponentId: componentId,
            Type:        type,
            Name:        options?.Name,
            State:       new Dictionary<string, object>(),
            Metadata:    options?.Metadata,
            CreatedAt:   DateTime.UtcNow
        );

        await PersistSessionAsync(session, ct);

        // Add to user index (sorted set: score = unix timestamp for chronological ordering)
        var score = (double)new DateTimeOffset(session.CreatedAt).ToUnixTimeMilliseconds();
        await _db.SortedSetAddAsync(UserIndexKey(userId), session.Id, score);
        await _db.KeyExpireAsync(UserIndexKey(userId), _opts.SessionTtl);

        return session;
    }

    public async Task<Session?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var raw = await _db.StringGetAsync(SessionKey(sessionId));
        return raw.HasValue ? JsonSerializer.Deserialize<Session>((string)raw!, JsonOpts) : null;
    }

    public async Task<IReadOnlyList<Session>> ListSessionsAsync(
        string userId, SessionType? type = null, int limit = 50, CancellationToken ct = default)
    {
        // Retrieve sessionIds from user sorted set (newest last = ascending score).
        var members = await _db.SortedSetRangeByRankAsync(
            UserIndexKey(userId), 0, -1, Order.Descending);

        var sessions = new List<Session>();
        foreach (var member in members)
        {
            var session = await GetSessionAsync(member.ToString(), ct);
            if (session is null) continue;
            if (type.HasValue && session.Type != type.Value) continue;
            sessions.Add(session);
            if (sessions.Count >= limit) break;
        }
        return sessions;
    }

    public async Task UpdateSessionStateAsync<T>(
        string sessionId, T state, CancellationToken ct = default)
    {
        var key   = StateKey(sessionId, typeof(T).Name);
        var value = JsonSerializer.Serialize(state, JsonOpts);
        await _db.StringSetAsync(key, value, _opts.SessionTtl);

        // Also refresh the session's UpdatedAt timestamp.
        var session = await GetSessionAsync(sessionId, ct);
        if (session is not null)
            await PersistSessionAsync(session with { UpdatedAt = DateTime.UtcNow }, ct);
    }

    public async Task<T?> GetSessionStateAsync<T>(
        string sessionId, CancellationToken ct = default)
    {
        var raw = await _db.StringGetAsync(StateKey(sessionId, typeof(T).Name));
        if (!raw.HasValue) return default;
        return JsonSerializer.Deserialize<T>((string)raw!, JsonOpts);
    }

    public async Task AddMessageAsync(
        string sessionId, AgentMessage msg, CancellationToken ct = default)
    {
        var key  = MessagesKey(sessionId);
        var json = JsonSerializer.Serialize(msg, JsonOpts);
        await _db.ListRightPushAsync(key, json);

        // Cap the list to MaxHistoryMessages to avoid unbounded growth.
        await _db.ListTrimAsync(key, -_opts.MaxHistoryMessages, -1);
        await _db.KeyExpireAsync(key, _opts.SessionTtl);
    }

    public async Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(
        string sessionId, int? lastN = null, CancellationToken ct = default)
    {
        var key = MessagesKey(sessionId);
        RedisValue[] raw;
        if (lastN.HasValue)
        {
            // LRANGE with negative start to get last N elements.
            raw = await _db.ListRangeAsync(key, -lastN.Value, -1);
        }
        else
        {
            raw = await _db.ListRangeAsync(key, 0, -1);
        }

        var result = new List<AgentMessage>(raw.Length);
        foreach (var item in raw)
        {
            if (!item.HasValue) continue;
            var msg = JsonSerializer.Deserialize<AgentMessage>((string)item!, JsonOpts);
            if (msg is not null) result.Add(msg);
        }
        return result;
    }

    public async Task<string?> GetSessionSummaryAsync(
        string sessionId, CancellationToken ct = default)
    {
        var raw = await _db.StringGetAsync(SummaryKey(sessionId));
        return raw.HasValue ? raw.ToString() : null;
    }

    public async Task UpdateSessionSummaryAsync(
        string sessionId, string summary, CancellationToken ct = default)
    {
        await _db.StringSetAsync(SummaryKey(sessionId), summary, _opts.SessionTtl);

        var session = await GetSessionAsync(sessionId, ct);
        if (session is not null)
            await PersistSessionAsync(session with { UpdatedAt = DateTime.UtcNow }, ct);
    }

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        // Retrieve userId before deletion to clean user index.
        var session = await GetSessionAsync(sessionId, ct);

        // Delete all keys associated with this session atomically.
        var keys = new RedisKey[]
        {
            SessionKey(sessionId),
            MessagesKey(sessionId),
            SummaryKey(sessionId),
        };
        await _db.KeyDeleteAsync(keys);

        // Remove from user index if we know the userId.
        if (session is not null)
            await _db.SortedSetRemoveAsync(UserIndexKey(session.UserId), sessionId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Serializes and writes a session record, refreshing its TTL.</summary>
    private async Task PersistSessionAsync(Session session, CancellationToken _ = default)
    {
        var json = JsonSerializer.Serialize(session, JsonOpts);
        await _db.StringSetAsync(SessionKey(session.Id), json, _opts.SessionTtl);
    }

    private RedisKey SessionKey(string id)        => $"{_opts.KeyPrefix}:s:{id}";
    private RedisKey MessagesKey(string id)        => $"{_opts.KeyPrefix}:m:{id}";
    private RedisKey StateKey(string id, string t) => $"{_opts.KeyPrefix}:st:{id}:{t}";
    private RedisKey SummaryKey(string id)         => $"{_opts.KeyPrefix}:sum:{id}";
    private RedisKey UserIndexKey(string userId)   => $"{_opts.KeyPrefix}:u:{userId}";

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose() => _ownedMux?.Dispose();
}
