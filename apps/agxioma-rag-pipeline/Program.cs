using AGXIOME.Core;
using AGXIOME.Runtime;
using AGXIOME.Security.Rbac;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.Embedders;
using AGXIOME.Storage.VectorStores;
using AgxiomaRagPipeline;
using System.Text.Json;

// ── Bootstrap ─────────────────────────────────────────────────────────────────
var builder = WebApplication.CreateBuilder(args);
builder.AddAgxiomeSecurityDefaults();

const int MaxIngestFiles = 128;
const int MaxIngestDepth = 8;
const long MaxIngestFileSizeBytes = 512 * 1024;
string[] AllowedIngestExtensions = [".md", ".txt", ".json", ".csv"];

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();
var ingestRoot = Path.GetFullPath(builder.Configuration["Knowledge:IngestRoot"] ?? builder.Environment.ContentRootPath);

// Embedder — Ollama nomic-embed-text (768 dims, zero config) or OpenAISmall
builder.Services.AddHttpClient("embedder");
builder.Services.AddSingleton<IEmbedder>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("embedder");

    // Ollama locale con mxbai-embed-large (1024 dims, zero API key, zero config)
    return new OllamaMxbaiEmbedder(http);
});

// Chunker — RecursiveChunker respects paragraph → line → sentence boundaries
builder.Services.AddSingleton<IChunker, RecursiveChunker>();

// Vector store — HNSWLib in-memory (zero dependencies, swappable for PgVector/Qdrant)
builder.Services.AddSingleton<IVectorStore, HNSWLibStore>();

// Knowledge base — orchestrates chunk→embed→upsert and search→rerank
builder.Services.AddSingleton<KnowledgeBase>(sp => new KnowledgeBase(
    sp.GetRequiredService<IChunker>(),
    sp.GetRequiredService<IEmbedder>(),
    sp.GetRequiredService<IVectorStore>(),
    new KnowledgeBaseOptions
    {
        ChunkMaxTokens = cfg.ChunkMaxTokens,
        ChunkOverlap   = cfg.ChunkOverlap,
        DefaultSearchOptions = new KnowledgeSearchOptions(TopK: cfg.TopK, MinScore: cfg.MinScore)
    }
));

// RAG agent — uses KnowledgeBase to inject context into every request
builder.Services.AddSingleton<IAgxiomeAgent>(sp =>
{
    var apiKey = builder.Configuration["LLMProvider:OpenAI:ApiKey"];
    if (string.IsNullOrEmpty(apiKey))
        apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");

    return AgxiomeBuilder.Agent(cfg.Id)
        .UseDeepSeek(apiKey, cfg.Model ?? "deepseek-v4-flash")
        .WithSystemPrompt(cfg.SystemPrompt)
        .WithAutoLoops(upperBound: 1)
        .Build();
});

var app = builder.Build();
await app.UseAgxiomeSecurityDefaultsAsync();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", (KnowledgeBase kb, IVectorStore vs) =>
    Results.Ok(new
    {
        status    = "ok",
        agent     = "MY_AGENT_NAME",
        vectorDb  = vs.ProviderType.ToString(),
        utc       = DateTime.UtcNow
    })
)
    .AllowAnonymousAgxiome();

