// T-150..T-161 — Enterprise Toolkits
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.Enterprise;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http { internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) }; }

// T-150 — Salesforce REST API (OAuth2 access_token + instance_url required)
[Toolkit("salesforce","Salesforce",ToolkitCategory.Enterprise)]
public sealed class SalesforceToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"salesforce";public override string Name=>"Salesforce";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"))&&!string.IsNullOrWhiteSpace(GetSetting("instance_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        var inst=(GetSetting("instance_url")??"").TrimEnd('/');
        var api=$"{inst}/services/data/v59.0";
        return a switch{
            "query"         =>await SfGet($"{api}/query?q={Uri.EscapeDataString(GetParam<string>(p,"soql")??"SELECT Id,Name FROM Account LIMIT 10")}",token,ct).ConfigureAwait(false),
            "get_object"    =>await SfGet($"{api}/sobjects/{GetParam<string>(p,"object")??"Account"}/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "list_objects"  =>await SfGet($"{api}/sobjects",token,ct).ConfigureAwait(false),
            "describe"      =>await SfGet($"{api}/sobjects/{GetParam<string>(p,"object")??"Account"}/describe",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> SfGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-151 — HubSpot (Bearer api_key required)
[Toolkit("hubspot","HubSpot",ToolkitCategory.Enterprise)]
public sealed class HubSpotToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"hubspot";public override string Name=>"HubSpot";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var limit=GetParam<int?>(p,"limit")??10;
        return a switch{
            "list_contacts"  =>await HsGet($"crm/v3/objects/contacts?limit={limit}",key,ct).ConfigureAwait(false),
            "get_contact"    =>await HsGet($"crm/v3/objects/contacts/{GetParam<string>(p,"id")}",key,ct).ConfigureAwait(false),
            "list_companies" =>await HsGet($"crm/v3/objects/companies?limit={limit}",key,ct).ConfigureAwait(false),
            "list_deals"     =>await HsGet($"crm/v3/objects/deals?limit={limit}",key,ct).ConfigureAwait(false),
            "list_tickets"   =>await HsGet($"crm/v3/objects/tickets?limit={limit}",key,ct).ConfigureAwait(false),
            "get_owner"      =>await HsGet($"crm/v3/owners/{GetParam<string>(p,"id")}",key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> HsGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.hubapi.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-152 — Zendesk (Basic auth: email/token + subdomain)
[Toolkit("zendesk","Zendesk",ToolkitCategory.Enterprise)]
public sealed class ZendeskToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"zendesk";public override string Name=>"Zendesk";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("subdomain"))&&!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var sub=GetRequiredSetting("subdomain");
        var email=GetRequiredSetting("email");
        var apiToken=GetRequiredSetting("api_token");
        var creds=Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}/token:{apiToken}"));
        var base_=$"https://{sub}.zendesk.com/api/v2";
        return a switch{
            "list_tickets"  =>await ZdGet($"{base_}/tickets.json?per_page={GetParam<int?>(p,"per_page")??10}",creds,ct).ConfigureAwait(false),
            "get_ticket"    =>await ZdGet($"{base_}/tickets/{GetParam<string>(p,"id")}.json",creds,ct).ConfigureAwait(false),
            "list_users"    =>await ZdGet($"{base_}/users.json?per_page={GetParam<int?>(p,"per_page")??10}",creds,ct).ConfigureAwait(false),
            "search"        =>await ZdGet($"{base_}/search.json?query={Uri.EscapeDataString(GetRequiredParam<string>(p,"query"))}",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ZdGet(string url,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-153 — ServiceNow (informative stub — complex OAuth)
[Toolkit("servicenow","ServiceNow",ToolkitCategory.Enterprise)]
public sealed class ServiceNowToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"servicenow";public override string Name=>"ServiceNow";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("instance_url"))&&!string.IsNullOrWhiteSpace(GetSetting("username"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var inst=(GetSetting("instance_url")??"").TrimEnd('/');
        var user=GetRequiredSetting("username");
        var pass=GetRequiredSetting("password");
        var creds=Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        var table=GetParam<string>(p,"table")??"incident";
        if(!IsValidTableName(table))return ToolkitResult.Fail($"Invalid table name: '{table}'");
        return a switch{
            "list"    =>await SnGet($"{inst}/api/now/table/{table}?sysparm_limit={GetParam<int?>(p,"limit")??10}",creds,ct).ConfigureAwait(false),
            "get"     =>await SnGet($"{inst}/api/now/table/{table}/{GetRequiredParam<string>(p,"sys_id")}",creds,ct).ConfigureAwait(false),
            "create"  =>await SnPost($"{inst}/api/now/table/{table}",GetParam<object?>(p,"data")??new{short_description=GetParam<string>(p,"description")??"New Record"},creds,ct).ConfigureAwait(false),
            "update"  =>await SnPatch($"{inst}/api/now/table/{table}/{GetRequiredParam<string>(p,"sys_id")}",GetParam<object?>(p,"data")??new{},creds,ct).ConfigureAwait(false),
            "search"  =>await SnGet($"{inst}/api/now/table/{table}?sysparm_query={Uri.EscapeDataString(GetRequiredParam<string>(p,"query"))}&sysparm_limit={GetParam<int?>(p,"limit")??10}",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static bool IsValidTableName(string n)=>
        !string.IsNullOrWhiteSpace(n)&&n.All(c=>char.IsLetterOrDigit(c)||c=='_');
    private static async Task<ToolkitResult> SnGet(string url,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> SnPost(string url,object body,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,url){Content=JsonContent.Create(body)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> SnPatch(string url,object body,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Patch,url){Content=JsonContent.Create(body)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        req.Headers.Add("Accept","application/json");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-154 — Datadog (DD-API-KEY + DD-APPLICATION-KEY headers)
[Toolkit("datadog","Datadog",ToolkitCategory.Enterprise)]
public sealed class DatadogToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"datadog";public override string Name=>"Datadog";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var apiKey=GetRequiredSetting("api_key");
        var appKey=GetSetting("app_key")??"";
        var site=GetSetting("site")??"datadoghq.com";
        return a switch{
            "query_metrics"  =>await DdGet($"https://api.{site}/api/v1/query?from={GetParam<long?>(p,"from")??DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()}&to={GetParam<long?>(p,"to")??DateTimeOffset.UtcNow.ToUnixTimeSeconds()}&query={Uri.EscapeDataString(GetRequiredParam<string>(p,"query"))}",apiKey,appKey,ct).ConfigureAwait(false),
            "list_monitors"  =>await DdGet($"https://api.{site}/api/v1/monitor?page={GetParam<int?>(p,"page")??0}&page_size={GetParam<int?>(p,"page_size")??25}",apiKey,appKey,ct).ConfigureAwait(false),
            "get_monitor"    =>await DdGet($"https://api.{site}/api/v1/monitor/{GetRequiredParam<string>(p,"id")}",apiKey,appKey,ct).ConfigureAwait(false),
            "list_events"    =>await DdGet($"https://api.{site}/api/v1/events?start={GetParam<long?>(p,"start")??DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds()}&end={GetParam<long?>(p,"end")??DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",apiKey,appKey,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> DdGet(string url,string apiKey,string appKey,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Add("DD-API-KEY",apiKey);
        if(!string.IsNullOrWhiteSpace(appKey))req.Headers.Add("DD-APPLICATION-KEY",appKey);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-155 — PagerDuty (Bearer api_key required)
[Toolkit("pagerduty","PagerDuty",ToolkitCategory.Enterprise)]
public sealed class PagerDutyToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"pagerduty";public override string Name=>"PagerDuty";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        return a switch{
            "list_incidents"  =>await PdGet($"incidents?limit={GetParam<int?>(p,"limit")??10}&statuses[]={GetParam<string>(p,"status")??"triggered"}",key,ct).ConfigureAwait(false),
            "get_incident"    =>await PdGet($"incidents/{GetRequiredParam<string>(p,"id")}",key,ct).ConfigureAwait(false),
            "list_services"   =>await PdGet("services",key,ct).ConfigureAwait(false),
            "list_on_calls"   =>await PdGet("oncalls",key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> PdGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.pagerduty.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Token",key);
        req.Headers.Add("Accept","application/vnd.pagerduty+json;version=2");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-156 — Sentry (Bearer token required)
[Toolkit("sentry","Sentry",ToolkitCategory.Enterprise)]
public sealed class SentryToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"sentry";public override string Name=>"Sentry";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("auth_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("auth_token");
        var org=GetSetting("organization")??"";
        var proj=GetSetting("project")??GetParam<string>(p,"project")??"";
        return a switch{
            "list_projects" =>await SyGet($"organizations/{org}/projects/",token,ct).ConfigureAwait(false),
            "list_issues"   =>await SyGet($"projects/{org}/{proj}/issues/?limit={GetParam<int?>(p,"limit")??10}",token,ct).ConfigureAwait(false),
            "get_issue"     =>await SyGet($"issues/{GetRequiredParam<string>(p,"id")}/",token,ct).ConfigureAwait(false),
            "list_events"   =>await SyGet($"projects/{org}/{proj}/events/?limit={GetParam<int?>(p,"limit")??10}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> SyGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://sentry.io/api/0/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-157 — Grafana (Bearer api_key or basic auth)
[Toolkit("grafana","Grafana",ToolkitCategory.Enterprise)]
public sealed class GrafanaToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"grafana";public override string Name=>"Grafana";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("base_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var base_=(GetSetting("base_url")??"").TrimEnd('/');
        var token=GetSetting("api_key");
        var user=GetSetting("username");var pass=GetSetting("password");
        return a switch{
            "list_dashboards"=>await GfGet($"{base_}/api/search?type=dash-db&limit={GetParam<int?>(p,"limit")??25}",token,user,pass,ct).ConfigureAwait(false),
            "get_dashboard"  =>await GfGet($"{base_}/api/dashboards/uid/{GetRequiredParam<string>(p,"uid")}",token,user,pass,ct).ConfigureAwait(false),
            "list_datasources"=>await GfGet($"{base_}/api/datasources",token,user,pass,ct).ConfigureAwait(false),
            "list_alerts"    =>await GfGet($"{base_}/api/alert-notifications",token,user,pass,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> GfGet(string url,string? token,string? user,string? pass,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        if(!string.IsNullOrWhiteSpace(token))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        else if(user!=null&&pass!=null)req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-158 — New Relic (API-Key header)
[Toolkit("newrelic","New Relic",ToolkitCategory.Enterprise)]
public sealed class NewRelicToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"newrelic";public override string Name=>"New Relic";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var accountId=GetRequiredSetting("account_id");
        return a switch{
            "nrql" =>await NrQuery(GetRequiredParam<string>(p,"query"),accountId,key,ct).ConfigureAwait(false),
            "list_applications"=>await NrGet($"v2/applications.json",key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> NrGet(string path,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.newrelic.com/{path}");
        req.Headers.Add("X-Api-Key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> NrQuery(string nrql,string accountId,string key,CancellationToken ct){
        var payload=new{query=$"{{ actor {{ account(id: {accountId}) {{ nrql(query: \"{nrql.Replace("\"","\\\"")}\") {{ results }} }} }} }}"};
        var req=new HttpRequestMessage(HttpMethod.Post,"https://api.newrelic.com/graphql"){Content=JsonContent.Create(payload)};
        req.Headers.Add("API-Key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-159 — Elasticsearch (REST API — Basic or Bearer auth)
[Toolkit("elasticsearch","Elasticsearch",ToolkitCategory.Enterprise)]
public sealed class ElasticsearchToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"elasticsearch";public override string Name=>"Elasticsearch";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("host"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var host=(GetSetting("host")??"http://localhost:9200").TrimEnd('/');
        var user=GetSetting("username");var pass=GetSetting("password");var apiKey=GetSetting("api_key");
        var idx=GetRequiredParam<string>(p,"index");
        return a switch{
            "search"         =>await EsPost($"{host}/{idx}/_search",GetParam<object?>(p,"query")??new{query=new{match_all=new{}}},user,pass,apiKey,ct).ConfigureAwait(false),
            "index"          =>await EsPost($"{host}/{idx}/_doc/{GetParam<string>(p,"id")??""}",GetParam<object?>(p,"document")??new{},user,pass,apiKey,ct).ConfigureAwait(false),
            "get"            =>await EsGet($"{host}/{idx}/_doc/{GetRequiredParam<string>(p,"id")}",user,pass,apiKey,ct).ConfigureAwait(false),
            "delete"         =>await EsDelete($"{host}/{idx}/_doc/{GetRequiredParam<string>(p,"id")}",user,pass,apiKey,ct).ConfigureAwait(false),
            "cluster_health" =>await EsGet($"{host}/_cluster/health",user,pass,apiKey,ct).ConfigureAwait(false),
            "list_indices"   =>await EsGet($"{host}/_cat/indices?format=json",user,pass,apiKey,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static HttpRequestMessage EsRequest(HttpMethod method,string url,string? user,string? pass,string? apiKey){
        var req=new HttpRequestMessage(method,url);
        if(apiKey!=null)req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("ApiKey",apiKey);
        else if(user!=null&&pass!=null)req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
        return req;
    }
    private static async Task<ToolkitResult> EsGet(string url,string? user,string? pass,string? apiKey,CancellationToken ct){
        var req=EsRequest(HttpMethod.Get,url,user,pass,apiKey);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> EsPost(string url,object body,string? user,string? pass,string? apiKey,CancellationToken ct){
        var req=EsRequest(HttpMethod.Post,url,user,pass,apiKey);req.Content=JsonContent.Create(body);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> EsDelete(string url,string? user,string? pass,string? apiKey,CancellationToken ct){
        var req=EsRequest(HttpMethod.Delete,url,user,pass,apiKey);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-160 — Okta (api_token required)
[Toolkit("okta","Okta",ToolkitCategory.Enterprise)]
public sealed class OktaToolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"okta";public override string Name=>"Okta";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("domain"))&&!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var domain=(GetSetting("domain")??"").TrimEnd('/');
        var token=GetRequiredSetting("api_token");
        var limit=GetParam<int?>(p,"limit")??20;
        return a switch{
            "list_users"  =>await OkGet($"{domain}/api/v1/users?limit={limit}",token,ct).ConfigureAwait(false),
            "get_user"    =>await OkGet($"{domain}/api/v1/users/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "list_groups" =>await OkGet($"{domain}/api/v1/groups?limit={limit}",token,ct).ConfigureAwait(false),
            "list_apps"   =>await OkGet($"{domain}/api/v1/apps?limit={limit}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> OkGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("SSWS",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-161 — Auth0 (Management API Bearer token required)
[Toolkit("auth0","Auth0",ToolkitCategory.Enterprise)]
public sealed class Auth0Toolkit(ToolkitConfig? cfg=null):ToolkitBase(cfg){
    public override string Id=>"auth0";public override string Name=>"Auth0";
    public override ToolkitCategory Category=>ToolkitCategory.Enterprise;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("domain"))&&!string.IsNullOrWhiteSpace(GetSetting("management_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var domain=(GetSetting("domain")??"").TrimEnd('/');
        var token=GetRequiredSetting("management_token");
        var limit=GetParam<int?>(p,"per_page")??20;
        return a switch{
            "list_users"  =>await A0Get($"{domain}/api/v2/users?per_page={limit}",token,ct).ConfigureAwait(false),
            "get_user"    =>await A0Get($"{domain}/api/v2/users/{Uri.EscapeDataString(GetRequiredParam<string>(p,"id"))}",token,ct).ConfigureAwait(false),
            "list_clients"=>await A0Get($"{domain}/api/v2/clients?per_page={limit}",token,ct).ConfigureAwait(false),
            "list_roles"  =>await A0Get($"{domain}/api/v2/roles?per_page={limit}",token,ct).ConfigureAwait(false),
            "stats"       =>await A0Get($"{domain}/api/v2/stats/daily?from={GetParam<string>(p,"from")??"20240101"}&to={GetParam<string>(p,"to")??"20241231"}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> A0Get(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}
