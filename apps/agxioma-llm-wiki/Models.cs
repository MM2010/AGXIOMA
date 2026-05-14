namespace AgxiomaLLMWiki;

// ── Configuration ─────────────────────────────────────────────────────────────

public sealed record WikiSettings
{
    /// <summary>Root of the AGXIOMA project to ingest.</summary>
    public string ProjectRootPath { get; init; } = ".";

    /// <summary>Where to write the generated wiki markdown files.</summary>
    public string WikiOutputPath  { get; init; } = "wiki";

    /// <summary>LLM model for wiki generation.</summary>
    public string LLMModel       { get; init; } = "deepseek-v4-flash";

    /// <summary>LLM provider identifier (deepseek, openai, ollama, etc.).</summary>
    public string LLMProvider    { get; init; } = "deepseek";

    /// <summary>Ollama base URL for embeddings.</summary>
    public string OllamaBaseUrl  { get; init; } = "http://localhost:11434/";

    /// <summary>Ollama embedding model.</summary>
    public string EmbedModel     { get; init; } = "mxbai-embed-large";

    /// <summary>Dimension count of the embedding model.</summary>
    public int    EmbedDimensions { get; init; } = 1024;

    /// <summary>Max tokens per chunk when splitting source files.</summary>
    public int    ChunkMaxTokens { get; init; } = 512;

    /// <summary>Token overlap between consecutive chunks.</summary>
    public int    ChunkOverlap   { get; init; } = 64;

    /// <summary>
    /// Hard character limit per chunk sent to the embedder.
    /// mxbai-embed-large: 512 token context. C# code ≈ 3.5 chars/token → 1200 chars ≈ 340 tokens (safe).
    /// The latency-based probe is unreliable because BPE compresses repeated chars,
    /// causing it to overestimate the real limit for code/text.
    /// </summary>
    public int    EmbedMaxChars  { get; init; } = 1200;

    /// <summary>Number of top chunks to retrieve during vector search.</summary>
    public int    TopK           { get; init; } = 8;

    /// <summary>Minimum similarity score for retrieved chunks.</summary>
    public double MinScore       { get; init; } = 0.45;

    /// <summary>File extensions to include in ingestion.</summary>
    public string[] SourceExtensions { get; init; } =
        [".cs", ".md", ".json", ".csproj", ".props", ".txt", ".yml", ".yaml"];

    /// <summary>Directory name segments to exclude from ingestion.</summary>
    public string[] SourceExcludes { get; init; } =
        ["bin", "obj", "nupkgs", "packages", ".git", "wiki"];

    /// <summary>
    /// Max number of files processed concurrently.
    /// Parallelises DeepSeek LLM calls (network I/O). Embed calls (local Ollama) remain
    /// sequential per-file to avoid overloading the GPU. Default 4.
    /// </summary>
    public int IngestParallelism { get; init; } = 4;
}

// ── Ingest profile (runtime-injectable tunable parameters) ────────────────────

/// <summary>
/// All parameters that affect ingestion behaviour and can vary per-run.
/// Unlike <see cref="WikiSettings"/> (infrastructure/static config), an
/// <see cref="IngestProfile"/> can be sourced from appsettings, a database
/// row, a REST API, environment variables, or any composite thereof via
/// <see cref="IIngestProfileProvider"/>.
/// </summary>
public sealed record IngestProfile
{
    /// <summary>File extensions to include in ingestion.</summary>
    public string[] SourceExtensions  { get; init; } =
        [".cs", ".md", ".json", ".csproj", ".props", ".txt", ".yml", ".yaml"];

    /// <summary>Directory name segments to exclude from ingestion.</summary>
    public string[] SourceExcludes    { get; init; } =
        ["bin", "obj", "nupkgs", "packages", ".git", "wiki"];

    /// <summary>Max concurrent files (parallelises LLM network I/O).</summary>
    public int      IngestParallelism { get; init; } = 4;

    /// <summary>Max characters per chunk sent to the embedder.</summary>
    public int      EmbedMaxChars     { get; init; } = 1200;

    /// <summary>Max tokens per chunk when splitting source files.</summary>
    public int      ChunkMaxTokens    { get; init; } = 512;

    /// <summary>Token overlap between consecutive chunks.</summary>
    public int      ChunkOverlap      { get; init; } = 64;

    /// <summary>Number of top chunks to retrieve during vector search.</summary>
    public int      TopK              { get; init; } = 8;

    /// <summary>Minimum cosine similarity score for retrieved chunks.</summary>
    public double   MinScore          { get; init; } = 0.45;
}

/// <summary>
/// Abstraction over the source of an <see cref="IngestProfile"/>.
/// Implement this interface to load profiles from appsettings, a database,
/// a remote API, environment variables, or any composite chain.
/// </summary>
public interface IIngestProfileProvider
{
    /// <summary>
    /// Returns the active <see cref="IngestProfile"/> for the current run.
    /// Called once per ingest/query invocation, so implementations may
    /// reload from their source on every call if desired.
    /// </summary>
    Task<IngestProfile> GetProfileAsync(CancellationToken ct = default);
}

