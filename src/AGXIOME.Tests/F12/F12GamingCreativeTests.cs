using AGXIOME.Toolkits;
using AGXIOME.Toolkits.GamingCreative;

namespace AGXIOME.Tests.F12;

public sealed class SteamToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("steam", new SteamToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Steam", new SteamToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new SteamToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SteamToolkit().ExecuteAsync("get_player_summary", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("steam", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SteamToolkit(cfg).ExecuteAsync("get_player_summary", new Dictionary<string, object?> { ["steam_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class TwitchToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("twitch", new TwitchToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Twitch", new TwitchToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new TwitchToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TwitchToolkit().ExecuteAsync("get_streams", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("twitch", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TwitchToolkit(cfg).ExecuteAsync("get_streams", new Dictionary<string, object?> { ["game_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class SpotifyToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("spotify", new SpotifyToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Spotify", new SpotifyToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new SpotifyToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new SpotifyToolkit().ExecuteAsync("search_tracks", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("spotify", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new SpotifyToolkit(cfg).ExecuteAsync("search_tracks", new Dictionary<string, object?> { ["query"] = "rock" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class LastFmToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("lastfm", new LastFmToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Last.fm", new LastFmToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new LastFmToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new LastFmToolkit().ExecuteAsync("get_top_tracks", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("lastfm", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new LastFmToolkit(cfg).ExecuteAsync("get_top_tracks", new Dictionary<string, object?> { ["artist"] = "Radiohead" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class UnsplashToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("unsplash", new UnsplashToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Unsplash", new UnsplashToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new UnsplashToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new UnsplashToolkit().ExecuteAsync("search_photos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("unsplash", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new UnsplashToolkit(cfg).ExecuteAsync("search_photos", new Dictionary<string, object?> { ["query"] = "nature" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class PexelsToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("pexels", new PexelsToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Pexels", new PexelsToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new PexelsToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new PexelsToolkit().ExecuteAsync("search_photos", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("pexels", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new PexelsToolkit(cfg).ExecuteAsync("search_photos", new Dictionary<string, object?> { ["query"] = "city" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class GiphyToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("giphy", new GiphyToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Giphy", new GiphyToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new GiphyToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GiphyToolkit().ExecuteAsync("search_gifs", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("giphy", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GiphyToolkit(cfg).ExecuteAsync("search_gifs", new Dictionary<string, object?> { ["query"] = "funny" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}

public sealed class OpenAiImageGenToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("openai_image_gen", new OpenAiImageGenToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("OpenAI Image Gen", new OpenAiImageGenToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new OpenAiImageGenToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new OpenAiImageGenToolkit().ExecuteAsync("generate", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("openai_image_gen", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new OpenAiImageGenToolkit(cfg).ExecuteAsync("generate", new Dictionary<string, object?> { ["prompt"] = "a fox", ["size"] = "512x512" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
