using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Text;

namespace AgxiomaLLMWiki;

/// <summary>
/// LLM-powered wiki page generator.
/// Follows the Karpathy LLMWiki pattern: reads source files → LLM synthesises
/// → writes structured markdown pages to the wiki/ directory.
///
/// Each page uses the frontmatter + structure defined in WIKI_SCHEMA.md.
/// </summary>
public sealed class WikiPageWriter
{
    private readonly WikiSettings  _settings;
    private readonly WikiDatabase  _db;
    private readonly IAgxiomeAgent _agent;
    private readonly ILogger<WikiPageWriter> _log;

    // Wiki schema excerpt injected into every LLM call
    private static readonly string PageFormatGuide = """
        ## Page Format (MUST follow exactly)

        ```markdown
        ---
        title: <Page Title>
        category: entity|concept|source|overview
        tags: [tag1, tag2]
        source_count: N
        created_at: YYYY-MM-DD
        updated_at: YYYY-MM-DD
        ---

        # <Page Title>

        ## Summary
        One paragraph synthesis of what this is and why it matters.

        ## Key Details
        - Bullet points of important facts, signatures, patterns

        ## Cross-References
        - [[RelatedSlug]] — why it's related

        ## Source Files
        - `path/to/source.cs` (ingested YYYY-MM-DD)
        ```

        Rules:
        - Keep concise. Summary: 1-2 paragraphs. Key Details: 5-15 bullets max.
        - Use [[WikiLink]] for cross-references (Obsidian-compatible)
        - Respond with ONLY the markdown content, no extra explanation
        """;

    public WikiPageWriter(
        WikiSettings settings,
        WikiDatabase db,
        IAgxiomeAgent agent,
        ILogger<WikiPageWriter> log)
    {
        _settings = settings;
        _db       = db;
        _agent    = agent;
        _log      = log;

        // Ensure wiki directory structure exists
        EnsureWikiDirs();
    }

    // ── Page generation ────────────────────────────────────────────────────────

