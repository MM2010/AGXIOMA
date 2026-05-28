using System.Security.Cryptography;
using System.Text;
using AgxiomaLLMWiki;

namespace AgxiomaLegalEmail.Services;

public sealed class LegalCodeIngestionService : IHostedService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<LegalCodeIngestionService> _log;
    private bool _ingested; private int _totalChunks;

    public bool IsIngested => _ingested;
    public int TotalChunks => _totalChunks;

    public LegalCodeIngestionService(IServiceProvider sp, ILogger<LegalCodeIngestionService> log)
    { _sp = sp; _log = log; }

    public Task StartAsync(CancellationToken ct)
    {
        _log.LogInformation("Checking legal codes state...");
        _ = RunIngestionAsync(ct);
        return Task.CompletedTask;
    }

    private async Task RunIngestionAsync(CancellationToken ct)
    {
        try
        {
            using var s = _sp.CreateScope();
            var db = s.ServiceProvider.GetRequiredService<WikiDatabase>();
            var settings = s.ServiceProvider.GetRequiredService<WikiSettings>();
            await db.EnsureSchemaAsync(ct);

            var cfg = s.ServiceProvider.GetRequiredService<IConfiguration>();
            var civilRel = cfg["Legal:CivilCodePath"] ?? "codice-civile";
            var penalRel = cfg["Legal:PenalCodePath"] ?? "codice-penale";
            var civilDir = Path.GetFullPath(Path.Combine(settings.ProjectRootPath, civilRel));
            var penalDir = Path.GetFullPath(Path.Combine(settings.ProjectRootPath, penalRel));
            var combined = $"{DirHash(civilDir)}:{DirHash(penalDir)}";

            var last = await LastStateAsync(db, ct);
            if (last == combined) { _ingested = true; _totalChunks = await ChunksAsync(db, ct); return; }

            _log.LogInformation("Ingesting codes into pgvector...");
            var ingester = s.ServiceProvider.GetRequiredService<IWikiIngester>();
            await ingester.RunAsync(new IngestJobState{JobId="civ"+Guid.NewGuid().ToString("N")[..9]}, civilRel, false, ct);
            await ingester.RunAsync(new IngestJobState{JobId="pen"+Guid.NewGuid().ToString("N")[..9]}, penalRel, false, ct);

            _totalChunks = await ChunksAsync(db, ct);
            await SaveStateAsync(db, combined, _totalChunks, ct);
            _ingested = true;
            _log.LogInformation("Done: {N} chunks.", _totalChunks);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        { _log.LogError(ex, "Ingestion failed."); }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static string DirHash(string fullPath)
    {
        if (!Directory.Exists(fullPath)) return "empty";
        var sb = new StringBuilder();
        foreach (var f in Directory.GetFiles(fullPath, "*.md", SearchOption.AllDirectories).Order())
            sb.Append($"{Path.GetFileName(f)}:{File.ReadAllText(f).Length}:");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    private static async Task<string?> LastStateAsync(WikiDatabase db, CancellationToken ct)
    {
        try { foreach (var l in await db.GetLogAsync(10, ct)) if (l.OpType == "ingestion") return l.Title; } catch { }
        return null;
    }

    private static Task SaveStateAsync(WikiDatabase db, string hash, int n, CancellationToken ct)
        => db.AppendLogAsync("ingestion", hash, $"chunks={n}", ct);

    private static async Task<int> ChunksAsync(WikiDatabase db, CancellationToken ct)
    {
        try { return await db.CountPagesAsync(ct); } catch { return 0; }
    }
}
