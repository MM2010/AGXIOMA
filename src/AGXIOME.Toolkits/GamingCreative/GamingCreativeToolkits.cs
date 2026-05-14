// T-142..T-149 — Gaming & Creative Toolkits
using System.Net.Http.Json;
using System.Text.Json;

namespace AGXIOME.Toolkits.GamingCreative;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http { internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) }; }

// T-142 — Steam Web API (api_key required for some endpoints)
[Toolkit("steam","Steam",ToolkitCategory.GamingCreative)]
public sealed class SteamToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"steam";public override string Name=>"Steam";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??"";
        var keyParam=!string.IsNullOrWhiteSpace(key)?$"&key={key}":"";
        var steamId=GetParam<string>(p,"steam_id")??"";
        var appId=GetParam<string>(p,"app_id")??"";
        return a switch{
            "get_player_summaries"=>await StGet($"ISteamUser/GetPlayerSummaries/v2/?steamids={steamId}{keyParam}",ct).ConfigureAwait(false),
            "get_owned_games"     =>await StGet($"IPlayerService/GetOwnedGames/v1/?steamid={steamId}&include_appinfo=1{keyParam}",ct).ConfigureAwait(false),
            "get_app_details"     =>await StRaw($"https://store.steampowered.com/api/appdetails?appids={appId}",ct).ConfigureAwait(false),
            "get_news"            =>await StGet($"ISteamNews/GetNewsForApp/v2/?appid={appId}&count={GetParam<int?>(p,"count")??10}{keyParam}",ct).ConfigureAwait(false),
            "search_games"        =>await StRaw($"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&l=en&cc=US",ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: get_player_summaries|get_owned_games|get_app_details|get_news|search_games")
        };
    }
    private static async Task<ToolkitResult> StGet(string path,CancellationToken ct)=>ToolkitResult.Success(await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.steampowered.com/{path}",ct).ConfigureAwait(false));
    private static async Task<ToolkitResult> StRaw(string url,CancellationToken ct)=>ToolkitResult.Success(await Http.Client.GetFromJsonAsync<JsonElement>(url,ct).ConfigureAwait(false));
}

