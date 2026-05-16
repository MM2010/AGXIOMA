using Dapper;
using Npgsql;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace AgxiomaLLMWiki;

/// <summary>
/// Manages the PostgreSQL LLMWikiTest database:
///   - wiki_sources    — tracks ingested source files
///   - wiki_pages      — tracks generated wiki pages
///   - wiki_log        — append-only operation log
///   - wiki_embeddings — pgvector table for semantic search (1024 dims, mxbai-embed-large)
///
/// Redis is used for:
///   - Ingest job state (TTL 24h)
///   - Frequently queried page cache (TTL 10 min)
/// </summary>
public sealed class WikiDatabase : IWikiDatabase
{
    private readonly string _connStr;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<WikiDatabase>   _log;

    private const string EmbeddingsTable = "wiki_embeddings";
    private const int    EmbedDimensions = 1024;

    public WikiDatabase(
        string connectionString,
        IConnectionMultiplexer? redis,
        ILogger<WikiDatabase> log)
    {
        _connStr = connectionString;
        _redis   = redis;
        _log     = log;
    }

    // ── Schema bootstrap ───────────────────────────────────────────────────────

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // Enable pgvector extension
        await conn.ExecuteAsync("CREATE EXTENSION IF NOT EXISTS vector;");

        // wiki_sources
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS wiki_sources (
                id             SERIAL PRIMARY KEY,
                relative_path  TEXT NOT NULL UNIQUE,
                full_path      TEXT NOT NULL,
                file_type      TEXT NOT NULL,
                content_hash   TEXT NOT NULL,
                file_size_bytes BIGINT NOT NULL DEFAULT 0,
                ingested_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );");

        // wiki_pages
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS wiki_pages (
                id           SERIAL PRIMARY KEY,
                slug         TEXT NOT NULL UNIQUE,
                title        TEXT NOT NULL,
                category     TEXT NOT NULL,
                summary      TEXT NOT NULL DEFAULT '',
                file_path    TEXT NOT NULL,
                source_count INT NOT NULL DEFAULT 0,
                created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );");

        // wiki_log
        await conn.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS wiki_log (
                id         SERIAL PRIMARY KEY,
                op_type    TEXT NOT NULL,
                title      TEXT NOT NULL,
                details    TEXT NOT NULL DEFAULT '',
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );");

        // wiki_embeddings (pgvector) — ensure correct vector dimension
        // pgvector stores atttypmod = N for vector(N); -1 means no constraint
        const int expectedTypmod = EmbedDimensions;
        var existingTypmod = await conn.ExecuteScalarAsync<int?>(
            $"SELECT atttypmod FROM pg_attribute " +
            $"WHERE attrelid = '{EmbeddingsTable}'::regclass AND attname = 'vector'");
        if (existingTypmod.HasValue && existingTypmod.Value != expectedTypmod)
        {
            _log.LogWarning(
                "[WikiDB] wiki_embeddings vector dimension mismatch (atttypmod={Current}, expected {Expected}). Dropping and recreating…",
                existingTypmod.Value, expectedTypmod);
            await conn.ExecuteAsync($"DROP TABLE IF EXISTS {EmbeddingsTable} CASCADE;");
        }

        await conn.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS {EmbeddingsTable} (
                id           TEXT NOT NULL PRIMARY KEY,
                page_slug    TEXT NOT NULL,
                vector       vector({EmbedDimensions}) NOT NULL,
                text_content TEXT,
                metadata     JSONB
            );");

        // Index for ANN search on embeddings
        await conn.ExecuteAsync($@"
            CREATE INDEX IF NOT EXISTS idx_wiki_embeddings_vector
            ON {EmbeddingsTable} USING hnsw (vector vector_cosine_ops);");

        _log.LogInformation("[WikiDB] Schema ensured (wiki_sources, wiki_pages, wiki_log, wiki_embeddings)");
    }

    // ── Source tracking ────────────────────────────────────────────────────────

    public async Task<WikiSource?> GetSourceByPathAsync(string relativePath, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<WikiSource>(
            "SELECT id, relative_path AS RelativePath, full_path AS FullPath, file_type AS FileType, " +
            "content_hash AS ContentHash, file_size_bytes AS FileSizeBytes, " +
            "ingested_at AS IngestedAt, updated_at AS UpdatedAt " +
            "FROM wiki_sources WHERE relative_path = @RelativePath",
            new { RelativePath = relativePath });
    }

