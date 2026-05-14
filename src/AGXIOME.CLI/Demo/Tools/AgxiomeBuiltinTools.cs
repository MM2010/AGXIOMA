// AgxiomeBuiltinTools — Real executable tools for dynamic agent instantiation.
//
// Tools implemented:
//   http_fetch       — HTTP GET a URL, returns plain text (HTML stripped)
//   web_search       — DuckDuckGo HTML search, returns top-N result titles + URLs
//   file_save        — Saves text content to a local file, returns absolute path
//
// All tools implement IAgxiomeExecutableTool so they:
//   1. Expose a ToJsonSchema() used by MapRequest to pass schemas to the LLM
//   2. Receive ToolContext.Parameters populated by the LLM's JSON arguments
//   3. Return ToolResult.Data (string) that gets fed back as tool_result message

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Web;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.CLI.Demo.Tools;

// ══════════════════════════════════════════════════════════════════════════════
// BASE HELPER
// ══════════════════════════════════════════════════════════════════════════════

public abstract class AgxiomeTool : IAgxiomeExecutableTool
{
    public abstract string Name        { get; }
    public abstract string Description { get; }
    public string? Version             => "1.0";
    public IReadOnlyList<string> Categories { get; init; } = [];

    public abstract Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct);

    /// <summary>Build OpenAI function-calling schema from abstract definition.</summary>
    public abstract JsonObject ToJsonSchema();

    protected string? Param(ToolContext ctx, string key)
    {
        if (ctx.Parameters is null) return null;
        ctx.Parameters.TryGetValue(key, out var v);
        return v?.ToString();
    }

    protected static JsonObject BuildSchema(string name, string description,
        params (string name, string type, string desc, bool required)[] props)
    {
        var properties = new JsonObject();
        var required   = new JsonArray();
        foreach (var (pName, pType, pDesc, pReq) in props)
        {
            properties[pName] = new JsonObject { ["type"] = pType, ["description"] = pDesc };
            if (pReq) required.Add(pName);
        }
        return new JsonObject
        {
            ["name"]        = name,
            ["description"] = description,
            ["parameters"]  = new JsonObject
            {
                ["type"]       = "object",
                ["properties"] = properties,
                ["required"]   = required
            }
        };
    }

    /// <summary>Strip all HTML markup from a string — same logic as WebScraperOrchestrationDemo.</summary>
    protected static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = Regex.Replace(html,  @"<(script|style)[^>]*>[\s\S]*?<\/\1>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text,      @"<!--[\s\S]*?-->", " ");
        text = Regex.Replace(text,      @"<(br|p|div|li|tr|h[1-6]|blockquote|pre|section|article)[^>]*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text,      @"<[^>]+>", " ");
        text = HttpUtility.HtmlDecode(text);
        text = Regex.Replace(text,      @"[ \t]{2,}", " ");
        text = Regex.Replace(text,      @"\n{3,}", "\n\n");
        return text.Trim();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 1: http_fetch
// Fetches a URL and returns plain text (HTML stripped).
// LLM arguments: { "url": "https://...", "max_chars": 3000 }
// ══════════════════════════════════════════════════════════════════════════════

public sealed class HttpFetchTool : AgxiomeTool
{
    public override string Name        => "http_fetch";
    public override string Description =>
        "Fetch a web page by URL and return its content as plain text (HTML removed). " +
        "Use this to retrieve live data from any public URL.";

    public override JsonObject ToJsonSchema() => BuildSchema(Name, Description,
        ("url",       "string",  "The full URL to fetch (must start with https:// or http://)", true),
        ("max_chars", "integer", "Maximum characters to return (default 3000, max 8000)",       false));

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var url      = Param(ctx, "url");
        var maxChars = int.TryParse(Param(ctx, "max_chars"), out var mc) ? Math.Min(mc, 8000) : 3000;

        if (string.IsNullOrWhiteSpace(url) || (!url.StartsWith("http://") && !url.StartsWith("https://")))
            return new ToolResult(false, null, "Invalid or missing URL. Must start with http:// or https://");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var http = BuildHttpClient();
            using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var html   = await resp.Content.ReadAsStringAsync(ct);
            var plain  = StripHtml(html);
            var result = plain.Length > maxChars ? plain[..maxChars] + $"\n[…truncated at {maxChars} chars]" : plain;

            sw.Stop();
            return new ToolResult(true, result,
                Metadata: new ToolMetadata(Name, Version, BytesProcessed: html.Length));
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ToolResult(false, null, $"HTTP fetch failed: {ex.Message}");
        }
    }

    private static HttpClient BuildHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 8 };
        var http    = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.Add("Accept-Language", "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7");
        // Bypass Google's cookie-consent wall (consent.google.com redirect).
        // CONSENT=YES+cb tells Google the user accepted; SAPERRJ=1 suppresses the
        // consent interstitial for Google-hosted domains. Both are widely documented
        // workarounds for automated scraping.
        http.DefaultRequestHeaders.Add("Cookie",
            "CONSENT=YES+cb.20240101-07-p0.it+FX+667; SAPERRJ=1; NID=dummy");
        return http;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 2: web_search
