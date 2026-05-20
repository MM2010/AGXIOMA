// WebScraperOrchestrationDemo — Multi-agent web scraping with DeepSeek orchestrator.
//
// Architecture:
//   4 WebWorkerAgent instances (one per engine: Google, DuckDuckGo, Bing, Yahoo)
//     → each scrapes top-10 results, strips HTML, saves to local file
//     → reports completion via System.Threading.Channels.Channel<WorkerReport>
//   Orchestrator (DeepSeek-backed AgxiomeAgent)
//     → reads all worker reports
//     → validates saved files (no HTML tags, meaningful content)
//     → prints final audit summary
//
// Usage:
//   agxiome demo scraper [--query "..."] [--out ./output]
//
// Requires env var: DEEPSEEK_API_KEY
//
// NOTE: Search engines apply bot-protection.
//   Google/Bing/Yahoo may return partial or blocked responses — the demo
//   handles this gracefully and the orchestrator flags degraded results.
//   DuckDuckGo HTML endpoint is the most scraper-friendly for local dev use.
//   Respect robots.txt and rate limits in production scenarios.

using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Web;
using AGXIOME.Core.Agent;
using AGXIOME.LlmWiky;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.CLI.Demo;

public static class WebScraperOrchestrationDemo
{
    // ── Engines ────────────────────────────────────────────────────────────────
    private static readonly string[] Engines = ["Google", "DuckDuckGo", "Bing", "Yahoo"];

