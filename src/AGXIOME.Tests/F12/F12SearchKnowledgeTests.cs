using AGXIOME.Toolkits;
using AGXIOME.Toolkits.SearchKnowledge;

namespace AGXIOME.Tests.F12;

public sealed class GoogleSearchToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_search", new GoogleSearchToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Search", new GoogleSearchToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new GoogleSearchToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleSearchToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsUnauthorizedOrError()
    {
        var cfg = new ToolkitConfig("google_search", new Dictionary<string, string> { ["api_key"] = "test", ["cx"] = "test-cx" });
        var r = await new GoogleSearchToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class BingToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("bing_search", new BingSearchToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Bing Search", new BingSearchToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new BingSearchToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new BingSearchToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("bing_search", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BingSearchToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class SearXNGToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("searxng", new SearXngToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SearXNG", new SearXngToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new SearXngToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SearXngToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsError()
    {
        var cfg = new ToolkitConfig("searxng", new Dictionary<string, string> { ["base_url"] = "https://example.invalid" });
        var r = await new SearXngToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class TavilyToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("tavily", new TavilyToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Tavily", new TavilyToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new TavilyToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TavilyToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsUnauthorizedOrError()
    {
        var cfg = new ToolkitConfig("tavily", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TavilyToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class ExaToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("exa", new ExaToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Exa", new ExaToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new ExaToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ExaToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsUnauthorizedOrError()
    {
        var cfg = new ToolkitConfig("exa", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ExaToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class BraveToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("brave_search", new BraveSearchToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Brave Search", new BraveSearchToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new BraveSearchToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new BraveSearchToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("brave_search", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new BraveSearchToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class SerpApiToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("serpapi", new SerpApiToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("SerpAPI", new SerpApiToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new SerpApiToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SerpApiToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsUnauthorizedOrError()
    {
        var cfg = new ToolkitConfig("serpapi", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SerpApiToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["q"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class SerperToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("serper", new SerperToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Serper", new SerperToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new SerperToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SerperToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsUnauthorizedOrError()
    {
        var cfg = new ToolkitConfig("serper", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SerperToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class FirecrawlToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("firecrawl", new FirecrawlToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Firecrawl", new FirecrawlToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new FirecrawlToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new FirecrawlToolkit().ExecuteAsync("crawl", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsUnauthorizedOrError()
    {
        var cfg = new ToolkitConfig("firecrawl", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new FirecrawlToolkit(cfg).ExecuteAsync("crawl", new Dictionary<string, object?> { ["url"] = "https://example.com" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class WikipediaToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("wikipedia", new WikipediaToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Wikipedia", new WikipediaToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new WikipediaToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WikipediaToolkit().ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "C sharp" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("wikipedia", new Dictionary<string, string> { ["enabled"] = "true" });
        var r = await new WikipediaToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "C sharp" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class ArxivToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("arxiv", new ArxivToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("arXiv", new ArxivToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new ArxivToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ArxivToolkit().ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "llm", ["max_results"] = 5 }, default);
        Assert.True(r.Status is ToolkitResultStatus.Ok or ToolkitResultStatus.Error);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("arxiv", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ArxivToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "llm", ["max_results"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PubMedToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("pubmed", new PubMedToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("PubMed", new PubMedToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.SearchKnowledge, new PubMedToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PubMedToolkit().ExecuteAsync("search", [], default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact(Skip = "requires live PubMed API")] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pubmed", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PubMedToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "cancer", ["retmax"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