// Searches DuckDuckGo HTML endpoint and returns top-N results (title + URL + snippet).
// LLM arguments: { "query": "...", "num_results": 5 }
// ══════════════════════════════════════════════════════════════════════════════

public sealed class WebSearchTool : AgxiomeTool
{
    public override string Name        => "web_search";
    public override string Description =>
        "Search the web for a query and return the top results with title, URL, and snippet. " +
        "Use this to find current information on any topic.";

    public override JsonObject ToJsonSchema() => BuildSchema(Name, Description,
        ("query",       "string",  "The search query",                              true),
        ("num_results", "integer", "Number of results to return (default 5, max 10)", false));

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var query  = Param(ctx, "query");
        var numRes = int.TryParse(Param(ctx, "num_results"), out var n) ? Math.Clamp(n, 1, 10) : 5;

        if (string.IsNullOrWhiteSpace(query))
            return new ToolResult(false, null, "Missing 'query' parameter.");

        // Try each engine in order until one returns results.
        // DDG frequently returns CAPTCHA/bot-walls; Yahoo and Bing are more permissive.
        var engines = new[]
        {
            ("DuckDuckGo", $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}",   "duckduckgo.com"),
            ("Yahoo",      $"https://search.yahoo.com/search?p={Uri.EscapeDataString(query)}&n=10", "yahoo.com"),
            ("Bing",       $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}&count=10", "bing.com"),
        };

        var errors = new List<string>();
        foreach (var (engineName, url, selfHost) in engines)
        {
            try
            {
                using var http = BuildSearchClient();
                var html    = await http.GetStringAsync(url, ct);
                var results = ParseSearchResults(html, selfHost, numRes);

                if (results.Count == 0)
                {
                    // Dump HTML for diagnostics and try next engine
                    var dumpPath = Path.Combine(Path.GetTempPath(),
                        $"search_debug_{engineName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html");
                    try { await File.WriteAllTextAsync(dumpPath, html[..Math.Min(html.Length, 8000)], ct); } catch { /* best-effort */ }
                    errors.Add($"{engineName}: 0 results (bot-wall? dump: {dumpPath})");
                    continue;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Search results for: \"{query}\" (via {engineName})");
                sb.AppendLine();
                int rank = 1;
                foreach (var (title, link, snippet) in results)
                {
                    sb.AppendLine($"[{rank++}] {title}");
                    if (!string.IsNullOrEmpty(link))    sb.AppendLine($"    URL: {link}");
                    if (!string.IsNullOrEmpty(snippet)) sb.AppendLine($"    {snippet}");
                    sb.AppendLine();
                }

                return new ToolResult(true, sb.ToString(),
                    Metadata: new ToolMetadata(Name, Version, ItemsProcessed: results.Count));
            }
            catch (Exception ex)
            {
                errors.Add($"{engineName}: {ex.Message}");
            }
        }

