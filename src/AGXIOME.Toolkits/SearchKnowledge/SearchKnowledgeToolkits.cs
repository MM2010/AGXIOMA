// T-120..T-136 — Search & Knowledge Toolkits
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.SearchKnowledge;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http
{
    internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
}

// T-120 — Google Custom Search (requires cx + api_key)
[Toolkit("google_search","Google Search",ToolkitCategory.SearchKnowledge)]
public sealed class GoogleSearchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"google_search";public override string Name=>"Google Search";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"))&&!string.IsNullOrWhiteSpace(GetSetting("cx"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var cx=GetRequiredSetting("cx");
        var query=HttpUtility.UrlEncode(GetParam<string>(p,"query")??"");
        var num=GetParam<int?>(p,"num")??10;
        return a switch{
            "search" or "web_search" => await GoogleSearch($"https://www.googleapis.com/customsearch/v1?key={key}&cx={cx}&q={query}&num={num}",query,ct).ConfigureAwait(false),
            "image_search"           => await GoogleSearch($"https://www.googleapis.com/customsearch/v1?key={key}&cx={cx}&q={query}&num={num}&searchType=image",query,ct).ConfigureAwait(false),
            "news_search"            => await GoogleSearch($"https://www.googleapis.com/customsearch/v1?key={key}&cx={cx}&q={query}&num={num}&tbm=nws",query,ct).ConfigureAwait(false),
            "video_search"           => await GoogleSearch($"https://www.googleapis.com/customsearch/v1?key={key}&cx={cx}&q={query}&num={num}&tbm=vid",query,ct).ConfigureAwait(false),
            "suggest"                => await GsGet($"https://suggestqueries.google.com/complete/search?client=firefox&q={query}",ct).ConfigureAwait(false),
            _                        => throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> GoogleSearch(string url,string rawQuery,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(url,ct).ConfigureAwait(false);
        var items=json.TryGetProperty("items",out var el)?el.EnumerateArray().Select(i=>new{
            title=i.TryGetProperty("title",out var t)?t.GetString():"",
            link=i.TryGetProperty("link",out var l)?l.GetString():"",
            snippet=i.TryGetProperty("snippet",out var s)?s.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{query=rawQuery,results=items,totalResults=items.Length});
    }
    private static async Task<ToolkitResult> GsGet(string url,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(url,ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-121 — Bing Web Search (requires api_key / Ocp-Apim-Subscription-Key)
[Toolkit("bing_search","Bing Search",ToolkitCategory.SearchKnowledge)]
public sealed class BingSearchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"bing_search";public override string Name=>"Bing Search";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=HttpUtility.UrlEncode(GetParam<string>(p,"query")??"");
        var count=GetParam<int?>(p,"count")??10;
        return a switch{
            "search" or "web_search" => await BingSearch($"search?q={query}&count={count}",key,"webPages",ct).ConfigureAwait(false),
            "news_search"            => await BingSearch($"news/search?q={query}&count={count}",key,"value",ct).ConfigureAwait(false),
            "image_search"           => await BingSearch($"images/search?q={query}&count={count}",key,"value",ct).ConfigureAwait(false),
            "video_search"           => await BingSearch($"videos/search?q={query}&count={count}",key,"value",ct).ConfigureAwait(false),
            "suggest"                => await BingSearch($"suggestions?q={query}",key,"suggestionGroups",ct).ConfigureAwait(false),
            _                        => throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> BingSearch(string path,string key,string root,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.bing.microsoft.com/v7.0/{path}");
        req.Headers.Add("Ocp-Apim-Subscription-Key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-122 — SearXNG (self-hosted, base_url required)
[Toolkit("searxng","SearXNG",ToolkitCategory.SearchKnowledge)]
public sealed class SearXngToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"searxng";public override string Name=>"SearXNG";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("base_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var base_=GetRequiredSetting("base_url").TrimEnd('/');
        var query=HttpUtility.UrlEncode(GetParam<string>(p,"query")??"");
        var lang=GetParam<string>(p,"language")??""; var safesearch=GetParam<string>(p,"safesearch")??"0";
        return a switch{
            "search" or "web_search" => await SxSearch($"{base_}/search?q={query}&format=json&categories=general&language={lang}",query,ct).ConfigureAwait(false),
            "image_search"           => await SxSearch($"{base_}/search?q={query}&format=json&categories=images&language={lang}",query,ct).ConfigureAwait(false),
            "news_search"            => await SxSearch($"{base_}/search?q={query}&format=json&categories=news&language={lang}",query,ct).ConfigureAwait(false),
            "video_search"           => await SxSearch($"{base_}/search?q={query}&format=json&categories=videos&language={lang}",query,ct).ConfigureAwait(false),
            "file_search"            => await SxSearch($"{base_}/search?q={query}&format=json&categories=files&language={lang}",query,ct).ConfigureAwait(false),
            _                        => throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> SxSearch(string url,string query,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(url,ct).ConfigureAwait(false);
        var results=json.TryGetProperty("results",out var r)?r.EnumerateArray().Select(i=>new{
            title=i.TryGetProperty("title",out var t)?t.GetString():"",
            url=i.TryGetProperty("url",out var u)?u.GetString():"",
            content=i.TryGetProperty("content",out var c)?c.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{query,results});
    }
}

// T-123 — Tavily AI Search (api_key required)
[Toolkit("tavily","Tavily",ToolkitCategory.SearchKnowledge)]
public sealed class TavilyToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"tavily";public override string Name=>"Tavily";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=GetParam<string>(p,"query")??"";
        var depth=GetParam<string>(p,"search_depth")??"basic";
        var maxResults=GetParam<int?>(p,"max_results")??5;
        var includeAnswer=GetParam<bool?>(p,"include_answer")??true;
        var body=new{api_key=key,query,search_depth=depth,max_results=maxResults,include_answer=includeAnswer};
        var resp=await Http.Client.PostAsJsonAsync("https://api.tavily.com/search",body,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var answer=json.TryGetProperty("answer",out var ans)?ans.GetString():null;
        var results=json.TryGetProperty("results",out var r)?r.EnumerateArray().Select(i=>new{
            title=i.TryGetProperty("title",out var t)?t.GetString():"",
            url=i.TryGetProperty("url",out var u)?u.GetString():"",
            content=i.TryGetProperty("content",out var c)?c.GetString():"",
            score=i.TryGetProperty("score",out var s)?s.GetDouble():0.0}).ToArray():[];
        return ToolkitResult.Success(new{query,answer,results});
    }
}

// T-124 — Exa Neural Search (api_key required)
[Toolkit("exa","Exa",ToolkitCategory.SearchKnowledge)]
public sealed class ExaToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"exa";public override string Name=>"Exa";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=GetParam<string>(p,"query")??""; var numResults=GetParam<int?>(p,"num_results")??5;
        var useAutoprompt=GetParam<bool?>(p,"use_autoprompt")??true;
        return a switch{
            "search" or "web_search" => await ExaPost(key,"search",new{query,numResults,useAutoprompt,type=GetParam<string>(p,"type")??"auto"},ct).ConfigureAwait(false),
            "neural_search"          => await ExaPost(key,"search",new{query,numResults,useAutoprompt=true,type="neural"},ct).ConfigureAwait(false),
            "get_contents"           => await ExaPost(key,"contents",new{ids=GetParam<string[]?>(p,"ids")??Array.Empty<string>(),text=new{maxCharacters=GetParam<int?>(p,"max_chars")??1000}},ct).ConfigureAwait(false),
            "find_similar"           => await ExaPost(key,"findSimilar",new{url=GetRequiredParam<string>(p,"url"),numResults,excludeSourceDomain=GetParam<bool?>(p,"exclude_source")??true},ct).ConfigureAwait(false),
            "search_and_contents"    => await ExaPost(key,"search",new{query,numResults,useAutoprompt,type=GetParam<string>(p,"type")??"auto",contents=new{text=new{maxCharacters=500}}},ct).ConfigureAwait(false),
            _                        => throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ExaPost(string key,string endpoint,object body,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.exa.ai/{endpoint}"){Content=JsonContent.Create(body)};
        req.Headers.Add("x-api-key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-125 — Brave Search (api_key required)
[Toolkit("brave_search","Brave Search",ToolkitCategory.SearchKnowledge)]
public sealed class BraveSearchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"brave_search";public override string Name=>"Brave Search";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=HttpUtility.UrlEncode(GetParam<string>(p,"query")??"");
        var count=GetParam<int?>(p,"count")??10;
        return a switch{
            "search" or "web_search" => await BraveGet($"web/search?q={query}&count={count}",key,ct).ConfigureAwait(false),
            "news_search"            => await BraveGet($"news/search?q={query}&count={count}",key,ct).ConfigureAwait(false),
            "image_search"           => await BraveGet($"images/search?q={query}&count={count}",key,ct).ConfigureAwait(false),
            "video_search"           => await BraveGet($"videos/search?q={query}&count={count}",key,ct).ConfigureAwait(false),
            "suggest"                => await BraveGet($"suggest/search?q={query}&count={count}",key,ct).ConfigureAwait(false),
            _                        => throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> BraveGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.search.brave.com/res/v1/{path}");
        req.Headers.Add("Accept","application/json");
        req.Headers.Add("Accept-Encoding","gzip");
        req.Headers.Add("X-Subscription-Token",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-126 — SerpAPI (api_key required)
[Toolkit("serpapi","SerpAPI",ToolkitCategory.SearchKnowledge)]
public sealed class SerpApiToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"serpapi";public override string Name=>"SerpAPI";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var q=HttpUtility.UrlEncode(GetParam<string>(p,"q")??"");
        var num=GetParam<int?>(p,"num")??10;
        return a switch{
            "search" or "google"  => await SerpGet($"engine=google&q={q}&num={num}&api_key={key}",ct).ConfigureAwait(false),
            "news_search"         => await SerpGet($"engine=google_news&q={q}&api_key={key}",ct).ConfigureAwait(false),
            "image_search"        => await SerpGet($"engine=google_images&q={q}&num={num}&api_key={key}",ct).ConfigureAwait(false),
            "scholar_search"      => await SerpGet($"engine=google_scholar&q={q}&num={num}&api_key={key}",ct).ConfigureAwait(false),
            "jobs_search"         => await SerpGet($"engine=google_jobs&q={q}&api_key={key}",ct).ConfigureAwait(false),
            _                     => await SerpGet($"engine={a}&q={q}&num={num}&api_key={key}",ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> SerpGet(string qs,CancellationToken ct){
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://serpapi.com/search.json?{qs}",ct).ConfigureAwait(false);
        return ToolkitResult.Success(json);
    }
}

// T-127 — Serper.dev (api_key required)
[Toolkit("serper","Serper",ToolkitCategory.SearchKnowledge)]
public sealed class SerperToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"serper";public override string Name=>"Serper";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=GetParam<string>(p,"query")??""; var num=GetParam<int?>(p,"num")??10;
        var endpoint=a switch{
            "search" or "web_search" =>"search",
            "news_search"            =>"news",
            "image_search"           =>"images",
            "scholar_search"         =>"scholar",
            "places_search"          =>"places",
            _                        =>a
        };
        var body=new{q=query,num};
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://google.serper.dev/{endpoint}"){
            Content=JsonContent.Create(body)
        };
        req.Headers.Add("X-API-KEY",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var results=json.TryGetProperty("organic",out var r)
            ?r.EnumerateArray().Select(i=>new{
                title=i.TryGetProperty("title",out var t)?t.GetString():"",
                link=i.TryGetProperty("link",out var l)?l.GetString():"",
                snippet=i.TryGetProperty("snippet",out var s)?s.GetString():""}).ToArray():[];
        var answerBox=json.TryGetProperty("answerBox",out var ab)?ab.GetRawText():null;
        return ToolkitResult.Success(new{query,endpoint,results,answerBox});
    }
}

// T-128 — Firecrawl (api_key required — scrape/crawl to markdown)
[Toolkit("firecrawl","Firecrawl",ToolkitCategory.SearchKnowledge)]
public sealed class FirecrawlToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"firecrawl";public override string Name=>"Firecrawl";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var url=GetRequiredParam<string>(p,"url");
        var endpoint=a switch{"crawl"=>"crawl","map"=>"map",_=>"scrape"};
        var formats=GetParam<string[]?>(p,"formats")??["markdown"];
        object body=endpoint=="scrape"?new{url,formats}:new{url};
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.firecrawl.dev/v1/{endpoint}"){
            Content=JsonContent.Create(body)
        };
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var markdown=json.TryGetProperty("data",out var d)&&d.TryGetProperty("markdown",out var md)?md.GetString():json.GetRawText();
        var title=json.TryGetProperty("data",out var d2)&&d2.TryGetProperty("metadata",out var meta)&&meta.TryGetProperty("title",out var ti)?ti.GetString():"";
        return ToolkitResult.Success(new{url,title,markdown,charCount=markdown?.Length??0});
    }
}

// T-129 — Wikipedia (free, no key needed)
[Toolkit("wikipedia","Wikipedia",ToolkitCategory.SearchKnowledge)]
public sealed class WikipediaToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"wikipedia";public override string Name=>"Wikipedia";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var lang=GetParam<string>(p,"lang")??"en";
        return a switch{
            "search"=>await WikiSearch(GetParam<string>(p,"query")??"",lang,GetParam<int?>(p,"limit")??5,ct).ConfigureAwait(false),
            "summary"=>await WikiSummary(GetParam<string>(p,"title")??"",lang,ct).ConfigureAwait(false),
            "sections"=>await WikiSections(GetParam<string>(p,"title")??"",lang,ct).ConfigureAwait(false),
            _=>await WikiSummary(GetParam<string>(p,"title")??GetParam<string>(p,"query")??"",lang,ct).ConfigureAwait(false)
        };
    }
    private async Task<ToolkitResult> WikiSearch(string query,string lang,int limit,CancellationToken ct){
        var q=HttpUtility.UrlEncode(query);
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://{lang}.wikipedia.org/w/api.php?action=search&list=search&srsearch={q}&srlimit={limit}&format=json",ct).ConfigureAwait(false);
        var results=json.TryGetProperty("query",out var qn)&&qn.TryGetProperty("search",out var s)
            ?s.EnumerateArray().Select(i=>new{
                title=i.TryGetProperty("title",out var t)?t.GetString():"",
                snippet=Regex.Replace(i.TryGetProperty("snippet",out var sn)?sn.GetString()??"":" ","<.*?>",""),
                wordcount=i.TryGetProperty("wordcount",out var wc)?wc.GetInt32():0}).ToArray():[];
        return ToolkitResult.Success(new{query,lang,results});
    }
    private async Task<ToolkitResult> WikiSummary(string title,string lang,CancellationToken ct){
        var t=HttpUtility.UrlEncode(title);
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{t}",ct).ConfigureAwait(false);
        var extract=json.TryGetProperty("extract",out var e)?e.GetString():"";
        var pageTitle=json.TryGetProperty("title",out var ti)?ti.GetString():"";
        var pageUrl=json.TryGetProperty("content_urls",out var cu)&&cu.TryGetProperty("desktop",out var dt)&&dt.TryGetProperty("page",out var pg)?pg.GetString():"";
        return ToolkitResult.Success(new{title=pageTitle,summary=extract,url=pageUrl,charCount=extract?.Length??0});
    }
    private async Task<ToolkitResult> WikiSections(string title,string lang,CancellationToken ct){
        var t=HttpUtility.UrlEncode(title);
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://{lang}.wikipedia.org/api/rest_v1/page/sections/{t}",ct).ConfigureAwait(false);
        var sections=json.TryGetProperty("sections",out var s)
            ?s.EnumerateArray().Select(i=>i.TryGetProperty("title",out var h)?h.GetString():"").ToArray():[];
        return ToolkitResult.Success(new{title,sections});
    }
}

// T-130 — arXiv (free, no key needed)
[Toolkit("arxiv","arXiv",ToolkitCategory.SearchKnowledge)]
public sealed class ArxivToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"arxiv";public override string Name=>"arXiv";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        return a switch{
            "get_paper"=>await GetPaper(GetParam<string>(p,"id")??"",ct).ConfigureAwait(false),
            _=>await Search(GetParam<string>(p,"query")??"",GetParam<int?>(p,"max_results")??10,GetParam<string>(p,"sort_by")??"relevance",ct).ConfigureAwait(false)
        };
    }
    private async Task<ToolkitResult> Search(string query,int maxResults,string sortBy,CancellationToken ct){
        var q=HttpUtility.UrlEncode(query);
        var sort=sortBy switch{"date"=>"submittedDate","relevance"=>"relevance",_=>"relevance"};
        var xml=await Http.Client.GetStringAsync(
            $"https://export.arxiv.org/api/query?search_query=all:{q}&max_results={maxResults}&sortBy={sort}&sortOrder=descending",ct).ConfigureAwait(false);
        var doc=XDocument.Parse(xml);
        XNamespace atom="http://www.w3.org/2005/Atom";
        var papers=doc.Descendants(atom+"entry").Select(e=>new{
            id=e.Element(atom+"id")?.Value?.Split('/').Last()??"",
            title=e.Element(atom+"title")?.Value?.Trim()??"",
            summary=e.Element(atom+"summary")?.Value?.Trim()??"",
            published=e.Element(atom+"published")?.Value??"",
            authors=e.Elements(atom+"author").Select(a=>a.Element(atom+"name")?.Value??"").ToArray(),
            link=e.Elements(atom+"link").FirstOrDefault(l=>l.Attribute("rel")?.Value=="alternate")?.Attribute("href")?.Value??""
        }).ToArray();
        return ToolkitResult.Success(new{query,papers,count=papers.Length});
    }
    private async Task<ToolkitResult> GetPaper(string id,CancellationToken ct){
        var xml=await Http.Client.GetStringAsync($"https://export.arxiv.org/api/query?id_list={id}",ct).ConfigureAwait(false);
        var doc=XDocument.Parse(xml);
        XNamespace atom="http://www.w3.org/2005/Atom";
        var e=doc.Descendants(atom+"entry").FirstOrDefault();
        if(e==null)return ToolkitResult.Fail($"Paper {id} not found");
        return ToolkitResult.Success(new{
            id,
            title=e.Element(atom+"title")?.Value?.Trim()??"",
            summary=e.Element(atom+"summary")?.Value?.Trim()??"",
            published=e.Element(atom+"published")?.Value??"",
            authors=e.Elements(atom+"author").Select(a=>a.Element(atom+"name")?.Value??"").ToArray(),
            categories=e.Elements(XNamespace.Get("http://arxiv.org/schemas/atom")+"primary_category")
                .Select(c=>c.Attribute("term")?.Value??"").ToArray()
        });
    }
}

// T-131 — PubMed (free NCBI API, no key needed for basic use)
[Toolkit("pubmed","PubMed",ToolkitCategory.SearchKnowledge)]
public sealed class PubMedToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"pubmed";public override string Name=>"PubMed";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        return a switch{
            "fetch"=>await FetchArticle(GetParam<string>(p,"pmid")??"",ct).ConfigureAwait(false),
            _=>await Search(GetParam<string>(p,"query")??"",GetParam<int?>(p,"retmax")??10,ct).ConfigureAwait(false)
        };
    }
    private async Task<ToolkitResult> Search(string query,int retmax,CancellationToken ct){
        var q=HttpUtility.UrlEncode(query);
        var apiKey=GetSetting("api_key") is string k?$"&api_key={k}":"";
        // Step 1: esearch to get IDs
        var searchJson=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?db=pubmed&term={q}&retmax={retmax}&retmode=json{apiKey}",ct).ConfigureAwait(false);
        var ids=searchJson.TryGetProperty("esearchresult",out var r)&&r.TryGetProperty("idlist",out var il)
            ?il.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];
        if(ids.Length==0)return ToolkitResult.Success(new{query,articles=Array.Empty<object>(),count=0});
        // Step 2: efetch to get summaries
        var idList=string.Join(",",ids);
        var summaryJson=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esummary.fcgi?db=pubmed&id={idList}&retmode=json{apiKey}",ct).ConfigureAwait(false);
        var result=summaryJson.TryGetProperty("result",out var res)?res:default;
        var articles=ids.Select(id=>{
            if(result.ValueKind==JsonValueKind.Object&&result.TryGetProperty(id,out var art))
                return new{pmid=id,title=art.TryGetProperty("title",out var t)?t.GetString()??"":"",
                    source=art.TryGetProperty("source",out var s)?s.GetString()??"":"",
                    pubdate=art.TryGetProperty("pubdate",out var d)?d.GetString()??"":"",
                    authors=art.TryGetProperty("authors",out var au)?au.EnumerateArray()
                        .Select(a=>a.TryGetProperty("name",out var n)?n.GetString()??"":"").Take(5).ToArray():[]};
            return new{pmid=id,title="",source="",pubdate="",authors=Array.Empty<string>()};
        }).ToArray();
        return ToolkitResult.Success(new{query,articles,count=articles.Length});
    }
    private async Task<ToolkitResult> FetchArticle(string pmid,CancellationToken ct){
        var apiKey=GetSetting("api_key") is string k?$"&api_key={k}":"";
        var xml=await Http.Client.GetStringAsync(
            $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&id={pmid}&rettype=abstract&retmode=text{apiKey}",ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{pmid,abstract_text=xml.Trim(),charCount=xml.Length});
    }
}

// T-132 — DuckDuckGo Instant Answer (free, no API key required)
[Toolkit("duckduckgo","DuckDuckGo",ToolkitCategory.SearchKnowledge)]
public sealed class DuckDuckGoToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"duckduckgo";public override string Name=>"DuckDuckGo";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>true;
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var query=GetParam<string>(p,"query")??"";
        var q=HttpUtility.UrlEncode(query);
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://api.duckduckgo.com/?q={q}&format=json&no_redirect=1&no_html=1&skip_disambig=1",ct).ConfigureAwait(false);
        var answer=json.TryGetProperty("AbstractText",out var at)?at.GetString():"";
        var definition=json.TryGetProperty("Definition",out var def)?def.GetString():"";
        var entity=json.TryGetProperty("Heading",out var h)?h.GetString():"";
        var related=json.TryGetProperty("RelatedTopics",out var rt)
            ?rt.EnumerateArray().Take(10).Select(t=>new{
                text=t.TryGetProperty("Text",out var tx)?tx.GetString():"",
                url=t.TryGetProperty("FirstURL",out var u)?u.GetString():""}).ToArray():[];
        return a switch{
            "related"    =>ToolkitResult.Success(new{query,entity,related}),
            "definition" =>ToolkitResult.Success(new{query,entity,definition}),
            _            =>ToolkitResult.Success(new{query,entity,answer,definition,related})
        };
    }
}

// T-133 — You.com AI Search (api_key required)
[Toolkit("youcom","You.com",ToolkitCategory.SearchKnowledge)]
public sealed class YouComToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"youcom";public override string Name=>"You.com";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=GetParam<string>(p,"query")??"";
        var num=GetParam<int?>(p,"num")??10;
        var searchType=a switch{
            "news"        =>"news",
            "code"        =>"code",
            "ai_snippets" =>"snippets",
            _             =>"web"
        };
        var req=new HttpRequestMessage(HttpMethod.Get,
            $"https://api.you.com/v2/search?q={HttpUtility.UrlEncode(query)}&num={num}&search_type={searchType}");
        req.Headers.Add("X-API-Key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        return ToolkitResult.Success(new{query,searchType,results=json});
    }
}

