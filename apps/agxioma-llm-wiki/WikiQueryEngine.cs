using AGXIOME.Storage.Embedders;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Diagnostics;
using System.Text;

namespace AgxiomaLLMWiki;

/// <summary>
/// Semantic query engine for the AGXIOMA wiki.
///
/// Query flow (Karpathy LLMWiki pattern):
///   1. Embed the question with mxbai-embed-large
///   2. Search wiki_embeddings (pgvector cosine similarity)
///   3. Deduplicate results by page slug
///   4. Load full wiki page content for top pages
///   5. LLM synthesises answer with citations ([[PageSlug]])
///   6. Log query to wiki_log + log.md
/// </summary>
public sealed class WikiQueryEngine : IWikiQueryEngine
{
    private readonly WikiSettings            _settings;
    private readonly IIngestProfileProvider  _profileProvider;
    private readonly WikiDatabase            _db;
    private readonly WikiPageWriter          _pageWriter;
    private readonly OllamaMxbaiEmbedder     _embedder;
    private readonly IAgxiomeAgent           _agent;
    private readonly OllamaReranker?         _reranker;
    private readonly ILogger<WikiQueryEngine> _log;

    public WikiQueryEngine(
        WikiSettings settings,
        IIngestProfileProvider profileProvider,
        WikiDatabase db,
        WikiPageWriter pageWriter,
        OllamaMxbaiEmbedder embedder,
        IAgxiomeAgent agent,
        ILogger<WikiQueryEngine> log,
        OllamaReranker? reranker = null)
    {
        _settings        = settings;
        _profileProvider = profileProvider;
        _db              = db;
        _pageWriter      = pageWriter;
        _embedder        = embedder;
        _agent           = agent;
        _reranker        = reranker;
        _log             = log;
    }