/// <summary>
/// Default implementation: reads the profile from the bound
/// <see cref="WikiSettings"/> section in appsettings.json.
/// Swap this registration for any other <see cref="IIngestProfileProvider"/>
/// to change the config source without touching business logic.
/// </summary>
public sealed class AppSettingsProfileProvider(WikiSettings settings) : IIngestProfileProvider
{
    public Task<IngestProfile> GetProfileAsync(CancellationToken ct = default) =>
        Task.FromResult(new IngestProfile
        {
            SourceExtensions  = settings.SourceExtensions,
            SourceExcludes    = settings.SourceExcludes,
            IngestParallelism = settings.IngestParallelism,
            EmbedMaxChars     = settings.EmbedMaxChars,
            ChunkMaxTokens    = settings.ChunkMaxTokens,
            ChunkOverlap      = settings.ChunkOverlap,
            TopK              = settings.TopK,
            MinScore          = settings.MinScore,
        });
}

// ── Database row models ───────────────────────────────────────────────────────

/// <summary>Tracks a source file that has been ingested.</summary>
public sealed record WikiSource
{
    public int    Id           { get; init; }
    public string RelativePath { get; init; } = "";
    public string FullPath     { get; init; } = "";
    public string FileType     { get; init; } = "";
    public string ContentHash  { get; init; } = "";
    public long   FileSizeBytes { get; init; }
    public DateTime IngestedAt { get; init; }
    public DateTime UpdatedAt  { get; init; }
}

/// <summary>Tracks a generated wiki page.</summary>
public sealed record WikiPage
{
    public int    Id          { get; init; }
    public string Slug        { get; init; } = "";   // e.g. "entities/AgxiomeBuilder"
    public string Title       { get; init; } = "";
    public string Category    { get; init; } = "";   // entity|concept|source|overview
    public string Summary     { get; init; } = "";
    public string FilePath    { get; init; } = "";   // absolute path to wiki/*.md
    public int    SourceCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Append-only log entry.</summary>
public sealed record WikiLogEntry
{
    public int    Id        { get; init; }
    public string OpType    { get; init; } = "";   // ingest|query|lint|update
    public string Title     { get; init; } = "";
    public string Details   { get; init; } = "";
    public DateTime CreatedAt { get; init; }
}

// ── Request / Response models ─────────────────────────────────────────────────

public sealed record IngestJobRequest(
    string? SubPath = null,          // optional sub-path to ingest (default: full project)
    bool    Force   = false          // re-ingest even if hash unchanged
);

public sealed record IngestJobResponse(
    string  JobId,
    string  Status,
    int     FilesQueued,
    string  Message
);

public sealed record IngestProgressResponse(
    string  JobId,
    string  Status,
    int     FilesTotal,
    int     FilesProcessed,
    int     PagesCreated,
    int     PagesUpdated,
    string? CurrentFile,
    string? ErrorMessage
);

public sealed record WikiQueryRequest(
    string Question,
    int?   TopK     = null,
    double? MinScore = null
);

public sealed record WikiQueryResponse(
    string            Question,
    string            Answer,
    List<WikiPageRef> Sources,
    bool              ContextFound,
    double            LatencyMs
);

public sealed record WikiPageRef(
    string Slug,
    string Title,
    string Snippet,
    float  Score
);

public sealed record WikiPageListResponse(
    int              Total,
    List<WikiPageSummary> Pages
);

public sealed record WikiPageSummary(
    string   Slug,
    string   Title,
    string   Category,
    string   Summary,
    int      SourceCount,
    DateTime UpdatedAt
);

public sealed record WikiPageContentResponse(
    string   Slug,
    string   Title,
    string   Category,
    string   Content,
    DateTime UpdatedAt
);

public sealed record LintReport(
    int              OrphanPages,
    int              MissingLinks,
    int              StalePages,
    List<LintIssue>  Issues,
    string           Summary,
    DateTime         RunAt
);

public sealed record LintIssue(
    string Type,      // orphan|stale|missing_link|contradiction
    string PageSlug,
    string Description
);

public sealed record WikiStatsResponse(
    int      TotalSources,
    int      TotalPages,
    int      TotalLogEntries,
    int      TotalChunks,
    DateTime LastIngestAt,
    DateTime LastQueryAt
);

// ── In-memory job state ───────────────────────────────────────────────────────

public sealed class IngestJobState
{
    public string   JobId          { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string   Status         { get; set; } = "queued";   // queued|running|done|error
    public int      FilesTotal     { get; set; }
    public int      FilesProcessed { get; set; }
    public int      PagesCreated   { get; set; }
    public int      PagesUpdated   { get; set; }
    public string?  CurrentFile    { get; set; }
    public string?  ErrorMessage   { get; set; }
    public DateTime StartedAt      { get; init; } = DateTime.UtcNow;
}