// T-134 — Kagi Search (api_key required)
[Toolkit("kagi","Kagi",ToolkitCategory.SearchKnowledge)]
public sealed class KagiToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"kagi";public override string Name=>"Kagi";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        return a switch{
            "summarize"=>await KagiReq(HttpMethod.Post,"https://kagi.com/api/v0/summarize",key,
                new{url=GetParam<string>(p,"url")??"",engine=GetParam<string>(p,"engine")??"agnes"},ct).ConfigureAwait(false),
            "fastgpt"  =>await KagiReq(HttpMethod.Post,"https://kagi.com/api/v0/fastgpt",key,
                new{query=GetParam<string>(p,"query")??"",cache=true,web_search=true},ct).ConfigureAwait(false),
            _          =>await KagiReq(HttpMethod.Get,
                $"https://kagi.com/api/v0/search?q={HttpUtility.UrlEncode(GetParam<string>(p,"query")??"")}" +
                $"&limit={GetParam<int?>(p,"limit")??10}",key,null,ct).ConfigureAwait(false)
        };
    }
    private static async Task<ToolkitResult> KagiReq(HttpMethod method,string url,string key,object? body,CancellationToken ct){
        var req=new HttpRequestMessage(method,url);
        req.Headers.Add("Authorization",$"Bot {key}");
        if(body is not null)req.Content=JsonContent.Create(body);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static Task<ToolkitResult> KagiReq(HttpMethod m,string url,string key,CancellationToken ct)
        =>KagiReq(m,url,key,null,ct);
}