    public async Task<WikiQueryResponse> QueryAsync(
        WikiQueryRequest request,
        CancellationToken ct = default)
    {
        var sw       = Stopwatch.StartNew();
        var profile  = await _profileProvider.GetProfileAsync(ct);
        var topK     = request.TopK     ?? profile.TopK;
        var minScore = (float)(request.MinScore ?? profile.MinScore);

        // Step 1: embed the query
        float[] queryVec;
        try
        {
            queryVec = await _embedder.EmbedAsync(request.Question, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Query] Embedding failed: {Error}", ex.Message);
            return NoContext(request.Question, "Embedding service unavailable.", sw.Elapsed.TotalMilliseconds);
        }

        // Step 2: vector search
        var hits = await _db.SearchEmbeddingsAsync(queryVec, topK * 2, minScore, ct);

        if (hits.Count == 0)
        {
            await _pageWriter.AppendToLogMdAsync("query", $"\"{request.Question}\" — no context found", ct);
            await _db.AppendLogAsync("query", $"\"{request.Question}\"", "no_context", ct);
            return NoContext(request.Question, "The wiki does not contain relevant information yet. Try running /ingest first.", sw.Elapsed.TotalMilliseconds);
        }

        // Step 3: deduplicate by page slug, keep best score per slug
        var bySlug = hits
            .GroupBy(h => h.PageSlug)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Score).First());

        var topSlugs = bySlug.Values
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();

        // Step 3b: optional reranking (cross-encoder quality via local LLM)
        bool rerankerUsed = false;
        if ((request.UseReranker ?? false) && _reranker is not null && topSlugs.Count > 2)
        {
            var candidates = topSlugs.Select(h =>
            {
                var text = hits
                    .Where(x => x.PageSlug == h.PageSlug)
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => x.TextContent ?? "")
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Aggregate("", (a, b) => a.Length == 0 ? b : a + "\n\n" + b);
                return new RerankCandidate(h.PageSlug, text, h.Score, 0f);
            }).ToList();

            var reranked = await _reranker.RerankAsync(request.Question, candidates, ct);

            // Re-order topSlugs according to reranker output
            var rerankedSlugs = reranked.Select(r => r.Slug).ToList();
            var slugIndex = topSlugs.ToDictionary(h => h.PageSlug, h => h);
            topSlugs = rerankedSlugs
                .Where(slug => slugIndex.ContainsKey(slug))
                .Select(slug => slugIndex[slug])
                .ToList();
            rerankerUsed = true;
            _log.LogInformation("[Query] Reranker applied. New order: {Slugs}",
                string.Join(", ", rerankedSlugs));
        }

        // Build fallback content from stored chunk text (used when wiki page file is missing,
        // e.g. after a container restart where the wiki/ folder was not volume-mounted).
        var chunksBySlug = hits
            .GroupBy(h => h.PageSlug)
            .ToDictionary(
                g => g.Key,
                g => string.Join("\n\n", g.OrderByDescending(x => x.Score)
                                           .Select(x => x.TextContent)
                                           .Where(t => !string.IsNullOrEmpty(t))));

        // Step 4: load wiki page content for top slugs
        var pageContents = new List<(string Slug, string Content, float Score)>();
        foreach (var hit in topSlugs)
        {
            var cached  = await _db.GetCachedPageContentAsync(hit.PageSlug);
            var content = cached ?? await _pageWriter.ReadWikiPageAsync(hit.PageSlug, ct);

            // Fallback: wiki page file missing (container restart without volume mount)
            if (content is null)
            {
                chunksBySlug.TryGetValue(hit.PageSlug, out var chunks);
                if (!string.IsNullOrEmpty(chunks))
                {
                    content = chunks;
                    _log.LogDebug("[Query] Wiki page {Slug} not on disk — using stored chunk text as fallback", hit.PageSlug);
                }
            }

            if (content is not null)
            {
                pageContents.Add((hit.PageSlug, content, hit.Score));
                if (cached is null)
                    await _db.CachePageContentAsync(hit.PageSlug, content);
            }
        }

        if (pageContents.Count == 0)
        {
            return NoContext(request.Question, "Relevant pages found in index but could not be read. Try re-running /ingest.", sw.Elapsed.TotalMilliseconds);
        }

        // Step 5: LLM synthesis
        var contextBlock = BuildContextBlock(pageContents);
        var prompt = $"""
            You are an expert assistant for the AGXIOMA .NET agent framework wiki.
            Answer the question below using ONLY the wiki pages provided as context.
            Cite pages using [[PageSlug]] notation.
            If the context is insufficient, say so explicitly.

            ## Question
            {request.Question}

            ## Wiki Context
            {contextBlock}

            ## Instructions
            - Answer directly and concisely
            - Cite the most relevant pages with [[slug]] notation
            - If multiple pages are relevant, synthesise across them
            - If the question cannot be answered from the context, say:
              "The wiki does not contain enough information. Consider ingesting more sources."
            """;

        string answer;
        try
        {
            var response = await _agent.RunAsync(new AgentRequest(prompt), ct);
            answer = response.Content.Trim();
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Query] LLM synthesis failed: {Error}", ex.Message);
            answer = $"LLM synthesis error: {ex.Message}. Raw context available from {pageContents.Count} pages.";
        }

        // Step 6: build source refs (load titles from DB)
        var pageTitles = new Dictionary<string, string>();
        foreach (var (slug, _, _) in pageContents)
        {
            var page = await _db.GetPageBySlugAsync(slug, ct);
            pageTitles[slug] = page?.Title ?? slug;
        }

        var sourceRefs = pageContents.Select(p => new WikiPageRef(
            p.Slug,
            pageTitles.GetValueOrDefault(p.Slug, p.Slug),
            p.Content.Length > 200 ? p.Content[..200] + "…" : p.Content,
            p.Score)).ToList();

        await _pageWriter.AppendToLogMdAsync("query",
            $"\"{request.Question}\" — {pageContents.Count} pages retrieved", ct);
        await _db.AppendLogAsync("query",
            $"\"{request.Question}\"",
            $"pages={string.Join(",", topSlugs.Select(h => h.PageSlug))}", ct);

        sw.Stop();
        _log.LogInformation("[Query] \"{Q}\" → {Pages} pages, {Ms}ms",
            request.Question, pageContents.Count, (int)sw.Elapsed.TotalMilliseconds);

        return new WikiQueryResponse(
            Question:     request.Question,
            Answer:       answer,
            Sources:      sourceRefs,
            ContextFound: true,
            LatencyMs:    sw.Elapsed.TotalMilliseconds,
            RerankerUsed: rerankerUsed);
    }

    /// <summary>
    /// Retrieve-only path: embed → vector search → load pages. No LLM call.
    /// Used by multi-concept query strategies that merge results before synthesis.
    /// </summary>
    public async Task<IReadOnlyList<WikiPageChunk>> RetrieveAsync(
        string question,
        int topK = 5,
        float minScore = 0.25f,
        CancellationToken ct = default)
    {
        float[] queryVec;
        try
        {
            queryVec = await _embedder.EmbedAsync(question, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Retrieve] Embedding failed for '{Q}': {E}", question, ex.Message);
            return [];
        }

        var hits = await _db.SearchEmbeddingsAsync(queryVec, topK * 2, minScore, ct);
        if (hits.Count == 0) return [];

        var bySlug = hits
            .GroupBy(h => h.PageSlug)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Score).First());

        var topSlugs = bySlug.Values
            .OrderByDescending(h => h.Score)
            .Take(topK)
            .ToList();

        var results = new List<WikiPageChunk>();
        foreach (var hit in topSlugs)
        {
            var cached  = await _db.GetCachedPageContentAsync(hit.PageSlug);
            var content = cached ?? await _pageWriter.ReadWikiPageAsync(hit.PageSlug, ct);
            if (content is null)
            {
                // Parent-document retrieval: reconstruct the complete article from ALL
                // stored chunks ordered by chunk_index — never just the query-matched subset
                content = await _db.GetAllChunksBySlugAsync(hit.PageSlug, ct);
            }
            if (content is null) continue;
            if (cached is null) await _db.CachePageContentAsync(hit.PageSlug, content);

            var page  = await _db.GetPageBySlugAsync(hit.PageSlug, ct);
            var title = page?.Title ?? hit.PageSlug;
            results.Add(new WikiPageChunk(hit.PageSlug, title, content, hit.Score));
        }

        _log.LogInformation("[Retrieve] '{Q}' → {N} pages", question, results.Count);
        return results;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildContextBlock(List<(string Slug, string Content, float Score)> pages)
    {
        var sb = new StringBuilder();
        foreach (var (slug, content, score) in pages)
        {
            sb.AppendLine($"### [[{slug}]] (relevance: {score:F3})");
            // Limit per-page content to avoid token overflow
            var trimmed = content.Length > 3000 ? content[..3000] + "\n… [truncated]" : content;
            sb.AppendLine(trimmed);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static WikiQueryResponse NoContext(string question, string reason, double ms) =>
        new(
            Question:     question,
            Answer:       reason,
            Sources:      [],
            ContextFound: false,
            LatencyMs:    ms);
}