    public async Task UpsertSourceAsync(WikiSource source, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            INSERT INTO wiki_sources (relative_path, full_path, file_type, content_hash, file_size_bytes, ingested_at, updated_at)
            VALUES (@RelativePath, @FullPath, @FileType, @ContentHash, @FileSizeBytes, NOW(), NOW())
            ON CONFLICT (relative_path) DO UPDATE
                SET full_path       = EXCLUDED.full_path,
                    file_type       = EXCLUDED.file_type,
                    content_hash    = EXCLUDED.content_hash,
                    file_size_bytes = EXCLUDED.file_size_bytes,
                    updated_at      = NOW();",
            source);
    }

    public async Task<int> CountSourcesAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM wiki_sources", commandTimeout: 10);
    }

    public async Task<DateTime?> GetLastIngestAtAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<DateTime?>("SELECT MAX(updated_at) FROM wiki_sources");
    }

    // ── Wiki page tracking ─────────────────────────────────────────────────────

    public async Task UpsertPageAsync(WikiPage page, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(@"
            INSERT INTO wiki_pages (slug, title, category, summary, file_path, source_count, created_at, updated_at)
            VALUES (@Slug, @Title, @Category, @Summary, @FilePath, @SourceCount, NOW(), NOW())
            ON CONFLICT (slug) DO UPDATE
                SET title        = EXCLUDED.title,
                    category     = EXCLUDED.category,
                    summary      = EXCLUDED.summary,
                    file_path    = EXCLUDED.file_path,
                    source_count = EXCLUDED.source_count,
                    updated_at   = NOW();",
            page);
    }

    public async Task<WikiPage?> GetPageBySlugAsync(string slug, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<WikiPage>(
            "SELECT id, slug AS Slug, title AS Title, category AS Category, summary AS Summary, " +
            "file_path AS FilePath, source_count AS SourceCount, created_at AS CreatedAt, updated_at AS UpdatedAt " +
            "FROM wiki_pages WHERE slug = @Slug",
            new { Slug = slug });
    }

    public async Task<List<WikiPage>> ListPagesAsync(string? category = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        var sql = "SELECT id, slug AS Slug, title AS Title, category AS Category, summary AS Summary, " +
                  "file_path AS FilePath, source_count AS SourceCount, created_at AS CreatedAt, updated_at AS UpdatedAt " +
                  "FROM wiki_pages";
        if (category is not null)
            sql += " WHERE category = @Category";
        sql += " ORDER BY category, title";
        var result = await conn.QueryAsync<WikiPage>(sql, category is null ? null : new { Category = category });
        return result.ToList();
    }

    public async Task<int> CountPagesAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM wiki_pages", commandTimeout: 10);
    }

    // ── Log operations ─────────────────────────────────────────────────────────

    public async Task AppendLogAsync(string opType, string title, string details = "", CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            "INSERT INTO wiki_log (op_type, title, details) VALUES (@OpType, @Title, @Details)",
            new { OpType = opType, Title = title, Details = details });
    }

    public async Task<List<WikiLogEntry>> GetLogAsync(int limit = 50, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        var result = await conn.QueryAsync<WikiLogEntry>(
            "SELECT id, op_type AS OpType, title AS Title, details AS Details, created_at AS CreatedAt " +
            "FROM wiki_log ORDER BY created_at DESC LIMIT @Limit",
            new { Limit = limit });
        return result.ToList();
    }

    public async Task<int> CountLogEntriesAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM wiki_log", commandTimeout: 10);
    }

    public async Task<DateTime?> GetLastQueryAtAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<DateTime?>(
            "SELECT MAX(created_at) FROM wiki_log WHERE op_type = 'query'");
    }

    // ── Vector embeddings (pgvector) ───────────────────────────────────────────

    public async Task UpsertEmbeddingAsync(
        string id,
        string pageSlug,
        float[] vector,
        string? textContent,
        Dictionary<string, string>? metadata,
        CancellationToken ct = default)
    {
        var vecStr  = "[" + string.Join(",", vector.Select(v => v.ToString("G6", CultureInfo.InvariantCulture))) + "]";
        var metaJson = metadata is null ? null : JsonSerializer.Serialize(metadata);

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            $@"INSERT INTO {EmbeddingsTable} (id, page_slug, vector, text_content, metadata)
               VALUES (@Id, @PageSlug, @Vec::vector, @Text, @Meta::jsonb)
               ON CONFLICT (id) DO UPDATE
                 SET page_slug    = EXCLUDED.page_slug,
                     vector       = EXCLUDED.vector,
                     text_content = EXCLUDED.text_content,
                     metadata     = EXCLUDED.metadata;",
            new { Id = id, PageSlug = pageSlug, Vec = vecStr, Text = textContent, Meta = metaJson },
            commandTimeout: 30);
    }

    public async Task<List<(string PageSlug, string TextContent, float Score)>> SearchEmbeddingsAsync(
        float[] queryVector,
        int topK,
        float minScore,
        CancellationToken ct = default)
    {
        var vecStr = "[" + string.Join(",", queryVector.Select(v => v.ToString("G6", CultureInfo.InvariantCulture))) + "]";

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);

        // cosine similarity = 1 - cosine_distance
        var rows = await conn.QueryAsync<(string page_slug, string text_content, double score)>(
            $@"SELECT page_slug, text_content,
                      (1 - (vector <=> @Vec::vector)) AS score
               FROM {EmbeddingsTable}
               WHERE (1 - (vector <=> @Vec::vector)) >= @MinScore
               ORDER BY vector <=> @Vec::vector
               LIMIT @TopK",
            new { Vec = vecStr, MinScore = (double)minScore, TopK = topK },
            commandTimeout: 30);

        return rows.Select(r => (r.page_slug, r.text_content, (float)r.score)).ToList();
    }

    public async Task<int> CountEmbeddingsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {EmbeddingsTable}", commandTimeout: 10);
    }

    public async Task DeleteEmbeddingsBySlugAsync(string pageSlug, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            $"DELETE FROM {EmbeddingsTable} WHERE page_slug = @Slug",
            new { Slug = pageSlug });
    }

    // ── Redis: ingest job state ────────────────────────────────────────────────

    public async Task SaveJobStateAsync(IngestJobState state, CancellationToken ct = default)
    {
        if (_redis is null) return;
        try
        {
            var db  = _redis.GetDatabase();
            var key = $"llmwiki:job:{state.JobId}";
            await db.StringSetAsync(key, JsonSerializer.Serialize(state), TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _log.LogDebug("[WikiDB] Redis SaveJobState failed (degraded): {Error}", ex.Message);
        }
    }

    public async Task<IngestJobState?> GetJobStateAsync(string jobId, CancellationToken ct = default)
    {
        if (_redis is null) return null;
        try
        {
            var db  = _redis.GetDatabase();
            var val = await db.StringGetAsync($"llmwiki:job:{jobId}");
            if (!val.HasValue) return null;
            return JsonSerializer.Deserialize<IngestJobState>(val.ToString());
        }
        catch (Exception ex)
        {
            _log.LogDebug("[WikiDB] Redis GetJobState failed (degraded): {Error}", ex.Message);
            return null;
        }
    }

    // ── Redis: page content cache ──────────────────────────────────────────────

    public async Task CachePageContentAsync(string slug, string content, CancellationToken ct = default)
    {
        if (_redis is null) return;
        try
        {
            var db = _redis.GetDatabase();
            await db.StringSetAsync($"llmwiki:page:{slug}", content, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _log.LogDebug("[WikiDB] Redis CachePage failed (degraded): {Error}", ex.Message);
        }
    }

    public async Task<string?> GetCachedPageContentAsync(string slug, CancellationToken ct = default)
    {
        if (_redis is null) return null;
        try
        {
            var db  = _redis.GetDatabase();
            var val = await db.StringGetAsync($"llmwiki:page:{slug}");
            return val.HasValue ? val.ToString() : null;
        }
        catch (Exception ex)
        {
            _log.LogDebug("[WikiDB] Redis GetCachedPage failed (degraded): {Error}", ex.Message);
            return null;
        }
    }

    public async Task InvalidatePageCacheAsync(string slug)
    {
        if (_redis is null) return;
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync($"llmwiki:page:{slug}");
        }
        catch (Exception ex)
        {
            _log.LogDebug("[WikiDB] Redis InvalidateCache failed (degraded): {Error}", ex.Message);
        }
    }
}
