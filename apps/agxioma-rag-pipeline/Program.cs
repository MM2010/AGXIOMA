using AGXIOME.Core;
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

var cfg = builder.Configuration.GetSection("Agent").Get<AgentSettings>() ?? new AgentSettings();

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

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapGet("/health", (KnowledgeBase kb, IVectorStore vs) =>
    Results.Ok(new
    {
        status    = "ok",
        agent     = "MY_AGENT_NAME",
        vectorDb  = vs.ProviderType.ToString(),
        utc       = DateTime.UtcNow
    })
);

// Ingest a file or an entire directory
app.MapPost("/ingest", async (
    IngestRequest req,
    KnowledgeBase kb,
    CancellationToken ct) =>
{
    var ingested = new List<string>();

    if (Directory.Exists(req.Path))
    {
        var files = Directory
            .EnumerateFiles(req.Path, "*.*", SearchOption.AllDirectories)
            .Where(f => new[] { ".md", ".txt", ".json", ".csv" }.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase));

        foreach (var file in files)
        {
            await IngestFileAsync(kb, file, ct);
            ingested.Add(file);
        }
    }
    else if (File.Exists(req.Path))
    {
        await IngestFileAsync(kb, req.Path, ct);
        ingested.Add(req.Path);
    }
    else
    {
        return Results.NotFound(new { error = $"Path not found: {req.Path}" });
    }

    return Results.Ok(new { ingestedFiles = ingested.Count, paths = ingested });
});

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
});

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
});

// List all ingested document collections
app.MapGet("/collections", async (IVectorStore vs, CancellationToken ct) =>
{
    var cols = await vs.ListCollectionsAsync(ct);
    return Results.Ok(new { count = cols.Count, collections = cols });
});

// Delete a specific document from the knowledge base
app.MapDelete("/document/{id}", async (string id, KnowledgeBase kb, CancellationToken ct) =>
{
    await kb.DeleteAsync(id, ct);
    return Results.Ok(new { id, deleted = true });
});

app.Run();

// ── Helpers ───────────────────────────────────────────────────────────────────

static async Task IngestFileAsync(KnowledgeBase kb, string path, CancellationToken ct)
{
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