// T-135 — Perplexity Search (api_key required — chat completions format with citations)
[Toolkit("perplexity","Perplexity",ToolkitCategory.SearchKnowledge)]
public sealed class PerplexityToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"perplexity";public override string Name=>"Perplexity";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=GetParam<string>(p,"query")??"";
        var model=a switch{
            "deep_search" =>"sonar-pro",
            "research"    =>"sonar-deep-research",
            "reasoning"   =>"sonar-reasoning-pro",
            _             =>GetParam<string>(p,"model")??"sonar"
        };
        var body=new{model,messages=new[]{new{role="user",content=query}},
            return_citations=true,return_related_questions=true};
        var req=new HttpRequestMessage(HttpMethod.Post,"https://api.perplexity.ai/chat/completions"){
            Content=JsonContent.Create(body)
        };
        req.Headers.Add("Authorization",$"Bearer {key}");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var content=json.TryGetProperty("choices",out var ch)&&ch.GetArrayLength()>0
            &&ch[0].TryGetProperty("message",out var msg)&&msg.TryGetProperty("content",out var c)?c.GetString():"";
        var citations=json.TryGetProperty("citations",out var cit)
            ?cit.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];
        var relatedQuestions=json.TryGetProperty("related_questions",out var rq)
            ?rq.EnumerateArray().Select(x=>x.GetString()??"").ToArray():[];
        return ToolkitResult.Success(new{query,model,answer=content,citations,relatedQuestions});
    }
}