// Ingest a file or an entire directory
app.MapPost("/ingest", async (
    IngestRequest req,
    KnowledgeBase kb,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Path))
        return Results.BadRequest(new { error = "Path is required." });

    var requestedPath = ResolvePathWithinRoot(ingestRoot, req.Path);
    if (!IsUnderRoot(ingestRoot, requestedPath))
        return Results.BadRequest(new { error = $"Path '{req.Path}' is outside the allowed ingest root." });

    var ingested = new List<string>();

    if (Directory.Exists(requestedPath))
    {
        if (ContainsReparsePoint(requestedPath, ingestRoot))
            return Results.BadRequest(new { error = "Requested path contains a reparse point and cannot be ingested." });

        var allFiles = Directory
            .EnumerateFiles(requestedPath, "*.*", SearchOption.AllDirectories)
            .ToList();

        if (allFiles.Count > MaxIngestFiles)
            return Results.BadRequest(new { error = $"Requested path contains too many files. Max allowed: {MaxIngestFiles}." });

        if (allFiles.Any(f => GetRelativeDepth(requestedPath, f) > MaxIngestDepth))
            return Results.BadRequest(new { error = $"Requested path exceeds the maximum directory depth of {MaxIngestDepth}." });

        var files = allFiles
            .Where(f => AllowedIngestExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
            return Results.BadRequest(new { error = "Requested path does not contain supported files to ingest." });

        foreach (var file in files)
        {
            if (ContainsReparsePoint(file, ingestRoot))
                return Results.BadRequest(new { error = "Requested path contains a reparse point and cannot be ingested." });

            var fileError = ValidateIngestFile(file);
            if (fileError is not null)
                return Results.BadRequest(new { error = fileError });

            await IngestFileAsync(kb, file, ct);
            ingested.Add(Path.GetRelativePath(ingestRoot, file).Replace('\\', '/'));
        }
    }
    else if (File.Exists(requestedPath))
    {
        if (!AllowedIngestExtensions.Contains(Path.GetExtension(requestedPath), StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Requested file type is not supported for ingest." });

        if (ContainsReparsePoint(requestedPath, ingestRoot))
            return Results.BadRequest(new { error = "Requested path contains a reparse point and cannot be ingested." });

        var fileError = ValidateIngestFile(requestedPath);
        if (fileError is not null)
            return Results.BadRequest(new { error = fileError });

        await IngestFileAsync(kb, requestedPath, ct);
        ingested.Add(Path.GetRelativePath(ingestRoot, requestedPath).Replace('\\', '/'));
    }
    else
    {
        return Results.NotFound(new { error = $"Path not found: {req.Path}" });
    }

    return Results.Ok(new { ingestedFiles = ingested.Count, paths = ingested });
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeIngest)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

// Ingest raw text with an explicit ID
app.MapPost("/ingest/text", async (
    IngestTextRequest req,
    KnowledgeBase kb,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Id) || string.IsNullOrWhiteSpace(req.Content))
        return Results.BadRequest(new { error = "Id and Content are required." });

    await kb.IngestAsync(new Document(
        req.Id,
        req.Content,
        DocumentType.PlainText,
        req.Metadata?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? null
    ), ct);

    return Results.Ok(new { id = req.Id, message = "Ingested." });
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeIngest)
    .WithAgxiomeRateLimit(20, TimeSpan.FromMinutes(1));

// RAG query: retrieve chunks → inject as context → LLM answer
app.MapPost("/query", async (
    QueryRequest req,
    KnowledgeBase kb,
    IAgxiomeAgent agent,
    CancellationToken ct) =>
{
    var opts    = new KnowledgeSearchOptions(TopK: req.TopK ?? 5, MinScore: req.MinScore ?? 0.5);
    var chunks  = await kb.SearchAsync(req.Question, opts, ct);

    if (chunks.Count == 0)
    {
        return Results.Ok(new QueryResponse(
            req.Question,
            "I don't have any relevant documents to answer this question. Please ingest some documents first.",
            new List<ChunkReference>(),
            false
        ));
    }

    // Build grounded prompt
    var context = string.Join("\n\n---\n\n", chunks.Select((c, i) =>
        $"[Source {i + 1}: {c.DocumentId}, score={c.Score:F2}]\n{c.Content}"));

    var prompt =
        $"""
        Answer the following question using ONLY the provided context. If the context does not contain enough information, say so.

        Context:
        {context}

        Question: {req.Question}
        """;

    var response = await agent.RunAsync(new AgentRequest(prompt), ct);

    var refs = chunks.Select(c => new ChunkReference(
        c.DocumentId, c.Content[..Math.Min(150, c.Content.Length)], c.Score)).ToList();

    return Results.Ok(new QueryResponse(req.Question, response.Content, refs, true));
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeSearch)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// List all ingested document collections
app.MapGet("/collections", async (IVectorStore vs, CancellationToken ct) =>
{
    var cols = await vs.ListCollectionsAsync(ct);
    return Results.Ok(new { count = cols.Count, collections = cols });
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeSearch)
    .WithAgxiomeRateLimit(30, TimeSpan.FromMinutes(1));

// Delete a specific document from the knowledge base
app.MapDelete("/document/{id}", async (string id, KnowledgeBase kb, CancellationToken ct) =>
{
    await kb.DeleteAsync(id, ct);
    return Results.Ok(new { id, deleted = true });
})
    .RequireAgxiomePermission(AgxiomePermission.KnowledgeDelete)
    .WithAgxiomeRateLimit(10, TimeSpan.FromMinutes(1));

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task IngestFileAsync(KnowledgeBase kb, string path, CancellationToken ct)
{
    var info = new FileInfo(path);
    if (info.Length > MaxIngestFileSizeBytes)
        throw new InvalidOperationException($"File '{Path.GetFileName(path)}' exceeds the maximum allowed ingest size.");

    var ext  = Path.GetExtension(path);
    var docId = Path.GetFileNameWithoutExtension(path);
    var text = await File.ReadAllTextAsync(path, ct);
    var type = ext.ToLower() switch
    {
        ".md"  => DocumentType.Markdown,
        ".json"=> DocumentType.Html,
        ".csv" => DocumentType.Csv,
        _      => DocumentType.PlainText
    };
    await kb.IngestAsync(new Document(docId, text, type, new Dictionary<string, string>
    {
        ["source"]      = path,
        ["ingested_at"] = DateTime.UtcNow.ToString("o")
    }), ct);
}

static string ResolvePathWithinRoot(string root, string requestedPath) =>
    Path.GetFullPath(Path.IsPathRooted(requestedPath)
        ? requestedPath
        : Path.Combine(root, requestedPath));

static bool IsUnderRoot(string root, string fullPath)
{
    var normalizedRoot = EnsureTrailingSeparator(root);
    var normalizedPath = EnsureTrailingSeparator(Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath) ?? fullPath);
    return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
        || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase);
}

static string EnsureTrailingSeparator(string path)
{
    var fullPath = Path.GetFullPath(path);
    return fullPath.EndsWith(Path.DirectorySeparatorChar)
        ? fullPath
        : fullPath + Path.DirectorySeparatorChar;
}

static int GetRelativeDepth(string root, string fullPath)
{
    var relative = Path.GetRelativePath(root, Path.GetDirectoryName(fullPath) ?? fullPath);
    if (relative == ".")
        return 0;

    return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length;
}

static bool ContainsReparsePoint(string fullPath, string stopRoot)
{
    var current = File.Exists(fullPath)
        ? new FileInfo(fullPath).Directory
        : new DirectoryInfo(fullPath);

    while (current is not null)
    {
        if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
            return true;

        if (string.Equals(current.FullName, Path.GetFullPath(stopRoot), StringComparison.OrdinalIgnoreCase))
            break;

        current = current.Parent;
    }

    if (File.Exists(fullPath))
    {
        var fileInfo = new FileInfo(fullPath);
        return (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    return false;
}

static string? ValidateIngestFile(string path)
{
    var info = new FileInfo(path);
    return info.Length > MaxIngestFileSizeBytes
        ? $"File '{Path.GetFileName(path)}' exceeds the maximum allowed ingest size."
        : null;
}

public partial class Program { }
