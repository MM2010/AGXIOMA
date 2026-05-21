using Dapper;
using Npgsql;

namespace AGXIOME.Storage;

/// <summary>
/// Creates the required database tables if they don't exist.
/// Call once at application startup before using any storage services.
///
/// Usage:
///   var initializer = new StorageSchemaInitializer(connStr);
///   await initializer.EnsureSchemaAsync();
/// </summary>
public sealed class StorageSchemaInitializer
{
    private readonly string _connectionString;

    public StorageSchemaInitializer(string connectionString) =>
        _connectionString = connectionString;

    /// <summary>
    /// Creates all AGXIOME storage tables if they do not exist.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        // Enable pgvector extension (required for agent_memory.embedding)
        await conn.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS vector");

        // ── Sessions ──────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS sessions (
                id          VARCHAR(64) PRIMARY KEY,
                user_id     VARCHAR(64) NOT NULL,
                component_id VARCHAR(64) NOT NULL,
                type        VARCHAR(16) NOT NULL,
                name        VARCHAR(255),
                session_state JSONB DEFAULT '{}',
                metadata    JSONB DEFAULT '{}',
                summary     TEXT,
                created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )");

        // ── Session messages ──────────────────────────────────────
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS session_messages (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                session_id      VARCHAR(64) NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
                role            VARCHAR(32) NOT NULL,
                content         TEXT NOT NULL,
                tool_calls_json JSONB,
                metadata_json   JSONB,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )");

        // ── Indexes for sessions ──────────────────────────────────
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_sessions_user_id ON sessions(user_id)");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_sessions_type ON sessions(type)");
        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_session_messages_session ON session_messages(session_id)");

        // ── Agent memory (pgvector) ───────────────────────────────
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS agent_memory (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                agent_id        VARCHAR(64),
                content         TEXT NOT NULL,
                embedding       VECTOR(1536),
                metadata_json   JSONB DEFAULT '{}',
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )");

        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_agent_memory_agent ON agent_memory(agent_id)");

        // ── Outbox ────────────────────────────────────────────────
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS outbox_messages (
                id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                event_type      VARCHAR(255) NOT NULL,
                event_body_json JSONB NOT NULL,
                correlation_id  VARCHAR(64),
                processed_at    TIMESTAMPTZ,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
            )");

        await conn.ExecuteAsync("CREATE INDEX IF NOT EXISTS idx_outbox_unprocessed ON outbox_messages(created_at) WHERE processed_at IS NULL");
    }
}