// T-143 — Twitch (Client Credentials OAuth — client_id + client_secret required)
[Toolkit("twitch","Twitch",ToolkitCategory.GamingCreative)]
public sealed class TwitchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"twitch";public override string Name=>"Twitch";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("client_id"));
    private string? _cachedToken;
    private DateTime _tokenExpiry=DateTime.MinValue;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var clientId=GetSetting("client_id")??throw new InvalidOperationException("Twitch client_id not set");
        var token=await GetToken(clientId,ct).ConfigureAwait(false);
        return a switch{
            "get_streams"      =>await TwGet($"streams?first={GetParam<int?>(p,"first")??10}",clientId,token,ct).ConfigureAwait(false),
            "search_channels"  =>await TwGet($"search/channels?query={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&first={GetParam<int?>(p,"first")??10}",clientId,token,ct).ConfigureAwait(false),
            "get_users"        =>await TwGet($"users?login={GetParam<string>(p,"login")}",clientId,token,ct).ConfigureAwait(false),
            "get_games"        =>await TwGet($"games/top?first={GetParam<int?>(p,"first")??10}",clientId,token,ct).ConfigureAwait(false),
            "get_channel_info" =>await TwGet($"channels?broadcaster_id={GetParam<string>(p,"broadcaster_id")?? throw new ArgumentException("broadcaster_id required")}",clientId,token,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: get_streams|search_channels|get_users|get_games|get_channel_info")
        };
    }
    private async Task<string> GetToken(string clientId,CancellationToken ct){
        if(_cachedToken!=null&&DateTime.UtcNow<_tokenExpiry)return _cachedToken;
        var secret=GetSetting("client_secret")??throw new InvalidOperationException("Twitch client_secret not set");
        var resp=await Http.Client.PostAsync($"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={secret}&grant_type=client_credentials",null,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        _cachedToken=json.GetProperty("access_token").GetString()!;
        _tokenExpiry=DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32()-60);
        return _cachedToken;
    }
    private static async Task<ToolkitResult> TwGet(string path,string clientId,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.twitch.tv/helix/{path}");
        req.Headers.Add("Client-Id",clientId);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-144 — Spotify (OAuth2 access_token required)
[Toolkit("spotify","Spotify",ToolkitCategory.GamingCreative)]
public sealed class SpotifyToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"spotify";public override string Name=>"Spotify";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetSetting("access_token")??throw new InvalidOperationException("Spotify access_token not set");
        return a switch{
            "search"         =>await SpGet($"search?q={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&type={GetParam<string>(p,"type")??"track"}&limit={GetParam<int?>(p,"limit")??10}",token,ct).ConfigureAwait(false),
            "get_track"      =>await SpGet($"tracks/{GetParam<string>(p,"id")?? throw new ArgumentException("id required")}",token,ct).ConfigureAwait(false),
            "get_artist"     =>await SpGet($"artists/{GetParam<string>(p,"id")?? throw new ArgumentException("id required")}",token,ct).ConfigureAwait(false),
            "get_playlist"   =>await SpGet($"playlists/{GetParam<string>(p,"id")?? throw new ArgumentException("id required")}",token,ct).ConfigureAwait(false),
            "recommendations"=>await SpGet($"recommendations?seed_tracks={GetParam<string>(p,"seed_tracks")??""}&seed_artists={GetParam<string>(p,"seed_artists")??""}&limit={GetParam<int?>(p,"limit")??10}",token,ct).ConfigureAwait(false),
            "current_user"   =>await SpGet("me",token,ct).ConfigureAwait(false),
            "new_releases"   =>await SpGet($"browse/new-releases?limit={GetParam<int?>(p,"limit")??10}",token,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: search|get_track|get_artist|get_playlist|recommendations|current_user|new_releases")
        };
    }
    private static async Task<ToolkitResult> SpGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.spotify.com/v1/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-145 — Last.fm (api_key required)
[Toolkit("lastfm","Last.fm",ToolkitCategory.GamingCreative)]
public sealed class LastFmToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"lastfm";public override string Name=>"Last.fm";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("Last.fm api_key not set");
        return a switch{
            "artist_info"   =>await LfGet($"method=artist.getinfo&artist={Uri.EscapeDataString(GetParam<string>(p,"artist")?? throw new ArgumentException("artist required"))}&api_key={key}&format=json",ct).ConfigureAwait(false),
            "track_info"    =>await LfGet($"method=track.getinfo&artist={Uri.EscapeDataString(GetParam<string>(p,"artist")?? throw new ArgumentException("artist required"))}&track={Uri.EscapeDataString(GetParam<string>(p,"track")?? throw new ArgumentException("track required"))}&api_key={key}&format=json",ct).ConfigureAwait(false),
            "top_tracks"    =>await LfGet($"method=artist.gettoptracks&artist={Uri.EscapeDataString(GetParam<string>(p,"artist")?? throw new ArgumentException("artist required"))}&api_key={key}&format=json",ct).ConfigureAwait(false),
            "similar_artists"=>await LfGet($"method=artist.getsimilar&artist={Uri.EscapeDataString(GetParam<string>(p,"artist")?? throw new ArgumentException("artist required"))}&api_key={key}&format=json",ct).ConfigureAwait(false),
            "search_track"  =>await LfGet($"method=track.search&track={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&api_key={key}&format=json&limit={GetParam<int?>(p,"limit")??10}",ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: artist_info|track_info|top_tracks|similar_artists|search_track")
        };
    }
    private static async Task<ToolkitResult> LfGet(string qs,CancellationToken ct)=>ToolkitResult.Success(await Http.Client.GetFromJsonAsync<JsonElement>($"https://ws.audioscrobbler.com/2.0/?{qs}",ct).ConfigureAwait(false));
}

