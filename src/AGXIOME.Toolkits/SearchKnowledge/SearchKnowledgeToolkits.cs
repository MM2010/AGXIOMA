// T-120..T-131 — Search & Knowledge Toolkits
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
/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class GoogleSearchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"google_search";public override string Name=>"Google Search";
    public override ToolkitCategory Category=>ToolkitCategory.SearchKnowledge;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"))&&!string.IsNullOrWhiteSpace(GetSetting("cx"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var cx=GetRequiredSetting("cx");
        var query=HttpUtility.UrlEncode(GetParam<string>(p,"query")??"");
        var num=GetParam<int?>(p,"num")??10;
        var url=$"https://www.googleapis.com/customsearch/v1?key={key}&cx={cx}&q={query}&num={num}";
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(url,ct).ConfigureAwait(false);
        var items=json.TryGetProperty("items",out var el)?el.EnumerateArray().Select(i=>new{
            title=i.TryGetProperty("title",out var t)?t.GetString():"",
            link=i.TryGetProperty("link",out var l)?l.GetString():"",
            snippet=i.TryGetProperty("snippet",out var s)?s.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{query,results=items,totalResults=items.Length});
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
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.bing.microsoft.com/v7.0/search?q={query}&count={count}");
        req.Headers.Add("Ocp-Apim-Subscription-Key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var items=json.TryGetProperty("webPages",out var wp)&&wp.TryGetProperty("value",out var vals)
            ?vals.EnumerateArray().Select(i=>new{
                name=i.TryGetProperty("name",out var n)?n.GetString():"",
                url=i.TryGetProperty("url",out var u)?u.GetString():"",
                snippet=i.TryGetProperty("snippet",out var s)?s.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{query,results=items});
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
        var format=GetParam<string>(p,"format")??"json";
        var json=await Http.Client.GetFromJsonAsync<JsonElement>($"{base_}/search?q={query}&format={format}",ct).ConfigureAwait(false);
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
        var query=GetParam<string>(p,"query")??"";
        var numResults=GetParam<int?>(p,"num_results")??5;
        var useAutoprompt=GetParam<bool?>(p,"use_autoprompt")??true;
        var body=new{query,numResults,useAutoprompt,type=GetParam<string>(p,"type")??"auto"};
        var req=new HttpRequestMessage(HttpMethod.Post,"https://api.exa.ai/search"){
            Content=JsonContent.Create(body)
        };
        req.Headers.Add("x-api-key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var results=json.TryGetProperty("results",out var r)?r.EnumerateArray().Select(i=>new{
            title=i.TryGetProperty("title",out var t)?t.GetString():"",
            url=i.TryGetProperty("url",out var u)?u.GetString():"",
            publishedDate=i.TryGetProperty("publishedDate",out var d)?d.GetString():"",
            author=i.TryGetProperty("author",out var au)?au.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{query,results});
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
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.search.brave.com/res/v1/web/search?q={query}&count={count}");
        req.Headers.Add("Accept","application/json");
        req.Headers.Add("Accept-Encoding","gzip");
        req.Headers.Add("X-Subscription-Token",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json=await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
        var results=json.TryGetProperty("web",out var web)&&web.TryGetProperty("results",out var r)
            ?r.EnumerateArray().Select(i=>new{
                title=i.TryGetProperty("title",out var t)?t.GetString():"",
                url=i.TryGetProperty("url",out var u)?u.GetString():"",
                description=i.TryGetProperty("description",out var d)?d.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{query,results});
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
        var engine=GetParam<string>(p,"engine")??"google";
        var num=GetParam<int?>(p,"num")??10;
        var json=await Http.Client.GetFromJsonAsync<JsonElement>(
            $"https://serpapi.com/search.json?engine={engine}&q={q}&num={num}&api_key={key}",ct).ConfigureAwait(false);
        var results=json.TryGetProperty("organic_results",out var r)
            ?r.EnumerateArray().Select(i=>new{
                title=i.TryGetProperty("title",out var t)?t.GetString():"",
                link=i.TryGetProperty("link",out var l)?l.GetString():"",
                snippet=i.TryGetProperty("snippet",out var s)?s.GetString():""}).ToArray():[];
        return ToolkitResult.Success(new{q,engine,results});
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
        var query=GetParam<string>(p,"query")??"";
        var type=GetParam<string>(p,"type")??"search"; // search|news|images
        var num=GetParam<int?>(p,"num")??10;
        var body=new{q=query,num};
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://google.serper.dev/{type}"){
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
        return ToolkitResult.Success(new{query,type,results,answerBox});
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
