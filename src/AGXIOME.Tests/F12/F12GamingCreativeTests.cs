using AGXIOME.Toolkits;
using AGXIOME.Toolkits.GamingCreative;

namespace AGXIOME.Tests.F12;

public sealed class SteamToolkitTests
{
    [Fact] public void Id_IsCorrect() => Assert.Equal("steam", new SteamToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Steam", new SteamToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new SteamToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SteamToolkit().ExecuteAsync("get_player_summary", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("twitch", new TwitchToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Twitch", new TwitchToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new TwitchToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new TwitchToolkit().ExecuteAsync("get_streams", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("spotify", new SpotifyToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Spotify", new SpotifyToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new SpotifyToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new SpotifyToolkit().ExecuteAsync("search_tracks", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("lastfm", new LastFmToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Last.fm", new LastFmToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new LastFmToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new LastFmToolkit().ExecuteAsync("get_top_tracks", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("unsplash", new UnsplashToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Unsplash", new UnsplashToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new UnsplashToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new UnsplashToolkit().ExecuteAsync("search_photos", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("pexels", new PexelsToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Pexels", new PexelsToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new PexelsToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new PexelsToolkit().ExecuteAsync("search_photos", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("giphy", new GiphyToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("Giphy", new GiphyToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new GiphyToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new GiphyToolkit().ExecuteAsync("search_gifs", [], default).GetAwaiter().GetResult();
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
    [Fact] public void Id_IsCorrect() => Assert.Equal("openai_image_gen", new OpenAiImageGenToolkit().Id);
    [Fact] public void Name_IsCorrect() => Assert.Equal("OpenAI Image Gen", new OpenAiImageGenToolkit().Name);
    [Fact] public void Category_IsCorrect() => Assert.Equal(ToolkitCategory.GamingCreative, new OpenAiImageGenToolkit().Category);
    [Fact] public void NotConfigured_ReturnsSkipped()
    {
        var r = new OpenAiImageGenToolkit().ExecuteAsync("generate", [], default).GetAwaiter().GetResult();
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("openai_image_gen", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new OpenAiImageGenToolkit(cfg).ExecuteAsync("generate", new Dictionary<string, object?> { ["prompt"] = "a fox", ["size"] = "512x512" }, default);
        Assert.Equal(ToolkitResultStatus.Ok, r.Status);
    }
}