        return new ToolResult(false, null,
            $"All search engines failed for '{query}'.\n" + string.Join("\n", errors));
    }

    /// <summary>
    /// Multi-strategy HTML result parser. Works for DDG classic, Yahoo, and Bing layouts.
    /// Falls back to generic external-link extraction when engine-specific patterns fail.
    /// </summary>
    private static List<(string Title, string Url, string Snippet)> ParseSearchResults(
        string html, string selfHost, int max)
    {
        var results = new List<(string, string, string)>();
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Strategy 1: DDG classic layout ────────────────────────────────────
        var ddgTitle   = new Regex(@"class=[""']result__a[""'][^>]*>(.*?)</a>",  RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var ddgUrl     = new Regex(@"class=[""']result__url[""'][^>]*>\s*(.*?)\s*</", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var ddgSnippet = new Regex(@"class=[""']result__snippet[""'][^>]*>(.*?)</",   RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var ddgTitles   = ddgTitle.Matches(html).Select(m => StripHtml(m.Groups[1].Value).Trim()).Where(s => s.Length > 2).ToList();
        var ddgUrls     = ddgUrl.Matches(html).Select(m => StripHtml(m.Groups[1].Value).Trim()).ToList();
        var ddgSnippets = ddgSnippet.Matches(html).Select(m => StripHtml(m.Groups[1].Value).Trim()).ToList();
        if (ddgTitles.Count > 0)
        {
            for (int i = 0; i < Math.Min(ddgTitles.Count, max); i++)
                results.Add((ddgTitles[i], i < ddgUrls.Count ? ddgUrls[i] : "", i < ddgSnippets.Count ? ddgSnippets[i] : ""));
            return results;
        }

        // ── Strategy 2: Yahoo layout (li.first / <h3 class="title">) ──────────
        var yahooTitle = new Regex(@"<h3[^>]*class=[""'][^""']*title[^""']*[""'][^>]*>\s*<a[^>]*href=[""']([^""']+)[""'][^>]*>(.*?)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var yahooSnip  = new Regex(@"<p[^>]*class=[""'][^""']*yxt-Summary[^""']*[""'][^>]*>(.*?)</p>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var yahooMatches  = yahooTitle.Matches(html).ToList();
        var yahooSnippets = yahooSnip.Matches(html).Select(m => StripHtml(m.Groups[1].Value).Trim()).ToList();
        if (yahooMatches.Count > 0)
        {
            for (int i = 0; i < Math.Min(yahooMatches.Count, max); i++)
            {
                var rawUrl = Uri.UnescapeDataString(yahooMatches[i].Groups[1].Value.Trim());
                // Yahoo wraps real URLs in a redirect — extract the real URL from /RU= parameter
                var ruMatch = Regex.Match(rawUrl, @"/RU=([^/]+)/");
                if (ruMatch.Success) rawUrl = Uri.UnescapeDataString(ruMatch.Groups[1].Value);
                var title = StripHtml(yahooMatches[i].Groups[2].Value).Trim();
                var snip  = i < yahooSnippets.Count ? yahooSnippets[i] : "";
                if (seen.Add(rawUrl)) results.Add((title, rawUrl, snip));
            }
            if (results.Count > 0) return results;
        }

        // ── Strategy 3: Generic external-link extraction (Bing + fallback) ────
        var genericLink = new Regex(
            @"href=[""'](https?://(?!" + Regex.Escape(selfHost) + @")[^""']{8,})[""'][^>]*>\s*([^<\n\r]{5,120})\s*</[aA]>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match m in genericLink.Matches(html))
        {
            if (results.Count >= max) break;
            var rawUrl = Uri.UnescapeDataString(m.Groups[1].Value.Trim());
            var title  = StripHtml(m.Groups[2].Value).Trim();
            if (title.Length < 5 || rawUrl.Length < 10) continue;
            if (!seen.Add(rawUrl)) continue;
            results.Add((title, rawUrl, ""));
        }

        return results;
    }

    private static HttpClient BuildSearchClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,*/*;q=0.8");
        return http;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL 3: file_save
// Saves text content to a local file and returns the absolute path.
// LLM arguments: { "filename": "report.txt", "content": "..." }
// ══════════════════════════════════════════════════════════════════════════════

public sealed class FileSaveTool : AgxiomeTool
{
    // All AGXIOME output files land in c:/temp/agxioma/ with .agmx extension
    private static readonly string DefaultBaseDir = Path.Combine("c:", "temp", "agxioma");
    private readonly string _baseDir;

    /// <param name="baseDir">Override base directory. Defaults to c:/temp/agxioma.</param>
    public FileSaveTool(string? baseDir = null)
    {
        _baseDir = baseDir ?? DefaultBaseDir;
        Directory.CreateDirectory(_baseDir);
    }

    public override string Name        => "file_save";
    public override string Description =>
        "Save text content to a local .agmx file in c:/temp/agxioma/. " +
        "Returns the absolute path of the saved file. " +
        "Filename format: <descriptive-name>-<guid>-<yyyyMMddHHmmss>.agmx  " +
        "(the .agmx extension is enforced automatically). " +
        "Use this to persist research results, reports, or extracted data.";

    public override JsonObject ToJsonSchema() => BuildSchema(Name, Description,
        ("filename", "string",
            "File name without path separators. The .agmx extension will be applied automatically. " +
            "Example: news_links-550e8400-20260513120000", true),
        ("content",  "string", "Text content to save to the file.", true));

    public override async Task<ToolResult> ExecuteAsync(ToolContext ctx, CancellationToken ct)
    {
        var filename = Param(ctx, "filename");
        var content  = Param(ctx, "content");

        if (string.IsNullOrWhiteSpace(filename))
            return new ToolResult(false, null, "Missing 'filename' parameter.");
        if (content is null)
            return new ToolResult(false, null, "Missing 'content' parameter.");

        // Security: strip any directory component to prevent path traversal
        filename = Path.GetFileName(filename);
        if (string.IsNullOrWhiteSpace(filename))
            return new ToolResult(false, null, "Invalid filename.");

        // Sanitise to safe chars only (allow word chars, dash, dot, space)
        filename = Regex.Replace(filename, @"[^\w\-. ]", "_");

        // Enforce .agmx extension — strip any existing extension then append .agmx
        filename = Path.GetFileNameWithoutExtension(filename) + ".agmx";

        var outPath = Path.Combine(_baseDir, filename);
        try
        {
            await File.WriteAllTextAsync(outPath, content, Encoding.UTF8, ct);
            return new ToolResult(true, $"File saved: {outPath}",
                Metadata: new ToolMetadata(Name, Version, BytesProcessed: Encoding.UTF8.GetByteCount(content)));
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, $"File save failed: {ex.Message}");
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// TOOL CATALOGUE — Lookup by agent role
// ══════════════════════════════════════════════════════════════════════════════

public static class AgxiomeToolCatalogue
{
    /// <summary>
    /// Returns the set of tools appropriate for a given agent role.
    /// The orchestrator (planner/synthesiser) calls this at runtime
    /// to equip each dynamically-spawned worker with the right capabilities.
    /// </summary>
    public static IReadOnlyList<IAgxiomeTool> ForRole(string role, string? outputDir = null)
    {
        var fetch  = new HttpFetchTool();
        var search = new WebSearchTool();
        var save   = new FileSaveTool(outputDir);

        return role.ToLowerInvariant() switch
        {
            "researcher" or "data_fetcher" or "analyst"
                => [search, fetch, save],
            "writer" or "reporter"
                => [save],
            "scraper" or "crawler"
                => [fetch, save],
            "searcher"
                => [search],
            _ // generalist gets all tools
                => [search, fetch, save]
        };
    }
}
