using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Manages agent session lifecycle: create, persist, resume, and delete sessions with full chat history and mutable session state. Sessions survive across agent restarts.</summary>
/// <remarks>Sessions store conversation history, session state (key-value), and an auto-generated summary. Backed by the configured database provider.</remarks>
public interface ISessionManager
{
    Task<Session> CreateSessionAsync(string userId, string componentId, SessionType type, SessionOptions? options = null, CancellationToken ct = default);
    Task<Session?> GetSessionAsync(string sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> ListSessionsAsync(string userId, SessionType? type = null, int limit = 50, CancellationToken ct = default);
    Task UpdateSessionStateAsync<T>(string sessionId, T state, CancellationToken ct = default);
    Task<T?> GetSessionStateAsync<T>(string sessionId, CancellationToken ct = default);
    Task AddMessageAsync(string sessionId, AgentMessage msg, CancellationToken ct = default);
    Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(string sessionId, int? lastN = null, CancellationToken ct = default);
    Task<string?> GetSessionSummaryAsync(string sessionId, CancellationToken ct = default);
    Task UpdateSessionSummaryAsync(string sessionId, string summary, CancellationToken ct = default);
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>Mutable key-value state store scoped to a single session. Thread-safe. Values are serialized as JSON.</summary>
public interface ISessionState
{
    T? Get<T>(string key);
    void Set<T>(string key, T value);
    bool ContainsKey(string key);
    Dictionary<string, object> ToDictionary();
}
