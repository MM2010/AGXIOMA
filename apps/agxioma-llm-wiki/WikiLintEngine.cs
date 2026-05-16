using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace AgxiomaLLMWiki;

/// <summary>
/// Wiki health and quality checker.
///
/// Lint checks:
///   1. Orphan pages     — pages in DB but not referenced in index.md
///   2. Stale pages      — wiki page updated_at < source file updated_at
///   3. Broken links     — [[WikiLink]] in pages pointing to non-existent slugs
///   4. Empty pages      — pages with no summary or content < 100 chars
///   5. LLM consistency  — uses DeepSeek to detect potential contradictions
/// </summary>
public sealed class WikiLintEngine : IWikiLintEngine
{
    private readonly WikiSettings     _settings;
    private readonly WikiDatabase     _db;
    private readonly WikiPageWriter   _pageWriter;
    private readonly IAgxiomeAgent    _agent;
    private readonly ILogger<WikiLintEngine> _log;

    public WikiLintEngine(
        WikiSettings settings,
        WikiDatabase db,
        WikiPageWriter pageWriter,
        IAgxiomeAgent agent,
        ILogger<WikiLintEngine> log)
    {
        _settings   = settings;
        _db         = db;
        _pageWriter = pageWriter;
        _agent      = agent;
        _log        = log;
    }

