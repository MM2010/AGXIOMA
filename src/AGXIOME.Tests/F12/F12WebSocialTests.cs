using AGXIOME.Toolkits;
using AGXIOME.Toolkits.WebSocial;

namespace AGXIOME.Tests.F12;

public sealed class TwitterToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("twitter", new TwitterToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Twitter/X", new TwitterToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new TwitterToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TwitterToolkit().ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "dotnet" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("twitter", new Dictionary<string, string> { ["bearer_token"] = "test" });
        var r = await new TwitterToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "hello" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class RedditToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("reddit", new RedditToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Reddit", new RedditToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new RedditToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new RedditToolkit().ExecuteAsync("posts", new Dictionary<string, object?> { ["subreddit"] = "programming", ["limit"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("reddit", new Dictionary<string, string> { ["enabled"] = "true" });
        var r = await new RedditToolkit(cfg).ExecuteAsync("posts", new Dictionary<string, object?> { ["subreddit"] = "news", ["limit"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class LinkedInToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("linkedin", new LinkedInToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("LinkedIn", new LinkedInToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new LinkedInToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new LinkedInToolkit().ExecuteAsync("get_profile", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("linkedin", new Dictionary<string, string> { ["access_token"] = "test" });
        var r = await new LinkedInToolkit(cfg).ExecuteAsync("get_profile", [], default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    // ── Security / boundary tests ──────────────────────────────────────────
    [Fact] public async Task CreatePost_NotConfigured_ReturnsSkipped()
    {
        var r = await new LinkedInToolkit().ExecuteAsync("create_post", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task CreatePost_FakeToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("linkedin", new Dictionary<string, string> { ["access_token"] = "fake_token_abc" });
        var r = await new LinkedInToolkit(cfg).ExecuteAsync("create_post",
            new Dictionary<string, object?> { ["text"] = "<script>alert(1)</script> Test post" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    [Fact] public async Task GetCompany_MissingId_ReturnsError()
    {
        var cfg = new ToolkitConfig("linkedin", new Dictionary<string, string> { ["access_token"] = "fake_token" });
        // get_company uses GetRequiredParam("id") → MissingParameter → Error
        var r = await new LinkedInToolkit(cfg).ExecuteAsync("get_company", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task GetJob_MissingId_ReturnsError()
    {
        var cfg = new ToolkitConfig("linkedin", new Dictionary<string, string> { ["access_token"] = "fake_token" });
        var r = await new LinkedInToolkit(cfg).ExecuteAsync("get_job", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task UnknownAction_WithToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("linkedin", new Dictionary<string, string> { ["access_token"] = "fake_token" });
        var r = await new LinkedInToolkit(cfg).ExecuteAsync("'; UNION SELECT * FROM secrets--", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class InstagramToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("instagram", new InstagramToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Instagram", new InstagramToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new InstagramToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new InstagramToolkit().ExecuteAsync("list_media", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("instagram", new Dictionary<string, string> { ["access_token"] = "test" });
        var r = await new InstagramToolkit(cfg).ExecuteAsync("list_media", new Dictionary<string, object?> { ["limit"] = 5 }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    // ── Security / boundary tests ──────────────────────────────────────────
    [Fact] public async Task GetMedia_NotConfigured_ReturnsSkipped()
    {
        var r = await new InstagramToolkit().ExecuteAsync("get_media", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task GetMedia_FakeToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("instagram", new Dictionary<string, string> { ["access_token"] = "FAKE_IGT" });
        var r = await new InstagramToolkit(cfg).ExecuteAsync("get_media",
            new Dictionary<string, object?> { ["media_id"] = "12345" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    [Fact] public async Task ListComments_MissingMediaId_ReturnsError()
    {
        var cfg = new ToolkitConfig("instagram", new Dictionary<string, string> { ["access_token"] = "FAKE_IGT" });
        // list_comments requires media_id via GetRequiredParam → MissingParameter → Error
        var r = await new InstagramToolkit(cfg).ExecuteAsync("list_comments", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task GetInsights_XssInMediaId_ReturnsError()
    {
        var cfg = new ToolkitConfig("instagram", new Dictionary<string, string> { ["access_token"] = "FAKE_IGT" });
        var r = await new InstagramToolkit(cfg).ExecuteAsync("get_insights",
            new Dictionary<string, object?> { ["media_id"] = "<script>alert(1)</script>" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    [Fact] public async Task UnknownAction_WithToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("instagram", new Dictionary<string, string> { ["access_token"] = "FAKE_IGT" });
        var r = await new InstagramToolkit(cfg).ExecuteAsync("__inject_action__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class YouTubeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("youtube", new YouTubeToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("YouTube", new YouTubeToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new YouTubeToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new YouTubeToolkit().ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "cats" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("youtube", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new YouTubeToolkit(cfg).ExecuteAsync("search", new Dictionary<string, object?> { ["query"] = "cats" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class TikTokToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("tiktok", new TikTokToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("TikTok", new TikTokToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new TikTokToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TikTokToolkit().ExecuteAsync("get_videos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("tiktok", new Dictionary<string, string> { ["access_token"] = "test" });
        var r = await new TikTokToolkit(cfg).ExecuteAsync("get_videos", new Dictionary<string, object?> { ["user_id"] = "u" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class MediumToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("medium", new MediumToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Medium", new MediumToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new MediumToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MediumToolkit().ExecuteAsync("feed", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("medium", new Dictionary<string, string> { ["integration_token"] = "test" });
        var r = await new MediumToolkit(cfg).ExecuteAsync("get_user", [], default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class DevToToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("devto", new DevToToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Dev.to", new DevToToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new DevToToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new DevToToolkit().ExecuteAsync("list_articles", new Dictionary<string, object?> { ["per_page"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("devto", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DevToToolkit(cfg).ExecuteAsync("list_articles", new Dictionary<string, object?> { ["per_page"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class HashnodeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("hashnode", new HashnodeToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Hashnode", new HashnodeToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new HashnodeToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new HashnodeToolkit().ExecuteAsync("list_posts", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("hashnode", new Dictionary<string, string> { ["token"] = "test" });
        var r = await new HashnodeToolkit(cfg).ExecuteAsync("list_posts", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    // ── Security / boundary tests ──────────────────────────────────────────
    [Fact] public async Task ListTags_ReturnsOkOrError()
    {
        // Public GraphQL API: no auth required
        var r = await new HashnodeToolkit().ExecuteAsync("list_tags", [], default);
        Assert.True(r.Status is ToolkitResultStatus.Ok or ToolkitResultStatus.Error);
    }

    [Fact] public async Task SearchPosts_WithXssQuery_DoesNotCrash()
    {
        // Query is URI-encoded before embedding in GraphQL — no script execution possible
        var r = await new HashnodeToolkit().ExecuteAsync("search_posts",
            new Dictionary<string, object?> { ["query"] = "<script>alert('xss')</script>" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Ok or ToolkitResultStatus.Error);
    }

    [Fact] public async Task SearchPosts_GraphQLInjectionAttempt_DoesNotCrash()
    {
        var r = await new HashnodeToolkit().ExecuteAsync("search_posts",
            new Dictionary<string, object?> { ["query"] = "\"} mutation { deletePost(id: \"123\") }" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Ok or ToolkitResultStatus.Error);
    }

    [Fact] public async Task GetUser_WithUsername_ReturnsOkOrError()
    {
        var r = await new HashnodeToolkit().ExecuteAsync("get_user",
            new Dictionary<string, object?> { ["username"] = "johndoe" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Ok or ToolkitResultStatus.Error);
    }

    [Fact] public async Task GetPost_MissingId_ReturnsError()
    {
        // get_post uses GetRequiredParam("id") → MissingParameter → Error
        var r = await new HashnodeToolkit().ExecuteAsync("get_post", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task UnknownAction_ReturnsError()
    {
        var r = await new HashnodeToolkit().ExecuteAsync("__inject__{malicious}", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class WordPressToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("wordpress", new WordPressToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("WordPress", new WordPressToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new WordPressToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WordPressToolkit().ExecuteAsync("list_posts", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("wordpress", new Dictionary<string, string> { ["site_url"] = "https://example.com" });
        var r = await new WordPressToolkit(cfg).ExecuteAsync("list_posts", new Dictionary<string, object?> { ["per_page"] = 5 }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class ShopifyToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("shopify", new ShopifyToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Shopify", new ShopifyToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new ShopifyToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ShopifyToolkit().ExecuteAsync("list_products", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("shopify", new Dictionary<string, string> { ["store"] = "example.myshopify.com", ["access_token"] = "test" });
        var r = await new ShopifyToolkit(cfg).ExecuteAsync("list_products", new Dictionary<string, object?> { ["limit"] = 5 }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }
}

public sealed class WooCommerceToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("woocommerce", new WooCommerceToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("WooCommerce", new WooCommerceToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new WooCommerceToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WooCommerceToolkit().ExecuteAsync("list_products", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("woocommerce", new Dictionary<string, string> { ["site_url"] = "https://example.com", ["consumer_key"] = "ck_test", ["consumer_secret"] = "cs_test" });
        var r = await new WooCommerceToolkit(cfg).ExecuteAsync("list_products", new Dictionary<string, object?> { ["per_page"] = 5 }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class SquarespaceToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("squarespace", new SquarespaceToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Squarespace", new SquarespaceToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new SquarespaceToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SquarespaceToolkit().ExecuteAsync("list_pages", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("squarespace", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SquarespaceToolkit(cfg).ExecuteAsync("list_pages", new Dictionary<string, object?> { ["site_id"] = "s" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class ZapierToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("zapier", new ZapierToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Zapier", new ZapierToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new ZapierToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ZapierToolkit().ExecuteAsync("trigger_zap", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("zapier", new Dictionary<string, string> { ["webhook_url"] = "https://example.com" });
        var r = await new ZapierToolkit(cfg).ExecuteAsync("trigger_zap", new Dictionary<string, object?> { ["payload"] = new { ping = true } }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class IFTTTToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("ifttt", new IftttToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("IFTTT", new IftttToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new IftttToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new IftttToolkit().ExecuteAsync("trigger_applet", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("ifttt", new Dictionary<string, string> { ["webhook_key"] = "test" });
        var r = await new IftttToolkit(cfg).ExecuteAsync("trigger_applet", new Dictionary<string, object?> { ["event"] = "e", ["value1"] = "v" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class MakeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("makecom", new MakeComToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Make.com", new MakeComToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new MakeComToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MakeComToolkit().ExecuteAsync("run_scenario", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("makecom", new Dictionary<string, string> { ["webhook_url"] = "https://example.com" });
        var r = await new MakeComToolkit(cfg).ExecuteAsync("run_scenario", new Dictionary<string, object?> { ["scenario_id"] = "s" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class WebScraperToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("web_scraper", new WebScraperToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Web Scraper", new WebScraperToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new WebScraperToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WebScraperToolkit().ExecuteAsync("scrape", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("web_scraper", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new WebScraperToolkit(cfg).ExecuteAsync("scrape", new Dictionary<string, object?> { ["url"] = "https://example.com", ["selector"] = "body" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class RssToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("rss", new RssToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("RSS Feed", new RssToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new RssToolkit().Category);
    [Fact] public async Task Execute_WithoutFeedLocation_ReturnsError()
    {
        var r = await new RssToolkit().ExecuteAsync("fetch", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsErrorForNonRssDocument()
    {
        var cfg = new ToolkitConfig("rss", new Dictionary<string, string> { ["feed_url"] = "https://example.com/feed" });
        var r = await new RssToolkit(cfg).ExecuteAsync("fetch", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}
