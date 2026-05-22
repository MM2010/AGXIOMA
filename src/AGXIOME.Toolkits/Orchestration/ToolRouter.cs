// CO-A3 — ToolRouter
// Routes a logical goal string to a concrete, configured IToolkit using fallback chains.
// No external dependencies — pure registry lookup with deterministic fallback logic.

namespace AGXIOME.Toolkits.Orchestration;

/// <summary>
/// Routes a logical capability goal (e.g. <c>"sql"</c>, <c>"spreadsheet"</c>, <c>"search"</c>)
/// to a concrete, configured <see cref="IToolkit"/> instance from the <see cref="CapabilityRegistry"/>.
///
/// <para>Resolution order:</para>
/// <list type="number">
///   <item><description>Exact ID match — returns the toolkit if <c>IsConfigured</c>.</description></item>
///   <item><description>Fallback chain — iterates the predefined chain, returns first configured.</description></item>
///   <item><description>Category match — returns first configured toolkit in that category.</description></item>
///   <item><description>Best-effort — returns first available even if not configured (caller decides).</description></item>
///   <item><description><c>null</c> — no match found.</description></item>
/// </list>
/// </summary>
public sealed class ToolRouter
{
    // ── Fallback chains ───────────────────────────────────────────────────────
    // Key: logical goal (lower-case).  Value: ordered list of toolkit IDs to try.
    private static readonly IReadOnlyDictionary<string, string[]> FallbackChains =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // Data storage
            ["sql"]         = ["sqlite_query", "postgresql_query", "mysql", "duckdb"],
            ["database"]    = ["sqlite_query", "postgresql_query", "mysql"],
            ["nosql"]       = ["mongodb", "cosmosdb", "firestore"],
            ["cache"]       = ["redis", "memcached"],
            // Tabular data
            ["spreadsheet"] = ["excel", "csv", "pandas_like"],
            ["table"]       = ["pandas_like", "csv", "excel", "sqlite_query"],
            ["dataframe"]   = ["pandas_like", "duckdb", "sqlite_query"],
            // Documents
            ["document"]    = ["json_data", "xml_data", "csv"],
            ["file"]        = ["json_data", "xml_data", "csv", "excel"],
            // Search / knowledge
            ["search"]      = ["tavily", "bing_search", "serp_api", "google_search"],
            ["knowledge"]   = ["wikipedia", "arxiv", "pubmed"],
            ["vector"]      = ["hnswlib", "pgvector", "qdrant", "weaviate"],
            // Communication
            ["message"]     = ["slack", "teams", "discord", "telegram"],
            ["email"]       = ["gmail", "sendgrid", "mailchimp"],
            ["notification"] = ["pushbullet", "twilio", "webhook"],
            // Productivity
            ["tasks"]       = ["jira", "linear", "trello", "asana", "notion"],
            ["calendar"]    = ["google_calendar", "outlook_calendar", "calendly"],
            // Code / dev
            ["code"]        = ["git", "github", "gitlab", "bitbucket"],
            ["infra"]       = ["docker", "kubernetes", "terraform"],
            // Cloud storage
            ["storage"]     = ["s3", "azure_blob", "google_drive", "dropbox"],
            // Finance
            ["stock"]       = ["yahoo_finance", "alpha_vantage", "polygon_io"],
            ["crypto"]      = ["binance", "coinbase", "kraken"],
        };

    private readonly CapabilityRegistry _registry;

    public ToolRouter(CapabilityRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Route a goal to the best available toolkit.
    /// </summary>
    /// <param name="goal">
    /// Either a toolkit ID (e.g. <c>"sqlite_query"</c>), a logical alias
    /// (e.g. <c>"sql"</c>, <c>"spreadsheet"</c>), or a category name
    /// (e.g. <c>"DataAnalysis"</c>).
    /// </param>
    /// <param name="configuredOnly">
    /// When <c>true</c>, only returns configured toolkits. When <c>false</c>,
    /// falls back to unconfigured ones as a last resort.
    /// </param>
    /// <returns>The best matching <see cref="IToolkit"/>, or <c>null</c> if none found.</returns>
    public IToolkit? Route(string goal, bool configuredOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);

        // 1. Exact ID match
        var direct = _registry.ResolveToolkit(goal);
        if (direct is not null && (direct.IsConfigured || !configuredOnly))
            return direct;

        // 2. Fallback chain
        if (FallbackChains.TryGetValue(goal, out var chain))
        {
            // First pass: configured only
            foreach (var id in chain)
            {
                var t = _registry.ResolveToolkit(id);
                if (t?.IsConfigured == true) return t;
            }
            // Second pass (if configuredOnly is false): any registered
            if (!configuredOnly)
            {
                foreach (var id in chain)
                {
                    var t = _registry.ResolveToolkit(id);
                    if (t is not null) return t;
                }
            }
        }

        // 3. Category match (goal string == enum name)
        if (Enum.TryParse<ToolkitCategory>(goal, ignoreCase: true, out var category))
        {
            var byCategory = _registry.GetByCategory(category);
            var configured = byCategory
                .Select(m => _registry.ResolveToolkit(m.Id))
                .FirstOrDefault(t => t?.IsConfigured == true);
            if (configured is not null) return configured;
            if (!configuredOnly)
                return byCategory.Select(m => _registry.ResolveToolkit(m.Id)).FirstOrDefault(t => t is not null);
        }

        return null;
    }

    /// <summary>
    /// Route and return a manifest instead of the toolkit instance.
    /// Useful for planner-side reasoning without instantiating the toolkit.
    /// </summary>
    public CapabilityManifest? RouteToManifest(string goal, bool configuredOnly = false)
    {
        var toolkit = Route(goal, configuredOnly);
        return toolkit is null ? null : _registry.FindById(toolkit.Id);
    }

    /// <summary>Return all known fallback aliases (keys of the fallback chain table).</summary>
    public static IEnumerable<string> KnownAliases => FallbackChains.Keys;
}