    public async Task<LintReport> RunLintAsync(CancellationToken ct = default)
    {
        var issues = new List<LintIssue>();

        // Load all wiki pages from DB
        var pages = await _db.ListPagesAsync(ct: ct);
        var slugSet = pages.Select(p => p.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _log.LogInformation("[Lint] Starting lint on {Count} pages…", pages.Count);

        await Check1_OrphanPages(pages, slugSet, issues, ct);
        await Check2_StalePages(pages, issues, ct);
        await Check3_BrokenLinks(pages, slugSet, issues, ct);
        await Check4_EmptyPages(pages, issues, ct);
        await Check5_LLMConsistency(pages, issues, ct);

        var report = new LintReport(
            OrphanPages:  issues.Count(i => i.Type == "orphan"),
            MissingLinks: issues.Count(i => i.Type == "broken_link"),
            StalePages:   issues.Count(i => i.Type == "stale"),
            Issues:       issues,
            Summary:      $"{issues.Count} issue(s): {issues.Count(i => i.Type == "orphan")} orphan, {issues.Count(i => i.Type == "broken_link")} broken links, {issues.Count(i => i.Type == "stale")} stale",
            RunAt:        DateTime.UtcNow);

        // Log to DB + log.md
        var summary = $"Lint complete — {issues.Count} issue(s) across {pages.Count} pages";
        await _pageWriter.AppendToLogMdAsync("lint", summary, ct);
        await _db.AppendLogAsync("lint", summary,
            $"orphan={issues.Count(i => i.Type == "orphan")},broken={issues.Count(i => i.Type == "broken_link")},stale={issues.Count(i => i.Type == "stale")}", ct);

        _log.LogInformation("[Lint] Done: {Issues} issues", issues.Count);
        return report;
    }

    // ── Check 1: Orphan pages ─────────────────────────────────────────────────

    private async Task Check1_OrphanPages(
        List<WikiPage> pages,
        HashSet<string> slugSet,
        List<LintIssue> issues,
        CancellationToken ct)
    {
        var indexPath = Path.Combine(_settings.WikiOutputPath, "index.md");
        if (!File.Exists(indexPath))
        {
            issues.Add(new LintIssue("orphan", "index.md", "index.md does not exist. Run /ingest to generate it."));
            return;
        }

        var indexContent = await File.ReadAllTextAsync(indexPath, ct);
        foreach (var page in pages)
        {
            if (page.Slug == "overview") continue; // skip special pages
            if (!indexContent.Contains($"[[{page.Slug}]]", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new LintIssue(
                    "orphan",
                    page.Slug,
                    $"Page '{page.Slug}' exists in DB but is not listed in index.md"));
            }
        }
    }

    // ── Check 2: Stale pages ──────────────────────────────────────────────────

    private async Task Check2_StalePages(
        List<WikiPage> pages,
        List<LintIssue> issues,
        CancellationToken ct)
    {
        foreach (var page in pages)
        {
            if (string.IsNullOrEmpty(page.FilePath)) continue;

            // Find source file for this page (derive from slug → relative path)
            var sourceRelative = DeriveSourcePath(page.Slug);
            if (sourceRelative is null) continue;

            var sourcePath = Path.Combine(_settings.ProjectRootPath, sourceRelative);
            if (!File.Exists(sourcePath)) continue;

            var sourceLastWritten = File.GetLastWriteTimeUtc(sourcePath);
            if (sourceLastWritten > page.UpdatedAt.AddMinutes(5))
            {
                issues.Add(new LintIssue(
                    "stale",
                    page.Slug,
                    $"Page '{page.Slug}' may be stale. Source updated {sourceLastWritten:yyyy-MM-dd HH:mm}, page updated {page.UpdatedAt:yyyy-MM-dd HH:mm}."));
            }
        }
        await Task.CompletedTask;
    }

    // ── Check 3: Broken wiki links ────────────────────────────────────────────

    private static readonly Regex WikiLinkPattern = new(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled);

    private async Task Check3_BrokenLinks(
        List<WikiPage> pages,
        HashSet<string> slugSet,
        List<LintIssue> issues,
        CancellationToken ct)
    {
        foreach (var page in pages)
        {
            var content = await _pageWriter.ReadWikiPageAsync(page.Slug, ct);
            if (content is null) continue;

            foreach (Match match in WikiLinkPattern.Matches(content))
            {
                var linked = match.Groups[1].Value.Trim().ToLowerInvariant();
                if (!slugSet.Contains(linked))
                {
                    issues.Add(new LintIssue(
                        "broken_link",
                        page.Slug,
                        $"Page '{page.Slug}' links to [[{linked}]] which does not exist."));
                }
            }
        }
    }

    // ── Check 4: Empty pages ──────────────────────────────────────────────────

    private async Task Check4_EmptyPages(
        List<WikiPage> pages,
        List<LintIssue> issues,
        CancellationToken ct)
    {
        foreach (var page in pages)
        {
            var content = await _pageWriter.ReadWikiPageAsync(page.Slug, ct);
            if (content is null)
            {
                issues.Add(new LintIssue(
                    "missing_file",
                    page.Slug,
                    $"Page '{page.Slug}' is in DB but the .md file is missing."));
                continue;
            }
            if (content.Length < 100)
            {
                issues.Add(new LintIssue(
                    "empty_page",
                    page.Slug,
                    $"Page '{page.Slug}' has very little content ({content.Length} chars)."));
            }
        }
    }

    // ── Check 5: LLM consistency ──────────────────────────────────────────────

    private async Task Check5_LLMConsistency(
        List<WikiPage> pages,
        List<LintIssue> issues,
        CancellationToken ct)
    {
        // Only run if we have enough pages and it's not too expensive
        if (pages.Count < 3)
        {
            _log.LogDebug("[Lint] Skipping LLM consistency check (< 3 pages)");
            return;
        }

        // Take a sample of pages for cost-effective consistency check
        var sample = pages
            .OrderByDescending(p => p.UpdatedAt)
            .Take(Math.Min(pages.Count, 10))
            .ToList();

        var sb = new StringBuilder();
        foreach (var page in sample)
        {
            var content = await _pageWriter.ReadWikiPageAsync(page.Slug, ct);
            if (content is null) continue;
            // Short excerpt per page
            var excerpt = content.Length > 500 ? content[..500] + "…" : content;
            sb.AppendLine($"### [[{page.Slug}]]");
            sb.AppendLine(excerpt);
            sb.AppendLine();
        }

        var prompt = $"""
            You are a wiki quality reviewer for the AGXIOMA .NET agent framework documentation.
            Review the following wiki page excerpts and identify any contradictions,
            inconsistencies, or errors across pages.

            ## Wiki Pages (sample of {sample.Count}):
            {sb}

            ## Task
            List ONLY real contradictions or factual inconsistencies you detect.
            Format each issue as:
            - [SLUG1 vs SLUG2]: Description of contradiction

            If there are no issues, respond with exactly: "No contradictions detected."
            Keep your response brief and actionable.
            """;

        try
        {
            var response = await _agent.RunAsync(new AgentRequest(prompt), ct);
            var result = response.Content.Trim();

            if (!result.StartsWith("No contradictions", StringComparison.OrdinalIgnoreCase)
                && result.Length > 10)
            {
                // Parse lines that start with "- ["
                foreach (var line in result.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("- [") && trimmed.Contains("]:"))
                    {
                        var colon  = trimmed.IndexOf("]:");
                        var slugPart = trimmed[3..colon];
                        var desc     = trimmed[(colon + 2)..].Trim();
                        issues.Add(new LintIssue(
                            "contradiction",
                            slugPart,
                            desc));
                    }
                }

                if (!issues.Any(i => i.Type == "contradiction"))
                {
                    // LLM found something but format was unexpected — add raw result
                    issues.Add(new LintIssue(
                        "contradiction",
                        "multiple",
                        result[..Math.Min(500, result.Length)]));
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Lint] LLM consistency check failed: {Error}", ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? DeriveSourcePath(string slug)
    {
        // Reverse the slug → source path mapping from WikiIngester.DerivePageSlug
        // entities/agxiome-agent → src/AGXIOME.Core/AgxiomeAgent.cs (best effort)
        // This is a heuristic since exact mapping isn't stored in the DB yet
        if (slug.StartsWith("entities/") || slug.StartsWith("concepts/"))
        {
            // Can't reliably reverse — skip
            return null;
        }
        if (slug.StartsWith("sources/"))
        {
            var relative = slug["sources/".Length..].Replace('-', '/').Replace(".md", "");
            return relative;
        }
        return null;
    }
}