    /// <summary>
    /// Generates or updates a wiki page for a source file.
    /// Returns the page slug.
    /// </summary>
    public async Task<string> GenerateOrUpdatePageAsync(
        string slug,
        string relativePath,
        string fileType,
        string sourceContent,
        bool   isNew,
        CancellationToken ct = default)
    {
        var category = DeriveCategory(slug);
        var title    = DeriveTitle(slug);
        var today    = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Truncate very long sources to avoid token limits
        var truncatedContent = sourceContent.Length > 8000
            ? sourceContent[..8000] + "\n\n... [truncated for brevity]"
            : sourceContent;

        var existingContent = isNew ? null : await ReadWikiPageAsync(slug);

        var prompt = BuildGenerationPrompt(
            slug, relativePath, fileType, category, title,
            truncatedContent, existingContent, today);

        AgentResponse response;
        try
        {
            response = await _agent.RunAsync(new AgentRequest(prompt), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[PageWriter] LLM call failed for {Slug}: {Error}", slug, ex.Message);
            // Write a minimal fallback page
            await WriteMinimalPageAsync(slug, title, category, relativePath, today, ct);
            return slug;
        }

        var pageContent = response.Content.Trim();

        // Strip markdown code fences if LLM wrapped content
        if (pageContent.StartsWith("```markdown"))
            pageContent = pageContent["```markdown".Length..].TrimStart();
        if (pageContent.StartsWith("```"))
            pageContent = pageContent[3..].TrimStart();
        if (pageContent.EndsWith("```"))
            pageContent = pageContent[..^3].TrimEnd();

        // Write to disk
        await WriteWikiPageAsync(slug, pageContent, ct);

        // Extract summary from generated page (first paragraph after ## Summary)
        var summary = ExtractSummary(pageContent, title);

        // Track in database
        await _db.UpsertPageAsync(new WikiPage
        {
            Slug        = slug,
            Title       = title,
            Category    = category,
            Summary     = summary,
            FilePath    = GetWikiFilePath(slug),
            SourceCount = 1
        }, ct);

        _log.LogDebug("[PageWriter] {Action} page: {Slug}", isNew ? "Created" : "Updated", slug);
        return slug;
    }

    // ── Overview page ─────────────────────────────────────────────────────────

    /// <summary>Generates or refreshes the project overview page.</summary>
    public async Task GenerateOverviewAsync(CancellationToken ct = default)
    {
        var pages   = await _db.ListPagesAsync(ct: ct);
        var sources = pages.Count;
        var today   = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var entitySummaries = pages
            .Where(p => p.Category == "entity")
            .Take(20)
            .Select(p => $"- **{p.Title}**: {p.Summary.Split('\n')[0]}")
            .ToList();

        var conceptSummaries = pages
            .Where(p => p.Category == "concept")
            .Take(15)
            .Select(p => $"- **{p.Title}**: {p.Summary.Split('\n')[0]}")
            .ToList();

        var prompt = $"""
            You are writing the project overview page for the AGXIOMA knowledge wiki.
            AGXIOMA is a .NET 10 agent framework for building LLM-powered multi-agent systems.

            Based on these wiki pages already created ({sources} total), write a comprehensive
            overview page for the AGXIOMA project.

            Key entities known:
            {string.Join('\n', entitySummaries)}

            Key concepts known:
            {string.Join('\n', conceptSummaries)}

            {PageFormatGuide}

            The page slug is "overview", category is "overview".
            Today is {today}. Make source_count = {sources}.
            """;

        try
        {
            var response = await _agent.RunAsync(new AgentRequest(prompt), ct);
            await WriteWikiPageAsync("overview", response.Content.Trim(), ct);
            await _db.UpsertPageAsync(new WikiPage
            {
                Slug        = "overview",
                Title       = "AGXIOMA Project Overview",
                Category    = "overview",
                Summary     = "High-level overview of the AGXIOMA .NET agent framework.",
                FilePath    = GetWikiFilePath("overview"),
                SourceCount = sources
            }, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[PageWriter] Overview generation failed: {Error}", ex.Message);
        }
    }

    // ── index.md regeneration ─────────────────────────────────────────────────

    /// <summary>
    /// Regenerates index.md — the content catalog of all wiki pages.
    /// Called after every ingest batch.
    /// </summary>
    public async Task RegenerateIndexAsync(CancellationToken ct = default)
    {
        var pages = await _db.ListPagesAsync(ct: ct);
        var sb    = new StringBuilder();

        sb.AppendLine("# AGXIOMA Wiki Index");
        sb.AppendLine();
        sb.AppendLine($"_Last updated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. Total pages: {pages.Count}._");
        sb.AppendLine();

        // Overview
        var overviewPages = pages.Where(p => p.Category == "overview").ToList();
        if (overviewPages.Count > 0)
        {
            sb.AppendLine("## Overview");
            foreach (var p in overviewPages)
                sb.AppendLine($"- [[{p.Slug}]] — {FirstLine(p.Summary)}");
            sb.AppendLine();
        }

        // Entities
        var entityPages = pages.Where(p => p.Category == "entity").OrderBy(p => p.Title).ToList();
        if (entityPages.Count > 0)
        {
            sb.AppendLine($"## Entities ({entityPages.Count})");
            foreach (var p in entityPages)
                sb.AppendLine($"- [[{p.Slug}]] — {FirstLine(p.Summary)}");
            sb.AppendLine();
        }

        // Concepts
        var conceptPages = pages.Where(p => p.Category == "concept").OrderBy(p => p.Title).ToList();
        if (conceptPages.Count > 0)
        {
            sb.AppendLine($"## Concepts ({conceptPages.Count})");
            foreach (var p in conceptPages)
                sb.AppendLine($"- [[{p.Slug}]] — {FirstLine(p.Summary)}");
            sb.AppendLine();
        }

        // Sources
        var sourcePages = pages.Where(p => p.Category == "source").OrderBy(p => p.Title).ToList();
        if (sourcePages.Count > 0)
        {
            sb.AppendLine($"## Sources ({sourcePages.Count})");
            foreach (var p in sourcePages)
                sb.AppendLine($"- [[{p.Slug}]] — {FirstLine(p.Summary)}");
            sb.AppendLine();
        }

        var indexPath = Path.Combine(_settings.WikiOutputPath, "index.md");
        await File.WriteAllTextAsync(indexPath, sb.ToString(), ct);
        _log.LogInformation("[PageWriter] index.md regenerated ({Count} pages)", pages.Count);
    }

    // ── log.md ────────────────────────────────────────────────────────────────

    /// <summary>Appends an entry to log.md (append-only chronological log).</summary>
    public async Task AppendToLogMdAsync(string opType, string title, CancellationToken ct = default)
    {
        var logPath = Path.Combine(_settings.WikiOutputPath, "log.md");
        var entry   = $"\n## [{DateTime.UtcNow:yyyy-MM-dd HH:mm}] {opType} | {title}\n";

        if (!File.Exists(logPath))
            await File.WriteAllTextAsync(logPath, "# AGXIOMA Wiki Log\n\n_Append-only chronological log of wiki operations._\n", ct);

        await File.AppendAllTextAsync(logPath, entry, ct);
    }

    // ── Read helpers ──────────────────────────────────────────────────────────

    public async Task<string?> ReadWikiPageAsync(string slug, CancellationToken ct = default)
    {
        var filePath = GetWikiFilePath(slug);
        if (!File.Exists(filePath)) return null;
        return await File.ReadAllTextAsync(filePath, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private string BuildGenerationPrompt(
        string slug, string relativePath, string fileType, string category,
        string title, string sourceContent, string? existingContent, string today)
    {
        var sb = new StringBuilder();

        if (existingContent is not null)
        {
            sb.AppendLine("You are updating an existing wiki page with new source content.");
            sb.AppendLine();
            sb.AppendLine("## Existing wiki page:");
            sb.AppendLine(existingContent);
            sb.AppendLine();
            sb.AppendLine("## Updated source content:");
        }
        else
        {
            sb.AppendLine($"You are creating a new wiki page for: **{title}**");
            sb.AppendLine();
            sb.AppendLine("## Source content:");
        }

        sb.AppendLine($"```{fileType}");
        sb.AppendLine(sourceContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"**Page slug**: `{slug}`");
        sb.AppendLine($"**Category**: `{category}` (entity|concept|source|overview)");
        sb.AppendLine($"**Source file**: `{relativePath}`");
        sb.AppendLine($"**Today**: {today}");
        sb.AppendLine();
        sb.AppendLine(PageFormatGuide);

        return sb.ToString();
    }

    private async Task WriteMinimalPageAsync(
        string slug, string title, string category,
        string relativePath, string today, CancellationToken ct)
    {
        var content = $"""
            ---
            title: {title}
            category: {category}
            tags: []
            source_count: 1
            created_at: {today}
            updated_at: {today}
            ---

            # {title}

            ## Summary
            Source file: `{relativePath}` — wiki page generation pending.

            ## Key Details
            - File: `{relativePath}`

            ## Source Files
            - `{relativePath}` (ingested {today})
            """;

        await WriteWikiPageAsync(slug, content, ct);
        await _db.UpsertPageAsync(new WikiPage
        {
            Slug        = slug,
            Title       = title,
            Category    = category,
            Summary     = $"Source: {relativePath}",
            FilePath    = GetWikiFilePath(slug),
            SourceCount = 1
        }, ct);
    }

    private async Task WriteWikiPageAsync(string slug, string content, CancellationToken ct)
    {
        var filePath = GetWikiFilePath(slug);
        var dir      = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(filePath, content, ct);
    }

    private string GetWikiFilePath(string slug) =>
        Path.Combine(_settings.WikiOutputPath, slug.Replace('/', Path.DirectorySeparatorChar) + ".md");

    private static string DeriveCategory(string slug)
    {
        if (slug.StartsWith("entities/"))  return "entity";
        if (slug.StartsWith("concepts/"))  return "concept";
        if (slug == "overview")            return "overview";
        return "source";
    }

    private static string DeriveTitle(string slug)
    {
        var name = slug.Contains('/') ? slug[(slug.LastIndexOf('/') + 1)..] : slug;
        // Convert kebab-case or PascalCase to readable title
        var spaced = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        return spaced.Replace('-', ' ').Replace('_', ' ')
                     .Split(' ')
                     .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w)
                     .Aggregate((a, b) => a + " " + b);
    }

    private static string ExtractSummary(string pageContent, string title)
    {
        var lines    = pageContent.Split('\n');
        var inSummary = false;
        var summary  = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("## Summary"))
            {
                inSummary = true;
                continue;
            }
            if (inSummary)
            {
                if (line.TrimStart().StartsWith("## ")) break;
                if (!string.IsNullOrWhiteSpace(line))
                    summary.AppendLine(line.Trim());
            }
        }

        var result = summary.ToString().Trim();
        return result.Length > 0 ? result[..Math.Min(500, result.Length)] : title;
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? text;

    private void EnsureWikiDirs()
    {
        Directory.CreateDirectory(_settings.WikiOutputPath);
        Directory.CreateDirectory(Path.Combine(_settings.WikiOutputPath, "entities"));
        Directory.CreateDirectory(Path.Combine(_settings.WikiOutputPath, "concepts"));
        Directory.CreateDirectory(Path.Combine(_settings.WikiOutputPath, "sources"));
    }
}