    // ── Entry point ────────────────────────────────────────────────────────────
    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("[AGXIOME] DEEPSEEK_API_KEY not set.");
            Console.ResetColor();
            return 1;
        }

        var query   = GetArg(args, "--query") ?? "artificial intelligence 2025";
        var outRoot = GetArg(args, "--out")   ?? Path.Combine(Directory.GetCurrentDirectory(), "agxiome-scraper-output");
        var runDir  = Path.Combine(outRoot, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(runDir);

        PrintBanner(query, runDir);

        // ── Shared report channel (workers → orchestrator) ─────────────────────
        var channel = Channel.CreateBounded<WorkerReport>(new BoundedChannelOptions(Engines.Length)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        // ── Launch workers in parallel ─────────────────────────────────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[Phase 1] Launching {Engines.Length} scrapers in parallel…\n");
        Console.ResetColor();

        var workerTasks = Engines.Select(engine =>
            RunWorkerAsync(engine, query, runDir, channel.Writer, ct)).ToArray();

        await Task.WhenAll(workerTasks);
        channel.Writer.Complete();

        // ── Collect reports ────────────────────────────────────────────────────
        var reports = new List<WorkerReport>();
        await foreach (var report in channel.Reader.ReadAllAsync(ct))
            reports.Add(report);

        PrintWorkerSummary(reports);

        // ── Build orchestrator context from reports + file samples ─────────────
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n[Phase 2] Orchestrator (DeepSeek) validating results…\n");
        Console.ResetColor();

        var validationPrompt = BuildValidationPrompt(query, reports, runDir);
        var orchestratorResult = await RunOrchestratorAsync(apiKey, validationPrompt, ct);

        // ── Local validation (deterministic, independent of LLM) ──────────────
        var localAudit = LocalValidate(reports, runDir);

        // ── Final report ───────────────────────────────────────────────────────
        PrintFinalReport(orchestratorResult, localAudit, reports, runDir);

        return localAudit.AllPassed ? 0 : 2;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // WORKER AGENT
    // Each worker: fetches search engine → strips HTML → saves file → reports
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task RunWorkerAsync(
        string engine,
        string query,
        string outputDir,
        ChannelWriter<WorkerReport> reportChannel,
        CancellationToken ct)
    {
        var sw      = System.Diagnostics.Stopwatch.StartNew();
        var outFile = Path.Combine(outputDir, $"{engine.ToLower()}-results.txt");

        Console.WriteLine($"  [{engine,-10}] Starting scrape…");

        try
        {
            using var http = BuildHttpClient(engine);
            var url  = BuildSearchUrl(engine, query);
            var html = await FetchAsync(http, url, ct);

            if (string.IsNullOrWhiteSpace(html))
                throw new InvalidOperationException("Empty response from search engine.");

            var results = ParseResults(engine, html);
            var saved   = SaveResults(engine, query, url, results, outFile);

            sw.Stop();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [{engine,-10}] ✓ {results.Count} results saved → {Path.GetFileName(outFile)} ({sw.ElapsedMilliseconds}ms)");
            Console.ResetColor();

            await reportChannel.WriteAsync(new WorkerReport(
                Engine: engine, Success: true, FilePath: outFile,
                ResultCount: results.Count, HtmlTagsFound: 0,
                ElapsedMs: sw.ElapsedMilliseconds, Error: null), ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Worker failed: save an error file so orchestrator can audit it
            await File.WriteAllTextAsync(outFile,
                $"# {engine} scrape FAILED\n# Query: {query}\n# Error: {ex.Message}\n# Timestamp: {DateTime.UtcNow:O}\n",
                ct);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [{engine,-10}] ⚠ Failed ({ex.GetType().Name}): {ex.Message[..Math.Min(60, ex.Message.Length)]}");
            Console.ResetColor();

            await reportChannel.WriteAsync(new WorkerReport(
                Engine: engine, Success: false, FilePath: outFile,
                ResultCount: 0, HtmlTagsFound: 0,
                ElapsedMs: sw.ElapsedMilliseconds, Error: ex.Message), ct);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HTTP FETCH
    // ══════════════════════════════════════════════════════════════════════════

    private static HttpClient BuildHttpClient(string engine)
    {
        var handler = new HttpClientHandler
        {
            // Follow redirects; some engines redirect to HTTPS
            AllowAutoRedirect  = true,
            MaxAutomaticRedirections = 5,
            // Accept compressed responses
            AutomaticDecompression = System.Net.DecompressionMethods.GZip
                                   | System.Net.DecompressionMethods.Deflate
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        // Use a realistic user-agent to reduce bot-blocking (for local dev/demo only)
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        return http;
    }

    private static string BuildSearchUrl(string engine, string query)
    {
        var q = Uri.EscapeDataString(query);
        return engine switch
        {
            "Google"    => $"https://www.google.com/search?q={q}&num=10&hl=en",
            "DuckDuckGo"=> $"https://html.duckduckgo.com/html/?q={q}",
            "Bing"      => $"https://www.bing.com/search?q={q}&count=10",
            "Yahoo"     => $"https://search.yahoo.com/search?p={q}&n=10",
            _           => throw new ArgumentException($"Unknown engine: {engine}")
        };
    }

    private static async Task<string> FetchAsync(HttpClient http, string url, CancellationToken ct)
    {
        using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HTML PARSING & STRIPPING
    // ══════════════════════════════════════════════════════════════════════════

    private static List<SearchResultItem> ParseResults(string engine, string html)
    {
        // Strip the full page but also attempt to extract result blocks
        var stripped = StripHtml(html);
        var lines    = stripped.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Collect URL-like tokens from raw HTML as result anchors
        var urls = ExtractUrls(html, engine);

        // Pair URLs with surrounding text lines as snippets (best-effort)
        var results = new List<SearchResultItem>();
        int urlIdx = 0;
        foreach (var line in lines)
        {
            if (line.Length < 20) continue;            // skip noise
            if (line.StartsWith("http")) continue;     // skip raw URLs in text
            if (urlIdx < urls.Count)
            {
                results.Add(new SearchResultItem(
                    Title:   line[..Math.Min(120, line.Length)],
                    Url:     urls[urlIdx],
                    Snippet: line[..Math.Min(200, line.Length)]));
                urlIdx++;
            }
            else
            {
                results.Add(new SearchResultItem(line[..Math.Min(120, line.Length)], "", line));
            }
            if (results.Count >= 10) break;
        }
        return results;
    }

    private static List<string> ExtractUrls(string html, string engine)
    {
        // Extract result URLs from anchor href attributes; filter out engine-internal links
        var pattern = @"href=""(https?://[^""]+)""";
        var exclude = engine switch
        {
            "Google"    => "google.com",
            "DuckDuckGo"=> "duckduckgo.com",
            "Bing"      => "bing.com",
            "Yahoo"     => "yahoo.com",
            _           => ""
        };
        return Regex.Matches(html, pattern)
            .Select(m => m.Groups[1].Value)
            .Where(u => !u.Contains(exclude) && !u.Contains("javascript:"))
            .Distinct()
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Remove all HTML markup from a string.
    /// Pipeline: strip script/style blocks → strip all tags → decode entities → collapse whitespace.
    /// No external dependencies — regex-based for zero-allocation simplicity.
    /// </summary>
    internal static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // 1. Remove <script>…</script> and <style>…</style> blocks entirely
        var text = Regex.Replace(html,
            @"<(script|style)[^>]*>[\s\S]*?<\/\1>",
            " ", RegexOptions.IgnoreCase);

        // 2. Remove HTML comments
        text = Regex.Replace(text, @"<!--[\s\S]*?-->", " ");

        // 3. Replace block elements with newlines for readability
        text = Regex.Replace(text,
            @"<(br|p|div|li|tr|h[1-6]|blockquote|pre|section|article)[^>]*>",
            "\n", RegexOptions.IgnoreCase);

        // 4. Remove all remaining tags
        text = Regex.Replace(text, @"<[^>]+>", " ");

        // 5. Decode common HTML entities
        text = HttpUtility.HtmlDecode(text);

        // 6. Collapse multiple whitespace/newlines
        text = Regex.Replace(text, @"[ \t]{2,}", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        return text.Trim();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FILE SAVE
    // ══════════════════════════════════════════════════════════════════════════

    private static int SaveResults(
        string engine, string query, string url,
        List<SearchResultItem> results, string outFile)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AGXIOME Web Scraper — {engine}");
        sb.AppendLine($"# Query     : {query}");
        sb.AppendLine($"# Source URL: {url}");
        sb.AppendLine($"# Scraped   : {DateTime.UtcNow:O}");
        sb.AppendLine($"# Results   : {results.Count}");
        sb.AppendLine($"# HTML tags : 0 (stripped)");
        sb.AppendLine();

        int rank = 1;
        foreach (var r in results)
        {
            sb.AppendLine($"[{rank++}] {r.Title}");
            if (!string.IsNullOrWhiteSpace(r.Url))
                sb.AppendLine($"    URL: {r.Url}");
            if (!string.IsNullOrWhiteSpace(r.Snippet) && r.Snippet != r.Title)
                sb.AppendLine($"    {r.Snippet}");
            sb.AppendLine();
        }

        File.WriteAllText(outFile, sb.ToString(), Encoding.UTF8);
        return results.Count;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // ORCHESTRATOR (DeepSeek)
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task<string> RunOrchestratorAsync(
        string apiKey, string validationPrompt, CancellationToken ct)
    {
        var llmConfig = new LLMWIKYConfig
        {
            Provider   = LLMProviderType.DeepSeek,
            Model      = "deepseek-chat",
            ApiKey     = apiKey,
            MaxTokens  = 2048,
            DefaultTemperature = 0.2   // low temp for deterministic validation
        };

        var agentConfig = new AgentConfig
        {
            Id           = "scraper-orchestrator",
            Name         = "Web Scraper Orchestrator",
            SystemPrompt =
                "You are a quality-assurance orchestrator for a multi-agent web scraping pipeline. " +
                "Your job is to audit scraped files: verify HTML was fully stripped, " +
                "results are meaningful plain text, and flag any anomalies. " +
                "Be concise. Output a structured audit report.",
            MaxIterations = 2,
            Timeout       = TimeSpan.FromSeconds(60),
            ReasoningEngine = llmConfig,
        };

        AgentFactory.Configure(LoggerFactory.Create(b => b
            .AddConsole()
            .AddFilter("AGXIOME", LogLevel.Warning)
            .AddFilter("Microsoft", LogLevel.Error)));

        var engine = new LLMWIKYEngine(llmConfig);
        var agent  = AgentFactory.CreateWithEngine(agentConfig, engine);

        try
        {
            var resp = await agent.RunAsync(new AgentRequest(validationPrompt), ct);
            return resp.Content;
        }
        catch (Exception ex)
        {
            return $"[Orchestrator LLM call failed: {ex.Message}]";
        }
    }

    private static string BuildValidationPrompt(
        string query, List<WorkerReport> reports, string runDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"You are auditing a multi-agent web scraping run for query: \"{query}\"");
        sb.AppendLine();
        sb.AppendLine("## Worker Reports");
        foreach (var r in reports)
        {
            var status = r.Success ? "SUCCESS" : "FAILED";
            sb.AppendLine($"- [{status}] {r.Engine}: {r.ResultCount} results, {r.ElapsedMs}ms" +
                          (r.Error != null ? $", error={r.Error[..Math.Min(80, r.Error.Length)]}" : ""));
        }

        sb.AppendLine();
        sb.AppendLine("## File Content Samples (first 30 lines of each saved file)");

        foreach (var r in reports.Where(r => File.Exists(r.FilePath)))
        {
            sb.AppendLine($"\n### {r.Engine} ({Path.GetFileName(r.FilePath)})");
            var lines = File.ReadLines(r.FilePath).Take(30).ToList();
            foreach (var line in lines) sb.AppendLine(line);

            // Count any residual HTML tags in saved file (should be 0)
            var content    = File.ReadAllText(r.FilePath);
            var tagMatches = Regex.Matches(content, @"<[a-zA-Z][^>]{0,100}>").Count;
            sb.AppendLine($"[HTML tag count in saved file: {tagMatches}]");
        }

        sb.AppendLine();
        sb.AppendLine("## Your Task");
        sb.AppendLine("1. Confirm whether HTML was properly stripped (0 tags = pass, >0 = fail).");
        sb.AppendLine("2. Assess if content is meaningful plain text.");
        sb.AppendLine("3. Note any failed workers and likely cause.");
        sb.AppendLine("4. Give a PASS/FAIL verdict per engine and an overall verdict.");
        sb.AppendLine("Format: structured list, max 300 words.");

        return sb.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // LOCAL (DETERMINISTIC) VALIDATION
    // ══════════════════════════════════════════════════════════════════════════

    private static LocalAuditResult LocalValidate(List<WorkerReport> reports, string runDir)
    {
        var checks  = new List<(string Engine, bool Pass, string Detail)>();
        var htmlTag = new Regex(@"<[a-zA-Z][^>]{0,100}>", RegexOptions.Compiled);

        foreach (var r in reports)
        {
            if (!File.Exists(r.FilePath))
            {
                checks.Add((r.Engine, false, "File not found"));
                continue;
            }
            var content  = File.ReadAllText(r.FilePath);
            var tagCount = htmlTag.Matches(content).Count;
            var hasContent = content.Length > 50;

            if (!r.Success)
            {
                checks.Add((r.Engine, false, $"Worker failed: {r.Error?[..Math.Min(60, r.Error?.Length ?? 0)]}"));
                continue;
            }
            if (tagCount > 0)
            {
                checks.Add((r.Engine, false, $"HTML not fully stripped: {tagCount} tags found"));
                continue;
            }
            if (!hasContent)
            {
                checks.Add((r.Engine, false, "File empty or too short"));
                continue;
            }
            checks.Add((r.Engine, true, $"OK — {r.ResultCount} results, {content.Length} chars, 0 HTML tags"));
        }

        return new LocalAuditResult(checks, checks.All(c => c.Pass));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CONSOLE OUTPUT
    // ══════════════════════════════════════════════════════════════════════════

    private static void PrintBanner(string query, string runDir)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  AGXIOME — Multi-Agent Web Scraper Orchestration Demo        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine($"  Query   : {query}");
        Console.WriteLine($"  Engines : {string.Join(", ", Engines)}");
        Console.WriteLine($"  Output  : {runDir}");
        Console.WriteLine($"  Goal    : Scrape top-10 results per engine, strip HTML, validate");
    }

    private static void PrintWorkerSummary(List<WorkerReport> reports)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("── Worker Summary ───────────────────────────────────────────────");
        Console.ResetColor();
        foreach (var r in reports)
        {
            var status = r.Success ? "✓" : "✗";
            var color  = r.Success ? ConsoleColor.Green : ConsoleColor.Red;
            Console.ForegroundColor = color;
            Console.Write($"  {status} {r.Engine,-12}");
            Console.ResetColor();
            Console.Write($"  {r.ResultCount,3} results");
            Console.WriteLine($"  {r.ElapsedMs,5}ms  {Path.GetFileName(r.FilePath)}");
        }
    }

    private static void PrintFinalReport(
        string llmVerdict, LocalAuditResult audit,
        List<WorkerReport> reports, string runDir)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("── Orchestrator (DeepSeek) Validation ───────────────────────────");
        Console.ResetColor();
        Console.WriteLine(llmVerdict);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("── Local Deterministic Audit ────────────────────────────────────");
        Console.ResetColor();
        foreach (var (engine, pass, detail) in audit.Checks)
        {
            Console.ForegroundColor = pass ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write($"  {(pass ? "PASS" : "FAIL")} ");
            Console.ResetColor();
            Console.WriteLine($"{engine,-12} {detail}");
        }
        Console.WriteLine();
        Console.ForegroundColor = audit.AllPassed ? ConsoleColor.Green : ConsoleColor.Yellow;
        Console.WriteLine($"  Overall: {(audit.AllPassed ? "ALL CHECKS PASSED" : "SOME CHECKS FAILED")}");
        Console.ResetColor();
        Console.WriteLine($"\n  Files saved in: {runDir}");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static string? GetArg(string[] args, string flag)
    {
        var idx = Array.IndexOf(args, flag);
        return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // RECORDS
    // ══════════════════════════════════════════════════════════════════════════

    private record WorkerReport(
        string Engine,
        bool   Success,
        string FilePath,
        int    ResultCount,
        int    HtmlTagsFound,
        long   ElapsedMs,
        string? Error);

    private record SearchResultItem(string Title, string Url, string Snippet);

    private record LocalAuditResult(
        List<(string Engine, bool Pass, string Detail)> Checks,
        bool AllPassed);
}
