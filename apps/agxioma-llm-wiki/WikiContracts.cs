namespace AgxiomaLLMWiki;

public interface IWikiDatabase
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<int> CountPagesAsync(CancellationToken ct = default);
    Task<int> CountSourcesAsync(CancellationToken ct = default);
    Task<int> CountLogEntriesAsync(CancellationToken ct = default);
    Task SaveJobStateAsync(IngestJobState state, CancellationToken ct = default);
    Task<IngestJobState?> GetJobStateAsync(string jobId, CancellationToken ct = default);
    Task<List<WikiPage>> ListPagesAsync(string? category = null, CancellationToken ct = default);
    Task<WikiPage?> GetPageBySlugAsync(string slug, CancellationToken ct = default);
    Task<string?> GetCachedPageContentAsync(string slug, CancellationToken ct = default);
    Task CachePageContentAsync(string slug, string content, CancellationToken ct = default);
    Task<List<WikiLogEntry>> GetLogAsync(int limit = 50, CancellationToken ct = default);
    Task<DateTime?> GetLastIngestAtAsync(CancellationToken ct = default);
    Task<DateTime?> GetLastQueryAtAsync(CancellationToken ct = default);
}

public interface IWikiIngester
{
    Task RunAsync(IngestJobState job, string? subPath = null, bool force = false, CancellationToken ct = default);
}

public interface IWikiPageWriter
{
    Task<string?> ReadWikiPageAsync(string slug, CancellationToken ct = default);
}

public interface IWikiQueryEngine
{
    Task<WikiQueryResponse> QueryAsync(WikiQueryRequest request, CancellationToken ct = default);
}

public interface IWikiLintEngine
{
    Task<LintReport> RunLintAsync(CancellationToken ct = default);
}