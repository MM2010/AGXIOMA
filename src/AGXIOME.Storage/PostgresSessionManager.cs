using Dapper;
using Npgsql;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage;

/// <summary>
/// PostgreSQL-backed session manager with chat history persistence.
///
/// Uses Dapper + Npgsql. All methods write to real tables.
/// Schema must exist — use <see cref="StorageSchemaInitializer"/> to auto-create.
///
/// Thread-safe. Each method opens a fresh connection from the pool.
/// </summary>
public sealed class PostgresSessionManager : ISessionManager
{
    private readonly string _connectionString;

    public PostgresSessionManager(string connectionString) =>
        _connectionString = connectionString;

    // ── Session lifecycle ─────────────────────────────────────────

    public async Task<AgentSession> CreateSessionAsync(
        string userId, string componentId, SessionType type,
        SessionOptions? options = null, CancellationToken ct = default)
    {
        var session = new AgentSession(
            Id:          Guid.NewGuid().ToString(),
            UserId:      userId,
            ComponentId: componentId,
            Type:        type,
            Name:        options?.Name,
            State:       new Dictionary<string, object>(),
            Metadata:    options?.Metadata,
            CreatedAt:   DateTime.UtcNow
        );

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO sessions (id, user_id, component_id, type, name, session_state, metadata, summary, created_at, updated_at)
              VALUES (@Id, @UserId, @ComponentId, @Type::VARCHAR(16), @Name, @State::JSONB, @Metadata::JSONB, @Summary, @CreatedAt, @UpdatedAt)",
            new
            {
                session.Id, session.UserId, session.ComponentId,
                Type = type.ToString(), session.Name,
                State = "{}", Metadata = SerializeJson(options?.Metadata),
                Summary = (string?)null,
                session.CreatedAt, UpdatedAt = session.CreatedAt
            }, commandTimeout: 30);

        return session;
    }

    public async Task<AgentSession?> GetSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<AgentSession>(
            @"SELECT id, user_id AS UserId, component_id AS ComponentId,
                     type::TEXT AS Type, name, session_state AS State,
                     metadata, summary, created_at AS CreatedAt, updated_at AS UpdatedAt
              FROM sessions WHERE id = @Id",
            new { Id = sessionId }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<AgentSession>> ListSessionsAsync(
        string userId, SessionType? type = null, int limit = 50, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var sql = @"SELECT id, user_id AS UserId, component_id AS ComponentId,
                           type::TEXT AS Type, name, session_state AS State,
                           metadata, summary, created_at AS CreatedAt, updated_at AS UpdatedAt
                    FROM sessions WHERE user_id = @UserId";

        if (type.HasValue)
            sql += " AND type = @Type::VARCHAR(16)";

        sql += " ORDER BY created_at DESC LIMIT @Limit";

        var result = await conn.QueryAsync<AgentSession>(sql,
            new { UserId = userId, Type = type?.ToString(), Limit = limit },
            commandTimeout: 30);

        return result.ToList();
    }

    // ── Session state ─────────────────────────────────────────────

    public async Task UpdateSessionStateAsync<T>(
        string sessionId, T state, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            @"UPDATE sessions SET session_state = @State::JSONB, updated_at = NOW()
              WHERE id = @Id",
            new { Id = sessionId, State = SerializeJson(state) },
            commandTimeout: 30);
    }

    public async Task<T?> GetSessionStateAsync<T>(
        string sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var raw = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT session_state::TEXT FROM sessions WHERE id = @Id",
            new { Id = sessionId }, commandTimeout: 30);

        if (raw is null) return default;
        try { return DeserializeJson<T>(raw); }
        catch { return default; }
    }

    // ── Message history ───────────────────────────────────────────

    public async Task AddMessageAsync(
        string sessionId, AgentMessage msg, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            @"INSERT INTO session_messages (session_id, role, content, tool_calls_json, metadata_json)
              VALUES (@SessionId, @Role, @Content, @ToolCalls::JSONB, @Metadata::JSONB)",
            new
            {
                SessionId = sessionId, msg.Role, msg.Content,
                ToolCalls = SerializeJson(new { msg.ToolCallId, msg.ToolName, msg.ToolArguments }),
                Metadata = "{}"
            }, commandTimeout: 30);
    }

    public async Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(
        string sessionId, int? lastN = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var sql = @"SELECT role, content, tool_calls_json AS ToolCallsJson
                    FROM session_messages
                    WHERE session_id = @SessionId
                    ORDER BY created_at";

        if (lastN.HasValue)
            sql = $"SELECT * FROM ({sql}) sub LIMIT @Limit";

        var result = await conn.QueryAsync<AgentMessage>(sql,
            new { SessionId = sessionId, Limit = lastN ?? int.MaxValue },
            commandTimeout: 30);

        return result.ToList();
    }

    // ── Summary ───────────────────────────────────────────────────

    public async Task<string?> GetSessionSummaryAsync(
        string sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        return await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT summary FROM sessions WHERE id = @Id",
            new { Id = sessionId }, commandTimeout: 30);
    }

    public async Task UpdateSessionSummaryAsync(
        string sessionId, string summary, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            @"UPDATE sessions SET summary = @Summary, updated_at = NOW()
              WHERE id = @Id",
            new { Id = sessionId, Summary = summary }, commandTimeout: 30);
    }

    // ── Delete ────────────────────────────────────────────────────

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.ExecuteAsync(
            "DELETE FROM sessions WHERE id = @Id",
            new { Id = sessionId }, commandTimeout: 30);
    }

    // ── JSON helpers ──────────────────────────────────────────────

    private static string SerializeJson<T>(T? obj) =>
        obj is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(obj);

    private static T? DeserializeJson<T>(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<T>(json);
}