// T-136 — GNews (api_key required — dedicated news search across 60 000+ sources)
[Toolkit("gnews","GNews",ToolkitCategory.SearchKnowledge)]
public sealed class GNewsToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"gnews";public override string Name=>"GNews";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var lang=GetParam<string>(p,"lang")??"en";
        var country=GetParam<string>(p,"country")??"us";
        var max=GetParam<int?>(p,"max")??10;
        var endpoint=a switch{
            "top_headlines"=>$"/v4/top-headlines?category={GetParam<string>(p,"category")??"general"}&lang={lang}&country={country}&max={max}&apikey={key}",
            "topic"        =>$"/v4/top-headlines?topic={HttpUtility.UrlEncode(GetParam<string>(p,"topic")??"breaking-news")}&lang={lang}&max={max}&apikey={key}",
            _              =>$"/v4/search?q={HttpUtility.UrlEncode(GetParam<string>(p,"query")??"")} &lang={lang}&country={country}&max={max}&apikey={key}"
        };
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"https://gnews.io{endpoint}",ct).ConfigureAwait(false);
        var articles=json.TryGetProperty("articles",out var arts)
            ?arts.EnumerateArray().Select(art=>new{
                title=art.TryGetProperty("title",out var t)?t.GetString():"",
                description=art.TryGetProperty("description",out var d)?d.GetString():"",
                url=art.TryGetProperty("url",out var u)?u.GetString():"",
                publishedAt=art.TryGetProperty("publishedAt",out var pub)?pub.GetString():"",
                source=art.TryGetProperty("source",out var src)&&src.TryGetProperty("name",out var sn)?sn.GetString():""
            }).ToArray():[];
        var total=json.TryGetProperty("totalArticles",out var tot)?tot.GetInt32():0;
        return ToolkitResult.Success(new{query=GetParam<string>(p,"query")??a,articles,total,lang,country});
    }
}
