using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AgxiomaDynamicSkillLab.Storage;

/// <summary>
/// SQLite store for tool manifests, source code, audit logs, and persistent skills.
/// </summary>
public sealed class SkillLabStore
{
    private readonly string _connStr;

    // In-memory tool registry for quick lookups
    private readonly ConcurrentDictionary<string, ToolDescriptor> _tools = new();

    public SkillLabStore(string dbPath)
    {
        _connStr = $"Data Source={dbPath}";
    }

    public async Task InitAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS generated_tools (
                id              TEXT PRIMARY KEY,
                name            TEXT NOT NULL,
                description     TEXT NOT NULL,
                source_code     TEXT NOT NULL,
                manifest_json   TEXT NOT NULL,
                status          TEXT NOT NULL,
                created_at      TEXT NOT NULL,
                validation_errors TEXT,
                compilation_errors TEXT,
                test_output     TEXT
            );

            CREATE TABLE IF NOT EXISTS audit_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id  TEXT NOT NULL,
                tool_id     TEXT,
                step        TEXT NOT NULL,
                status      TEXT NOT NULL,
                details     TEXT,
                recorded_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS persistent_skills (
                id           TEXT PRIMARY KEY,
                name         TEXT NOT NULL,
                description  TEXT NOT NULL,
                source_code  TEXT NOT NULL,
                manifest_json TEXT NOT NULL,
                hash         TEXT NOT NULL,
                version      TEXT NOT NULL,
                created_at   TEXT NOT NULL,
                promoted_at  TEXT,
                use_count    INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS ix_audit_session ON audit_log(session_id);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ─── Tool persistence ────────────────────────────────────────────────────

    public async Task SaveToolAsync(ToolDescriptor tool, CancellationToken ct = default)
    {
        _tools[tool.Id] = tool;

        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO generated_tools
                (id, name, description, source_code, manifest_json, status, created_at,
                 validation_errors, compilation_errors, test_output)
            VALUES ($id, $name, $desc, $src, $manifest, $status, $created,
                    $valErrors, $compErrors, $testOut)
            """;
        cmd.Parameters.AddWithValue("$id",        tool.Id);
        cmd.Parameters.AddWithValue("$name",      tool.Name);
        cmd.Parameters.AddWithValue("$desc",      tool.Description);
        cmd.Parameters.AddWithValue("$src",       tool.SourceCode);
        cmd.Parameters.AddWithValue("$manifest",  tool.ManifestJson);
        cmd.Parameters.AddWithValue("$status",    tool.Status.ToString());
        cmd.Parameters.AddWithValue("$created",   tool.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$valErrors", tool.ValidationErrors is { Count: >0 } ? JsonSerializer.Serialize(tool.ValidationErrors) : DBNull.Value);
        cmd.Parameters.AddWithValue("$compErrors", tool.CompilationErrors is { Count: >0 } ? JsonSerializer.Serialize(tool.CompilationErrors) : DBNull.Value);
        cmd.Parameters.AddWithValue("$testOut",   tool.TestOutput is { Count: >0 } ? JsonSerializer.Serialize(tool.TestOutput) : DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public ToolDescriptor? GetTool(string toolId) =>
        _tools.TryGetValue(toolId, out var t) ? t : null;

    public List<ToolDescriptor> GetAllTools() => _tools.Values.ToList();

    public void UpdateStatus(string toolId, ToolStatus status) {
        if (_tools.TryGetValue(toolId, out var t))
            _tools[toolId] = t with { Status = status };
    }

    // ─── Audit log ───────────────────────────────────────────────────────────

    public async Task AuditAsync(string sessionId, string? toolId, string step,
        string status, string? details = null, CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO audit_log (session_id, tool_id, step, status, details, recorded_at)
            VALUES ($s, $t, $step, $status, $d, $now)
            """;
        cmd.Parameters.AddWithValue("$s",     (object?)sessionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$t",     (object?)toolId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$step",  step);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$d",     details ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$now",   DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ─── Persistent skills ───────────────────────────────────────────────────

    public async Task PromoteSkillAsync(ToolDescriptor tool, CancellationToken ct = default)
    {
        var hash = ComputeHash(tool.SourceCode);

        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO persistent_skills
                (id, name, description, source_code, manifest_json, hash, version, created_at, promoted_at, use_count)
            VALUES ($id, $name, $desc, $src, $manifest, $hash, $ver, $created, $promoted, 0)
            """;
        cmd.Parameters.AddWithValue("$id",       tool.Id);
        cmd.Parameters.AddWithValue("$name",     tool.Name);
        cmd.Parameters.AddWithValue("$desc",     tool.Description);
        cmd.Parameters.AddWithValue("$src",      tool.SourceCode);
        cmd.Parameters.AddWithValue("$manifest", tool.ManifestJson);
        cmd.Parameters.AddWithValue("$hash",     hash);
        cmd.Parameters.AddWithValue("$ver",      "1.0.0");
        cmd.Parameters.AddWithValue("$created",  tool.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$promoted", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync(ct);

        _tools[tool.Id] = tool with { Status = ToolStatus.Promoted };
    }

    public async Task<List<PersistentSkill>> GetPersistentSkillsAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct);

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, name, description, source_code, manifest_json, hash, version, created_at, promoted_at, use_count FROM persistent_skills ORDER BY promoted_at DESC";

        var results = new List<PersistentSkill>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new PersistentSkill(
                Id: reader.GetString(0), Name: reader.GetString(1),
                Description: reader.GetString(2), SourceCode: reader.GetString(3),
                ManifestJson: reader.GetString(4), Hash: reader.GetString(5),
                Version: reader.GetString(6),
                CreatedAt: DateTime.Parse(reader.GetString(7)),
                PromotedAt: reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                UseCount: reader.GetInt32(9)
            ));
        }
        return results;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string ComputeHash(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
}
