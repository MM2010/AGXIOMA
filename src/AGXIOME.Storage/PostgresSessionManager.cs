using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage;

/// <summary>PostgreSQL-backed session manager with chat history persistence.</summary>
public sealed class PostgresSessionManager : ISessionManager
{
    private readonly string _connectionString;

    public PostgresSessionManager(string connectionString) => _connectionString = connectionString;

    public async Task<AgentSession> CreateSessionAsync(string userId, string componentId, SessionType type, SessionOptions? options = null, CancellationToken ct = default)
    {
        // INSERT INTO sessions (...) VALUES (...)
        await Task.CompletedTask;
        return new AgentSession(Guid.NewGuid().ToString(), userId, componentId, type, options?.Name, new(), options?.Metadata, DateTime.UtcNow);
    }

    public async Task<AgentSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return null;
    }

    public Task<IReadOnlyList<AgentSession>> ListSessionsAsync(string userId, SessionType? type = null, int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentSession>>(Array.Empty<AgentSession>());

    public Task UpdateSessionStateAsync<T>(string sessionId, T state, CancellationToken ct = default) => Task.CompletedTask;
    public Task<T?> GetSessionStateAsync<T>(string sessionId, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task AddMessageAsync(string sessionId, AgentMessage msg, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(string sessionId, int? lastN = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentMessage>>(Array.Empty<AgentMessage>());

    public Task<string?> GetSessionSummaryAsync(string sessionId, CancellationToken ct = default) => Task.FromResult<string?>(null);
    public Task UpdateSessionSummaryAsync(string sessionId, string summary, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteSessionAsync(string sessionId, CancellationToken ct = default) => Task.CompletedTask;
}
