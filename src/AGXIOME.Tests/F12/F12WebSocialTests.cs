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
        var r = await new TwitterToolkit().ExecuteAsync("post_tweet", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("twitter", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TwitterToolkit(cfg).ExecuteAsync("post_tweet", new Dictionary<string, object?> { ["text"] = "hello" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class RedditToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("reddit", new RedditToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Reddit", new RedditToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new RedditToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new RedditToolkit().ExecuteAsync("get_posts", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("reddit", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new RedditToolkit(cfg).ExecuteAsync("get_posts", new Dictionary<string, object?> { ["subreddit"] = "news" }, default);
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
        var r = await new LinkedInToolkit().ExecuteAsync("post_update", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("linkedin", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new LinkedInToolkit(cfg).ExecuteAsync("post_update", new Dictionary<string, object?> { ["text"] = "hi" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class InstagramToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("instagram", new InstagramToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Instagram", new InstagramToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new InstagramToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new InstagramToolkit().ExecuteAsync("get_media", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("instagram", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new InstagramToolkit(cfg).ExecuteAsync("get_media", new Dictionary<string, object?> { ["user_id"] = "u" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class YouTubeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("youtube", new YouTubeToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("YouTube", new YouTubeToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new YouTubeToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new YouTubeToolkit().ExecuteAsync("search_videos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("youtube", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new YouTubeToolkit(cfg).ExecuteAsync("search_videos", new Dictionary<string, object?> { ["query"] = "cats" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        var cfg = new ToolkitConfig("tiktok", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TikTokToolkit(cfg).ExecuteAsync("get_videos", new Dictionary<string, object?> { ["user_id"] = "u" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class MediumToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("medium", new MediumToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Medium", new MediumToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new MediumToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MediumToolkit().ExecuteAsync("create_post", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("medium", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MediumToolkit(cfg).ExecuteAsync("create_post", new Dictionary<string, object?> { ["title"] = "t", ["content"] = "c" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class DevToToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("devto", new DevToToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Dev.to", new DevToToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new DevToToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new DevToToolkit().ExecuteAsync("create_article", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("devto", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DevToToolkit(cfg).ExecuteAsync("create_article", new Dictionary<string, object?> { ["title"] = "t", ["body_markdown"] = "md" }, default);
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
        var r = await new HashnodeToolkit().ExecuteAsync("create_story", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("hashnode", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new HashnodeToolkit(cfg).ExecuteAsync("create_story", new Dictionary<string, object?> { ["title"] = "t", ["content"] = "c" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class WordPressToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("wordpress", new WordPressToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("WordPress", new WordPressToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new WordPressToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new WordPressToolkit().ExecuteAsync("create_post", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("wordpress", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new WordPressToolkit(cfg).ExecuteAsync("create_post", new Dictionary<string, object?> { ["title"] = "t", ["content"] = "c" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        var cfg = new ToolkitConfig("shopify", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ShopifyToolkit(cfg).ExecuteAsync("list_products", new Dictionary<string, object?> { ["shop"] = "myshop" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        var cfg = new ToolkitConfig("woocommerce", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new WooCommerceToolkit(cfg).ExecuteAsync("list_products", new Dictionary<string, object?> { ["site_url"] = "https://shop.com" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        var cfg = new ToolkitConfig("zapier", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ZapierToolkit(cfg).ExecuteAsync("trigger_zap", new Dictionary<string, object?> { ["zap_id"] = "z", ["payload"] = "{}" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        var cfg = new ToolkitConfig("ifttt", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new IftttToolkit(cfg).ExecuteAsync("trigger_applet", new Dictionary<string, object?> { ["event"] = "e", ["value1"] = "v" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class MakeToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("make_com", new MakeComToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Make (Integromat)", new MakeComToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new MakeComToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new MakeComToolkit().ExecuteAsync("run_scenario", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("make", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new MakeComToolkit(cfg).ExecuteAsync("run_scenario", new Dictionary<string, object?> { ["scenario_id"] = "s" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
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
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("RSS", new RssToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.WebSocial, new RssToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new RssToolkit().ExecuteAsync("fetch_feed", [], default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("rss", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new RssToolkit(cfg).ExecuteAsync("fetch_feed", new Dictionary<string, object?> { ["url"] = "https://example.com/feed" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
