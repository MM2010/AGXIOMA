// T-102..T-119 — Web & Social Toolkits
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.WebSocial;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http
{
    internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
}

// T-107 — Twitter/X (Bearer token required)
[Toolkit("twitter","Twitter/X",ToolkitCategory.WebSocial)]
public sealed class TwitterToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"twitter";public override string Name=>"Twitter/X";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("bearer_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("bearer_token");
        return a switch{
            "search"       =>await TwGet($"tweets/search/recent?query={Uri.EscapeDataString(GetParam<string>(p,"query")??"")}"+$"&max_results={GetParam<int?>(p,"max_results")??10}&tweet.fields=created_at,author_id,public_metrics",token,ct).ConfigureAwait(false),
            "get_tweet"    =>await TwGet($"tweets/{GetParam<string>(p,"id")}?tweet.fields=created_at,author_id,public_metrics",token,ct).ConfigureAwait(false),
            "get_user"     =>await TwGet($"users/by/username/{GetParam<string>(p,"username")}?user.fields=public_metrics,description",token,ct).ConfigureAwait(false),
            "user_timeline"=>await TwGet($"users/{GetParam<string>(p,"user_id")}/tweets?max_results={GetParam<int?>(p,"max_results")??10}&tweet.fields=created_at,public_metrics",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> TwGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.twitter.com/2/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-108 — Reddit (public JSON API — no auth needed for public subs)
[Toolkit("reddit","Reddit",ToolkitCategory.WebSocial)]
public sealed class RedditToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"reddit";public override string Name=>"Reddit";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var sub=GetParam<string>(p,"subreddit")??"programming";
        var sort=GetParam<string>(p,"sort")??"hot";
        var limit=GetParam<int?>(p,"limit")??25;
        return a switch{
            "posts"    =>await RedditGet($"r/{sub}/{sort}.json?limit={limit}",ct).ConfigureAwait(false),
            "search"   =>await RedditGet($"r/{sub}/search.json?q={Uri.EscapeDataString(GetParam<string>(p,"query")??"")}"+$"&sort={sort}&limit={limit}",ct).ConfigureAwait(false),
            "comments" =>await RedditGet($"r/{sub}/comments/{GetParam<string>(p,"post_id")}.json?limit={limit}",ct).ConfigureAwait(false),
            "info"     =>await RedditGet($"r/{sub}/about.json",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> RedditGet(string path,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://www.reddit.com/{path}");
        req.Headers.Add("User-Agent","AGXIOMA-Agent/1.0");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-109 — LinkedIn (requires OAuth2 access_token)
[Toolkit("linkedin","LinkedIn",ToolkitCategory.WebSocial)]
public sealed class LinkedInToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"linkedin";public override string Name=>"LinkedIn";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        return a switch{
            "get_profile"  =>await LiGet("v2/me",token,ct).ConfigureAwait(false),
            "get_email"    =>await LiGet("v2/emailAddress?q=members&projection=(elements*(handle~))",token,ct).ConfigureAwait(false),
            "get_network"  =>await LiGet("v2/connections?q=viewer&start=0&count=50",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> LiGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.linkedin.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-110 — Instagram (requires Meta Graph API access_token)
[Toolkit("instagram","Instagram",ToolkitCategory.WebSocial)]
public sealed class InstagramToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"instagram";public override string Name=>"Instagram";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        var userId=GetSetting("user_id")??"me";
        return a switch{
            "get_profile" =>await IgGet($"{userId}?fields=id,username,name,biography,followers_count,media_count",token,ct).ConfigureAwait(false),
            "list_media"  =>await IgGet($"{userId}/media?fields=id,caption,media_type,thumbnail_url,permalink,timestamp&limit={GetParam<int?>(p,"limit")??12}",token,ct).ConfigureAwait(false),
            "get_insights"=>await IgGet($"{userId}/insights?metric=impressions,reach,profile_views&period=day&since={GetRequiredParam<string>(p,"since")}&until={GetRequiredParam<string>(p,"until")}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> IgGet(string path,string token,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://graph.instagram.com/{path}&access_token={token}",ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-111 — YouTube Data API v3 (api_key required)
[Toolkit("youtube","YouTube",ToolkitCategory.WebSocial)]
public sealed class YouTubeToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"youtube";public override string Name=>"YouTube";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        return a switch{
            "search"       =>await YtGet($"search?part=snippet&q={Uri.EscapeDataString(GetParam<string>(p,"query")??"")}"+$"&type={GetParam<string>(p,"type")??"video"}&maxResults={GetParam<int?>(p,"max_results")??10}",key,ct).ConfigureAwait(false),
            "video_info"   =>await YtGet($"videos?part=snippet,statistics&id={GetParam<string>(p,"video_id")}",key,ct).ConfigureAwait(false),
            "channel_info" =>await YtGet($"channels?part=snippet,statistics&id={GetParam<string>(p,"channel_id")}",key,ct).ConfigureAwait(false),
            "playlist_items"=>await YtGet($"playlistItems?part=snippet&playlistId={GetParam<string>(p,"playlist_id")}&maxResults={GetParam<int?>(p,"max_results")??50}",key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> YtGet(string path,string key,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://www.googleapis.com/youtube/v3/{path}&key={key}",ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-112 — TikTok (requires TikTok for Developers access_token — informative stub)
[Toolkit("tiktok","TikTok",ToolkitCategory.WebSocial)]
public sealed class TikTokToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"tiktok";public override string Name=>"TikTok";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("TikTok requires TikTok for Developers approval and OAuth2. Use https://developers.tiktok.com/. Settings: access_token. API: https://open-api.tiktok.com/v2/"));
}

// T-113 — Medium (Integration Token or RSS-based reading — no official write API for free tier)
[Toolkit("medium","Medium",ToolkitCategory.WebSocial)]
public sealed class MediumToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"medium";public override string Name=>"Medium";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetSetting("integration_token");
        return a switch{
            "get_user"    when token!=null =>await MediumGet("me",token,ct).ConfigureAwait(false),
            "get_publications" when token!=null =>await MediumGet("me/publications",token,ct).ConfigureAwait(false),
            "feed"        =>await ReadRss($"https://medium.com/feed/{GetRequiredParam<string>(p,"username")}",ct).ConfigureAwait(false),
            "tag_feed"    =>await ReadRss($"https://medium.com/feed/tag/{GetRequiredParam<string>(p,"tag")}",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> MediumGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.medium.com/v1/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> ReadRss(string url,CancellationToken ct)=>await RssToolkit.FetchAndParseRss(url,Http.Client,ct).ConfigureAwait(false);
}

// T-114 — Dev.to (api_key optional — public read without key)
[Toolkit("devto","Dev.to",ToolkitCategory.WebSocial)]
public sealed class DevToToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"devto";public override string Name=>"Dev.to";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetSetting("api_key");
        return a switch{
            "list_articles"  =>await DtGet($"articles?per_page={GetParam<int?>(p,"per_page")??10}&page={GetParam<int?>(p,"page")??1}&tag={GetParam<string>(p,"tag")??""}&top={GetParam<string>(p,"top")??""}" ,key,ct).ConfigureAwait(false),
            "get_article"    =>await DtGet($"articles/{GetParam<string>(p,"id")}",key,ct).ConfigureAwait(false),
            "search_articles"=>await DtGet($"articles/search?q={Uri.EscapeDataString(GetParam<string>(p,"query")??"")}",key,ct).ConfigureAwait(false),
            "list_tags"      =>await DtGet("tags",key,ct).ConfigureAwait(false),
            "list_podcasts"  =>await DtGet("podcast_episodes",key,ct).ConfigureAwait(false),
            "get_user"       =>await DtGet($"users/{GetParam<string>(p,"username")??GetParam<string>(p,"id")??"me"}",key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> DtGet(string path,string? key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://dev.to/api/{path}");
        if(!string.IsNullOrWhiteSpace(key))req.Headers.Add("api-key",key);
        req.Headers.Add("User-Agent","AGXIOMA-Agent/1.0");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-115 — Hashnode (public GraphQL API)
[Toolkit("hashnode","Hashnode",ToolkitCategory.WebSocial)]
public sealed class HashnodeToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"hashnode";public override string Name=>"Hashnode";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var query=a switch{
            "list_posts"=>"{ feed(type: BEST, first: 10) { edges { node { id title brief publishedAt url } } } }",
            "get_post"  =>$"{{ post(id: \"{GetParam<string>(p,"id")}\") {{ id title content {{ markdown }} publishedAt author {{ name }} }} }}",
            _=>GetRequiredParam<string>(p,"query")
        };
        var token=GetSetting("token");
        var req=new HttpRequestMessage(HttpMethod.Post,"https://gql.hashnode.com"){Content=JsonContent.Create(new{query})};
        if(!string.IsNullOrWhiteSpace(token))req.Headers.Add("Authorization",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-116 — WordPress REST API (site_url required; username+app_password for write ops)
[Toolkit("wordpress","WordPress",ToolkitCategory.WebSocial)]
public sealed class WordPressToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"wordpress";public override string Name=>"WordPress";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("site_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var base_=(GetSetting("site_url")??"").TrimEnd('/')+"/wp-json/wp/v2";
        var user=GetSetting("username");var pass=GetSetting("app_password");
        string? creds=user!=null&&pass!=null?Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")):null;
        return a switch{
            "list_posts"    =>await WpGet($"{base_}/posts?per_page={GetParam<int?>(p,"per_page")??10}&page={GetParam<int?>(p,"page")??1}",creds,ct).ConfigureAwait(false),
            "get_post"      =>await WpGet($"{base_}/posts/{GetParam<string>(p,"id")}",creds,ct).ConfigureAwait(false),
            "list_pages"    =>await WpGet($"{base_}/pages?per_page={GetParam<int?>(p,"per_page")??10}",creds,ct).ConfigureAwait(false),
            "list_categories"=>await WpGet($"{base_}/categories",creds,ct).ConfigureAwait(false),
            "list_tags"     =>await WpGet($"{base_}/tags",creds,ct).ConfigureAwait(false),
            "search"        =>await WpGet($"{base_}/search?search={Uri.EscapeDataString(GetParam<string>(p,"query")??"")}",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> WpGet(string url,string? creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        if(creds!=null)req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-117 — Shopify Admin API (store + access_token required)
[Toolkit("shopify","Shopify",ToolkitCategory.WebSocial)]
public sealed class ShopifyToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"shopify";public override string Name=>"Shopify";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("store"))&&!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var store=(GetSetting("store")??"").TrimEnd('/');
        var token=GetRequiredSetting("access_token");
        var base_=$"https://{store}/admin/api/2024-01";
        return a switch{
            "list_products" =>await ShGet($"{base_}/products.json?limit={GetParam<int?>(p,"limit")??25}",token,ct).ConfigureAwait(false),
            "get_product"   =>await ShGet($"{base_}/products/{GetParam<string>(p,"id")}.json",token,ct).ConfigureAwait(false),
            "list_orders"   =>await ShGet($"{base_}/orders.json?limit={GetParam<int?>(p,"limit")??25}&status={GetParam<string>(p,"status")??"open"}",token,ct).ConfigureAwait(false),
            "get_order"     =>await ShGet($"{base_}/orders/{GetParam<string>(p,"id")}.json",token,ct).ConfigureAwait(false),
            "list_customers"=>await ShGet($"{base_}/customers.json?limit={GetParam<int?>(p,"limit")??25}",token,ct).ConfigureAwait(false),
            "shop_info"     =>await ShGet($"{base_}/shop.json",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ShGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Add("X-Shopify-Access-Token",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-118 — WooCommerce (site_url + consumer_key + consumer_secret)
[Toolkit("woocommerce","WooCommerce",ToolkitCategory.WebSocial)]
public sealed class WooCommerceToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"woocommerce";public override string Name=>"WooCommerce";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("site_url"))&&!string.IsNullOrWhiteSpace(GetSetting("consumer_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var site=(GetSetting("site_url")??"").TrimEnd('/');
        var ck=GetRequiredSetting("consumer_key");
        var cs=GetSetting("consumer_secret")??"";
        var creds=Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ck}:{cs}"));
        var base_=$"{site}/wp-json/wc/v3";
        return a switch{
            "list_products" =>await WcGet($"{base_}/products?per_page={GetParam<int?>(p,"per_page")??10}",creds,ct).ConfigureAwait(false),
            "get_product"   =>await WcGet($"{base_}/products/{GetParam<string>(p,"id")}",creds,ct).ConfigureAwait(false),
            "list_orders"   =>await WcGet($"{base_}/orders?per_page={GetParam<int?>(p,"per_page")??10}&status={GetParam<string>(p,"status")??"processing"}",creds,ct).ConfigureAwait(false),
            "list_customers"=>await WcGet($"{base_}/customers?per_page={GetParam<int?>(p,"per_page")??10}",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> WcGet(string url,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-119 — Squarespace (informative stub — very limited REST API)
[Toolkit("squarespace","Squarespace",ToolkitCategory.WebSocial)]
public sealed class SquarespaceToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"squarespace";public override string Name=>"Squarespace";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Squarespace API is limited. Use https://developers.squarespace.com/commerce-apis. Settings: api_key. Available: orders, inventory, fulfillments, profiles."));
}

// T-120 — Zapier (webhook trigger — POST to Catch Hook URL)
[Toolkit("zapier","Zapier",ToolkitCategory.WebSocial)]
public sealed class ZapierToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"zapier";public override string Name=>"Zapier";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("webhook_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var url=GetRequiredSetting("webhook_url");
        var payload=GetParam<object?>(p,"payload")??p.Where(kv=>kv.Key!="url").ToDictionary(kv=>kv.Key,kv=>kv.Value);
        var resp=await Http.Client.PostAsJsonAsync(url,payload,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(new{triggered=true,url,statusCode=(int)resp.StatusCode});
    }
}

// T-121 — IFTTT (applet webhook trigger)
[Toolkit("ifttt","IFTTT",ToolkitCategory.WebSocial)]
public sealed class IftttToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"ifttt";public override string Name=>"IFTTT";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("webhook_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("webhook_key");
        var ev=GetParam<string>(p,"event")??a;
        var body=new{value1=GetParam<string>(p,"value1"),value2=GetParam<string>(p,"value2"),value3=GetParam<string>(p,"value3")};
        var resp=await Http.Client.PostAsJsonAsync($"https://maker.ifttt.com/trigger/{ev}/with/key/{key}",body,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(new{triggered=true,event_=ev,statusCode=(int)resp.StatusCode});
    }
}

// T-122 — Make.com (Integromat — webhook trigger)
[Toolkit("makecom","Make.com",ToolkitCategory.WebSocial)]
public sealed class MakeComToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"makecom";public override string Name=>"Make.com";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("webhook_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var url=GetRequiredSetting("webhook_url");
        var payload=GetParam<object?>(p,"payload")??p.Where(kv=>kv.Key!="url").ToDictionary(kv=>kv.Key,kv=>kv.Value);
        var resp=await Http.Client.PostAsJsonAsync(url,payload,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(new{triggered=true,statusCode=(int)resp.StatusCode});
    }
}

// T-123 — Web Scraper (BCL HttpClient + basic HTML parsing without HtmlAgilityPack)
[Toolkit("web_scraper","Web Scraper",ToolkitCategory.WebSocial)]
public sealed class WebScraperToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"web_scraper";public override string Name=>"Web Scraper";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var url=GetRequiredParam<string>(p,"url");
        return a switch{
            "scrape"        =>await Scrape(url,ct).ConfigureAwait(false),
            "extract_text"  =>await ExtractText(url,ct).ConfigureAwait(false),
            "extract_links" =>await ExtractLinks(url,ct).ConfigureAwait(false),
            "extract_meta"  =>await ExtractMeta(url,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> Scrape(string url,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Add("User-Agent","Mozilla/5.0 (compatible; AGXIOMA/1.0)");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        var html=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{url,statusCode=(int)resp.StatusCode,contentType=resp.Content.Headers.ContentType?.MediaType,lengthBytes=html.Length,htmlSnippet=html.Length>2000?html[..2000]+"...":html});
    }
    private static async Task<ToolkitResult> ExtractText(string url,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Add("User-Agent","Mozilla/5.0");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        var html=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        // strip HTML tags via regex
        var text=System.Text.RegularExpressions.Regex.Replace(html,"<[^>]+>"," ");
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\s+"," ").Trim();
        return ToolkitResult.Success(new{url,text=text.Length>5000?text[..5000]+"...":text,length=text.Length});
    }
    private static async Task<ToolkitResult> ExtractLinks(string url,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Add("User-Agent","Mozilla/5.0");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        var html=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var matches=System.Text.RegularExpressions.Regex.Matches(html,@"href=[""']([^""'#]+)[""']",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var links=matches.Select(m=>m.Groups[1].Value).Where(h=>h.StartsWith("http")).Distinct().Take(100).ToArray();
        return ToolkitResult.Success(new{url,links,count=links.Length});
    }
    private static async Task<ToolkitResult> ExtractMeta(string url,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Add("User-Agent","Mozilla/5.0");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        var html=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var title=System.Text.RegularExpressions.Regex.Match(html,@"<title[^>]*>([^<]+)</title>",System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value.Trim();
        var desc=System.Text.RegularExpressions.Regex.Match(html,@"<meta[^>]+name=[""']description[""'][^>]+content=[""']([^""']+)[""']",System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value;
        var ogTitle=System.Text.RegularExpressions.Regex.Match(html,@"property=[""']og:title[""'][^>]+content=[""']([^""']+)[""']",System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value;
        return ToolkitResult.Success(new{url,title,description=desc,ogTitle,contentType=resp.Content.Headers.ContentType?.MediaType});
    }
}

// T-124 — RSS Feed (BCL XDocument — no extra packages needed)
[Toolkit("rss","RSS Feed",ToolkitCategory.WebSocial)]
public sealed class RssToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"rss";public override string Name=>"RSS Feed";
    public override ToolkitCategory Category=>ToolkitCategory.WebSocial;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var url=GetParam<string>(p,"url")??GetSetting("feed_url")?? throw AgxiomeToolkitException.MissingParameter(Id, "url");
        return a switch{
            "fetch" or "parse" =>await FetchAndParseRss(url,Http.Client,ct).ConfigureAwait(false),
            "search"           =>await SearchRss(url,GetParam<string>(p,"query")??"",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    internal static async Task<ToolkitResult> FetchAndParseRss(string url,HttpClient client,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);req.Headers.Add("User-Agent","AGXIOMA-Agent/1.0");
        var resp=await client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        var xml=await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        XDocument doc;try{doc=XDocument.Parse(xml);}catch(Exception){return ToolkitResult.Fail("Invalid XML/RSS content.");}
        var ns=doc.Root?.GetDefaultNamespace();
        // detect Atom vs RSS
        if(doc.Root?.Name.LocalName=="feed"){
            // Atom
            XNamespace atom="http://www.w3.org/2005/Atom";
            var items=doc.Descendants(atom+"entry").Take(50).Select(e=>new{
                title=e.Element(atom+"title")?.Value,
                link=e.Element(atom+"link")?.Attribute("href")?.Value,
                summary=e.Element(atom+"summary")?.Value??e.Element(atom+"content")?.Value,
                published=e.Element(atom+"published")?.Value??e.Element(atom+"updated")?.Value
            }).ToArray();
            return ToolkitResult.Success(new{type="atom",url,feedTitle=doc.Root.Element(atom+"title")?.Value,count=items.Length,items});
        }else{
            // RSS 2.0
            var channel=doc.Root?.Element("channel");
            var items=channel?.Elements("item").Take(50).Select(e=>new{
                title=e.Element("title")?.Value,
                link=e.Element("link")?.Value,
                description=e.Element("description")?.Value,
                pubDate=e.Element("pubDate")?.Value
            }).ToArray()??[];
            return ToolkitResult.Success(new{type="rss",url,feedTitle=channel?.Element("title")?.Value,feedDescription=channel?.Element("description")?.Value,count=items.Length,items});
        }
    }
    private async Task<ToolkitResult> SearchRss(string url,string query,CancellationToken ct){
        var result=await FetchAndParseRss(url,Http.Client,ct).ConfigureAwait(false);
        if(result.Status!=ToolkitResultStatus.Ok)return result;
        // Filter by query in title/description
        var data=JsonDocument.Parse(JsonSerializer.Serialize(result.Data)).RootElement;
        return ToolkitResult.Success(new{query,url,filtered=true,hint="Full-text filtering not applied at toolkit level — consume items and filter client-side",items=data});
    }
}
