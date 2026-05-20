using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Sess = AGXIOME.Shared.Models.AgentSession;

namespace AGXIOME.Core.Session;

/// <summary>In-memory session manager. Use PostgresSessionManager in production.</summary>
public sealed class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, Sess> _sessions = new();
    private readonly ConcurrentDictionary<string, List<AgentMessage>> _messages = new();

    public Task<Sess> CreateSessionAsync(string userId, string componentId, SessionType type, SessionOptions? options = null, CancellationToken ct = default)
    {
        var session = new Sess(
            Guid.CreateVersion7().ToString(),
            userId, componentId, type, options?.Name,
            new Dictionary<string, object>(),
            options?.Metadata,
            DateTime.UtcNow
        );
        _sessions[session.Id] = session;
        _messages[session.Id] = new();
        return Task.FromResult(session);
    }

    public Task<Sess?> GetSessionAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult(_sessions.GetValueOrDefault(sessionId));

    public Task<IReadOnlyList<Sess>> ListSessionsAsync(string userId, SessionType? type = null, int limit = 50, CancellationToken ct = default)
    {
        var query = _sessions.Values.Where(s => s.UserId == userId);
        if (type.HasValue) query = query.Where(s => s.Type == type.Value);
        return Task.FromResult<IReadOnlyList<AgentSession>>(query.TakeLast(limit).ToList());
    }

    public Task UpdateSessionStateAsync<T>(string sessionId, T state, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
            _sessions[sessionId] = s with { State = state as Dictionary<string, object> ?? s.State, UpdatedAt = DateTime.UtcNow };
        return Task.CompletedTask;
    }

    public Task<T?> GetSessionStateAsync<T>(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var s) && s.State is T t) return Task.FromResult<T?>(t);
        return Task.FromResult<T?>(default);
    }

    public Task AddMessageAsync(string sessionId, AgentMessage msg, CancellationToken ct = default)
    {
        if (_messages.ContainsKey(sessionId)) _messages[sessionId].Add(msg);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(string sessionId, int? lastN = null, CancellationToken ct = default)
    {
        if (!_messages.TryGetValue(sessionId, out var msgs)) return Task.FromResult<IReadOnlyList<AgentMessage>>(Array.Empty<AgentMessage>());
        var result = lastN.HasValue ? msgs.TakeLast(lastN.Value).ToList() : msgs.ToList();
        return Task.FromResult<IReadOnlyList<AgentMessage>>(result);
    }

    public Task<string?> GetSessionSummaryAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult(_sessions.GetValueOrDefault(sessionId)?.Name);

    public Task UpdateSessionSummaryAsync(string sessionId, string summary, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var s))
            _sessions[sessionId] = s with { Name = summary, UpdatedAt = DateTime.UtcNow };
        return Task.CompletedTask;
    }

    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);
        _messages.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }
}
