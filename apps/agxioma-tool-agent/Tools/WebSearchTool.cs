using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgxiomaToolAgent.Tools;

/// <summary>
/// Searches the web using the DuckDuckGo Instant Answer API.
/// No API key required. Returns abstract text + top related topics.
/// </summary>
public sealed class WebSearchTool : AgxiomeFunction
{
    private static readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("https://api.duckduckgo.com/"),
        DefaultRequestHeaders = { { "User-Agent", "AGXIOME/1.0 (tool-agent)" } }
    };

    public override string Name        => "web_search";
    public override string Description => "Search the web for current information. Returns a summary and related topics.";
    public override string? Version    => "1.0.0";

    [ToolParameter(Required = true, Description = "The search query string.")]
    public string Query { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "Maximum number of related topics to return (1-10, default 5).", Type = "integer")]
    public int MaxResults { get; set; } = 5;

    public override async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var query   = GetParam(context, "query", Query);
        var maxRes  = GetParam(context, "max_results", MaxResults);

        var url     = $"?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";
        var raw     = await _http.GetStringAsync(url, ct);
        var doc     = JsonDocument.Parse(raw);
        var root    = doc.RootElement;

        var results = new List<string>();

        // Primary abstract (Wikipedia-style summary)
        if (root.TryGetProperty("AbstractText", out var abs) && abs.GetString() is { Length: > 0 } absText)
            results.Add($"Summary: {absText}");

        // Related topics
        if (root.TryGetProperty("RelatedTopics", out var topics))
        {
            int count = 0;
            foreach (var topic in topics.EnumerateArray())
            {
                if (count >= maxRes) break;
                if (topic.TryGetProperty("Text", out var text) && text.GetString() is { Length: > 0 } t)
                {
                    results.Add(t);
                    count++;
                }
            }
        }

        if (results.Count == 0)
            results.Add($"No instant answer found for '{query}'. Try a more specific query.");

        var data = new
        {
            query,
            source = "DuckDuckGo Instant Answer",
            results
        };

        return new ToolResult(
            Success: true,
            Data: data,
            Metadata: new ToolMetadata(Name, Version, ItemsProcessed: results.Count)
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T GetParam<T>(ToolContext ctx, string key, T fallback)
    {
        if (ctx.Parameters != null && ctx.Parameters.TryGetValue(key, out var raw))
        {
            try { return (T)Convert.ChangeType(raw, typeof(T)); }
            catch { return fallback; }
        }
        return fallback;
    }
}