// T-146 — Unsplash (Client-ID access_key required)
[Toolkit("unsplash","Unsplash",ToolkitCategory.GamingCreative)]
public sealed class UnsplashToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"unsplash";public override string Name=>"Unsplash";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("access_key")??throw new InvalidOperationException("Unsplash access_key not set");
        return a switch{
            "search"    =>await UnGet($"search/photos?query={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&per_page={GetParam<int?>(p,"per_page")??10}&page={GetParam<int?>(p,"page")??1}",key,ct).ConfigureAwait(false),
            "random"    =>await UnGet($"photos/random?count={GetParam<int?>(p,"count")??1}{(GetParam<string>(p,"query")!=null?$"&query={Uri.EscapeDataString(GetParam<string>(p,"query")!)}":"")}",key,ct).ConfigureAwait(false),
            "get_photo" =>await UnGet($"photos/{GetParam<string>(p,"id")?? throw new ArgumentException("id required")}",key,ct).ConfigureAwait(false),
            "list"      =>await UnGet($"photos?per_page={GetParam<int?>(p,"per_page")??10}&order_by={GetParam<string>(p,"order_by")??"popular"}",key,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: search|random|get_photo|list")
        };
    }
    private static async Task<ToolkitResult> UnGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.unsplash.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Client-ID",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-147 — Pexels (Authorization: api_key header required)
[Toolkit("pexels","Pexels",ToolkitCategory.GamingCreative)]
public sealed class PexelsToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"pexels";public override string Name=>"Pexels";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("Pexels api_key not set");
        return a switch{
            "search"        =>await PxGet($"v1/search?query={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&per_page={GetParam<int?>(p,"per_page")??15}",key,ct).ConfigureAwait(false),
            "curated"       =>await PxGet($"v1/curated?per_page={GetParam<int?>(p,"per_page")??15}",key,ct).ConfigureAwait(false),
            "get_photo"     =>await PxGet($"v1/photos/{GetParam<string>(p,"id")?? throw new ArgumentException("id required")}",key,ct).ConfigureAwait(false),
            "video_search"  =>await PxGet($"videos/search?query={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&per_page={GetParam<int?>(p,"per_page")??10}",key,ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: search|curated|get_photo|video_search")
        };
    }
    private static async Task<ToolkitResult> PxGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.pexels.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue(key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-148 — Giphy (api_key required)
[Toolkit("giphy","Giphy",ToolkitCategory.GamingCreative)]
public sealed class GiphyToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"giphy";public override string Name=>"Giphy";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("Giphy api_key not set");
        var limit=GetParam<int?>(p,"limit")??10;
        return a switch{
            "search"   =>await GpGet($"search?q={Uri.EscapeDataString(GetParam<string>(p,"query")?? throw new ArgumentException("query required"))}&limit={limit}&api_key={key}",ct).ConfigureAwait(false),
            "trending" =>await GpGet($"trending?limit={limit}&api_key={key}",ct).ConfigureAwait(false),
            "random"   =>await GpGet($"random?api_key={key}&tag={Uri.EscapeDataString(GetParam<string>(p,"tag")??"funny")}",ct).ConfigureAwait(false),
            "get"      =>await GpGet($"{GetParam<string>(p,"id")?? throw new ArgumentException("id required")}?api_key={key}",ct).ConfigureAwait(false),
            _=>ToolkitResult.Fail($"Unknown action '{a}'. Valid: search|trending|random|get")
        };
    }
    private static async Task<ToolkitResult> GpGet(string path,CancellationToken ct)=>ToolkitResult.Success(await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.giphy.com/v1/gifs/{path}",ct).ConfigureAwait(false));
}

// T-149 — OpenAI Image Gen (alias for DALL-E — api_key required)
[Toolkit("openai_image","OpenAI Image Gen",ToolkitCategory.GamingCreative)]
public sealed class OpenAiImageGenToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"openai_image";public override string Name=>"OpenAI Image Gen";
    public override ToolkitCategory Category=>ToolkitCategory.GamingCreative;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key")??throw new InvalidOperationException("OpenAI api_key not set");
        var prompt=GetParam<string>(p,"prompt")??"A beautiful landscape";
        var model=GetParam<string>(p,"model")??GetSetting("model")??"dall-e-3";
        var size=GetParam<string>(p,"size")??"1024x1024";
        var quality=GetParam<string>(p,"quality")??"standard";
        var n=GetParam<int?>(p,"n")??1;
        var payload=new{model,prompt,size,quality,n};
        var req=new HttpRequestMessage(System.Net.Http.HttpMethod.Post,"https://api.openai.com/v1/images/generations"){Content=System.Net.Http.Json.JsonContent.Create(payload)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}
