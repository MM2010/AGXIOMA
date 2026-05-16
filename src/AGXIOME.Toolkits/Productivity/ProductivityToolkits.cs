// T-16..T-33 — Productivity Toolkits
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AGXIOME.Shared.Errors;

namespace AGXIOME.Toolkits.Productivity;

// ── shared HTTP client ─────────────────────────────────────────────────────
file static class Http { internal static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) }; }

// T-16 — Google Calendar (OAuth2 access_token required)
[Toolkit("google_calendar","Google Calendar",ToolkitCategory.Productivity)]
public sealed class GoogleCalendarToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_calendar"; public override string Name=>"Google Calendar";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        var calId=GetParam<string>(p,"calendar_id")??"primary";
        return a switch{
            "list_calendars"=>await GcGet("calendar/v3/users/me/calendarList",token,ct).ConfigureAwait(false),
            "list_events"   =>await GcGet($"calendar/v3/calendars/{Uri.EscapeDataString(calId)}/events?maxResults={GetParam<int?>(p,"max_results")??10}&singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(GetParam<string>(p,"time_min")??DateTime.UtcNow.ToString("O"))}",token,ct).ConfigureAwait(false),
            "get_event"     =>await GcGet($"calendar/v3/calendars/{Uri.EscapeDataString(calId)}/events/{GetRequiredParam<string>(p,"event_id")}",token,ct).ConfigureAwait(false),
            "create_event"  =>await GcPost($"calendar/v3/calendars/{Uri.EscapeDataString(calId)}/events",new{summary=GetParam<string>(p,"summary"),start=new{dateTime=GetRequiredParam<string>(p,"start")},end=new{dateTime=GetRequiredParam<string>(p,"end")}},token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    internal static async Task<ToolkitResult> GcGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://www.googleapis.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    internal static async Task<ToolkitResult> GcPost(string path,object body,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://www.googleapis.com/{path}"){Content=JsonContent.Create(body)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-17 — Google Drive (OAuth2 access_token required)
[Toolkit("google_drive","Google Drive",ToolkitCategory.Productivity)]
public sealed class GoogleDriveToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_drive"; public override string Name=>"Google Drive";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        return a switch{
            "list_files"    =>await GoogleCalendarToolkit.GcGet($"drive/v3/files?pageSize={GetParam<int?>(p,"page_size")??10}&fields=files(id,name,mimeType,size,modifiedTime)&q={Uri.EscapeDataString(GetParam<string>(p,"q")??"trashed=false")}",token,ct).ConfigureAwait(false),
            "get_file"      =>await GoogleCalendarToolkit.GcGet($"drive/v3/files/{GetRequiredParam<string>(p,"id")}?fields=id,name,mimeType,size,webViewLink",token,ct).ConfigureAwait(false),
            "create_folder" =>await GoogleCalendarToolkit.GcPost("drive/v3/files",new{name=GetRequiredParam<string>(p,"name"),mimeType="application/vnd.google-apps.folder"},token,ct).ConfigureAwait(false),
            "delete_file"   =>await DriveDelete(GetRequiredParam<string>(p,"id"),token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> DriveDelete(string id,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Delete,$"https://www.googleapis.com/drive/v3/files/{id}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(new{deleted=true,id});
    }
}

// T-18 — Google Docs (OAuth2 access_token required)
[Toolkit("google_docs","Google Docs",ToolkitCategory.Productivity)]
public sealed class GoogleDocsToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_docs"; public override string Name=>"Google Docs";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        return a switch{
            "get_doc"    =>await GoogleCalendarToolkit.GcGet($"docs/v1/documents/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "create_doc" =>await GoogleCalendarToolkit.GcPost("docs/v1/documents",new{title=GetParam<string>(p,"title")??"Untitled"},token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
}

// T-19 — Google Sheets (OAuth2 access_token required)
[Toolkit("google_sheets","Google Sheets",ToolkitCategory.Productivity)]
public sealed class GoogleSheetsToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_sheets"; public override string Name=>"Google Sheets";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        var spreadsheetId=GetRequiredParam<string>(p,"spreadsheet_id");
        var range=GetParam<string>(p,"range")??"Sheet1";
        return a switch{
            "read_values"  =>await GoogleCalendarToolkit.GcGet($"sheets/v4/spreadsheets/{spreadsheetId}/values/{Uri.EscapeDataString(range)}",token,ct).ConfigureAwait(false),
            "write_values" =>await GoogleCalendarToolkit.GcPost($"sheets/v4/spreadsheets/{spreadsheetId}/values/{Uri.EscapeDataString(range)}:append?valueInputOption=RAW",new{values=GetRequiredParam<object?>(p,"values")},token,ct).ConfigureAwait(false),
            "get_metadata" =>await GoogleCalendarToolkit.GcGet($"sheets/v4/spreadsheets/{spreadsheetId}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
}

// T-20 — OneDrive (OAuth2 access_token required)
[Toolkit("onedrive","OneDrive",ToolkitCategory.Productivity)]
public sealed class OneDriveToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"onedrive"; public override string Name=>"OneDrive";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        return a switch{
            "list_files"    =>await OdGet($"me/drive/root/children?$top={GetParam<int?>(p,"top")??20}",token,ct).ConfigureAwait(false),
            "get_item"      =>await OdGet($"me/drive/items/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "list_shared"   =>await OdGet("me/drive/sharedWithMe",token,ct).ConfigureAwait(false),
            "get_drive"     =>await OdGet("me/drive",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> OdGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://graph.microsoft.com/v1.0/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-21 — Notion (Bearer integration_token required)
[Toolkit("notion","Notion",ToolkitCategory.Productivity)]
public sealed class NotionToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"notion"; public override string Name=>"Notion";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("token");
        return a switch{
            "search"           =>await NoPost("search",new{query=GetParam<string>(p,"query")??""} ,token,ct).ConfigureAwait(false),
            "get_page"         =>await NoGet($"pages/{GetRequiredParam<string>(p,"page_id")}",token,ct).ConfigureAwait(false),
            "get_database"     =>await NoGet($"databases/{GetRequiredParam<string>(p,"database_id")}",token,ct).ConfigureAwait(false),
            "query_database"   =>await NoPost($"databases/{GetRequiredParam<string>(p,"database_id")}/query",GetParam<object?>(p,"filter")??new{},token,ct).ConfigureAwait(false),
            "get_block_children"=>await NoGet($"blocks/{GetRequiredParam<string>(p,"block_id")}/children",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> NoGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.notion.com/v1/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        req.Headers.Add("Notion-Version","2022-06-28");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> NoPost(string path,object body,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.notion.com/v1/{path}"){Content=JsonContent.Create(body)};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        req.Headers.Add("Notion-Version","2022-06-28");
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-22 — Confluence (Basic auth: email + api_token + domain)
[Toolkit("confluence","Confluence",ToolkitCategory.Productivity)]
public sealed class ConfluenceToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"confluence"; public override string Name=>"Confluence";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("domain"))&&!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var domain=(GetSetting("domain")??"").TrimEnd('/');
        var email=GetRequiredSetting("email");
        var apiToken=GetRequiredSetting("api_token");
        var creds=Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        return a switch{
            "list_spaces"   =>await CfGet($"{domain}/wiki/rest/api/space?limit={GetParam<int?>(p,"limit")??25}",creds,ct).ConfigureAwait(false),
            "list_pages"    =>await CfGet($"{domain}/wiki/rest/api/content?spaceKey={GetRequiredParam<string>(p,"space_key")}&type=page&limit={GetParam<int?>(p,"limit")??25}",creds,ct).ConfigureAwait(false),
            "get_page"      =>await CfGet($"{domain}/wiki/rest/api/content/{GetRequiredParam<string>(p,"id")}?expand=body.storage",creds,ct).ConfigureAwait(false),
            "search"        =>await CfGet($"{domain}/wiki/rest/api/search?cql={Uri.EscapeDataString(GetRequiredParam<string>(p,"cql"))}&limit={GetParam<int?>(p,"limit")??10}",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> CfGet(string url,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-23 — Jira (Basic auth: email + api_token + domain)
[Toolkit("jira","Jira",ToolkitCategory.Productivity)]
public sealed class JiraToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"jira"; public override string Name=>"Jira";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("domain"))&&!string.IsNullOrWhiteSpace(GetSetting("api_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var domain=(GetSetting("domain")??"").TrimEnd('/');
        var email=GetRequiredSetting("email");
        var apiToken=GetRequiredSetting("api_token");
        var creds=Convert.ToBase64String(Encoding.UTF8.GetBytes($"{email}:{apiToken}"));
        var api=$"{domain}/rest/api/3";
        return a switch{
            "list_issues"   =>await JrGet($"{api}/search?jql={Uri.EscapeDataString(GetParam<string>(p,"jql")??"project IS NOT EMPTY ORDER BY updated DESC")}&maxResults={GetParam<int?>(p,"max_results")??10}",creds,ct).ConfigureAwait(false),
            "get_issue"     =>await JrGet($"{api}/issue/{GetRequiredParam<string>(p,"key")}",creds,ct).ConfigureAwait(false),
            "list_projects" =>await JrGet($"{api}/project?maxResults={GetParam<int?>(p,"max_results")??20}",creds,ct).ConfigureAwait(false),
            "get_project"   =>await JrGet($"{api}/project/{GetRequiredParam<string>(p,"key")}",creds,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> JrGet(string url,string creds,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",creds);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-24 — Linear (Bearer api_key required — GraphQL)
[Toolkit("linear","Linear",ToolkitCategory.Productivity)]
public sealed class LinearToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"linear"; public override string Name=>"Linear";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var query=a switch{
            "list_issues" =>"{ issues(first: 25) { nodes { id title state { name } priority assignee { name } createdAt } } }",
            "list_teams"  =>"{ teams { nodes { id name key } } }",
            "list_projects"=>"{ projects { nodes { id name description } } }",
            "get_issue"   =>$"{{ issue(id: \"{GetRequiredParam<string>(p,"id")}\") {{ id title description state {{ name }} priority assignee {{ name }} createdAt }} }}",
            _=>GetParam<string>(p,"query")??throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
        var req=new HttpRequestMessage(HttpMethod.Post,"https://api.linear.app/graphql"){Content=JsonContent.Create(new{query})};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-25 — Asana (Bearer token required)
[Toolkit("asana","Asana",ToolkitCategory.Productivity)]
public sealed class AsanaToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"asana"; public override string Name=>"Asana";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("token");
        return a switch{
            "list_workspaces"=>await AsGet("workspaces",token,ct).ConfigureAwait(false),
            "list_projects"  =>await AsGet($"projects?workspace={GetRequiredParam<string>(p,"workspace")}",token,ct).ConfigureAwait(false),
            "list_tasks"     =>await AsGet($"tasks?project={GetRequiredParam<string>(p,"project")}&opt_fields=id,name,completed,due_on,assignee",token,ct).ConfigureAwait(false),
            "get_task"       =>await AsGet($"tasks/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> AsGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://app.asana.com/api/1.0/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-26 — Trello (api_key + token required)
[Toolkit("trello","Trello",ToolkitCategory.Productivity)]
public sealed class TrelloToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"trello"; public override string Name=>"Trello";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_key"))&&!string.IsNullOrWhiteSpace(GetSetting("token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var key=GetRequiredSetting("api_key");
        var tok=GetRequiredSetting("token");
        var auth=$"key={key}&token={tok}";
        return a switch{
            "list_boards"  =>await TrGet($"members/me/boards?{auth}&fields=id,name,desc,url",ct).ConfigureAwait(false),
            "get_board"    =>await TrGet($"boards/{GetRequiredParam<string>(p,"id")}?{auth}",ct).ConfigureAwait(false),
            "list_lists"   =>await TrGet($"boards/{GetRequiredParam<string>(p,"board_id")}/lists?{auth}",ct).ConfigureAwait(false),
            "list_cards"   =>await TrGet($"lists/{GetRequiredParam<string>(p,"list_id")}/cards?{auth}",ct).ConfigureAwait(false),
            "get_card"     =>await TrGet($"cards/{GetRequiredParam<string>(p,"id")}?{auth}",ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> TrGet(string path,CancellationToken ct)=>ToolkitResult.Success(await Http.Client.GetFromJsonAsync<JsonElement>($"https://api.trello.com/1/{path}",ct).ConfigureAwait(false));
}

// T-27 — Todoist (Bearer token required)
[Toolkit("todoist","Todoist",ToolkitCategory.Productivity)]
public sealed class TodoistToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"todoist"; public override string Name=>"Todoist";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("token");
        return a switch{
            "list_tasks"    =>await TdGet($"tasks?project_id={GetParam<string>(p,"project_id")??""}",token,ct).ConfigureAwait(false),
            "get_task"      =>await TdGet($"tasks/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            "list_projects" =>await TdGet("projects",token,ct).ConfigureAwait(false),
            "close_task"    =>await TdPost($"tasks/{GetRequiredParam<string>(p,"id")}/close",null,token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> TdGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.todoist.com/rest/v2/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> TdPost(string path,object? body,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://api.todoist.com/rest/v2/{path}"){Content=body!=null?JsonContent.Create(body):new StringContent("")};
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);
        return resp.IsSuccessStatusCode?ToolkitResult.Success(new{done=true}):ToolkitResult.Fail($"Todoist error {(int)resp.StatusCode}");
    }
}

// T-28 — Google Tasks (OAuth2 access_token required)
[Toolkit("google_tasks","Google Tasks",ToolkitCategory.Productivity)]
public sealed class GoogleTasksToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_tasks"; public override string Name=>"Google Tasks";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        var listId=GetParam<string>(p,"tasklist_id")??"@default";
        return a switch{
            "list_tasklists"=>await GoogleCalendarToolkit.GcGet("tasks/v1/users/@me/lists",token,ct).ConfigureAwait(false),
            "list_tasks"    =>await GoogleCalendarToolkit.GcGet($"tasks/v1/lists/{Uri.EscapeDataString(listId)}/tasks",token,ct).ConfigureAwait(false),
            "get_task"      =>await GoogleCalendarToolkit.GcGet($"tasks/v1/lists/{Uri.EscapeDataString(listId)}/tasks/{GetRequiredParam<string>(p,"id")}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
}

// T-29 — Evernote (informative stub — complex SDK-based OAuth)
[Toolkit("evernote","Evernote",ToolkitCategory.Productivity)]
public sealed class EvernoteToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"evernote"; public override string Name=>"Evernote";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"));
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Fail("Evernote requires their Thrift-based SDK. Use NuGet Evernote.EDAM or Evernote.API. Settings: access_token, sandbox (bool). Docs: https://dev.evernote.com/"));
}

// T-30 — Obsidian (Local REST API via obsidian-local-rest-api plugin)
[Toolkit("obsidian","Obsidian",ToolkitCategory.Productivity)]
public sealed class ObsidianToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"obsidian"; public override string Name=>"Obsidian";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("base_url"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var base_=(GetSetting("base_url")??"http://127.0.0.1:27123").TrimEnd('/');
        var apiKey=GetSetting("api_key")??"";
        return a switch{
            "list_files"  =>await ObGet($"{base_}/vault/",apiKey,ct).ConfigureAwait(false),
            "read_note"   =>await ObGet($"{base_}/vault/{Uri.EscapeDataString(GetRequiredParam<string>(p,"path"))}",apiKey,ct).ConfigureAwait(false),
            "search"      =>await ObPost($"{base_}/search/",new{query=GetRequiredParam<string>(p,"query")},apiKey,ct).ConfigureAwait(false),
            "active_note" =>await ObGet($"{base_}/active/",apiKey,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ObGet(string url,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        if(!string.IsNullOrWhiteSpace(key))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};
        var resp=await client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> ObPost(string url,object body,string key,CancellationToken ct){
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(10)};
        var req=new HttpRequestMessage(HttpMethod.Post,url){Content=JsonContent.Create(body)};
        if(!string.IsNullOrWhiteSpace(key))req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
        var resp=await client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-31 — Habitica (api_user + api_key required)
[Toolkit("habitica","Habitica",ToolkitCategory.Productivity)]
public sealed class HabiticaToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"habitica"; public override string Name=>"Habitica";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("api_user"))&&!string.IsNullOrWhiteSpace(GetSetting("api_key"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var user=GetRequiredSetting("api_user");
        var key=GetRequiredSetting("api_key");
        return a switch{
            "get_user"    =>await HbGet("user",user,key,ct).ConfigureAwait(false),
            "list_tasks"  =>await HbGet($"tasks/user?type={GetParam<string>(p,"type")??"todos"}",user,key,ct).ConfigureAwait(false),
            "score_task"  =>await HbPost($"tasks/{GetRequiredParam<string>(p,"task_id")}/score/{GetParam<string>(p,"direction")??"up"}",null,user,key,ct).ConfigureAwait(false),
            "list_party"  =>await HbGet("groups/party",user,key,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> HbGet(string path,string user,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://habitica.com/api/v3/{path}");
        req.Headers.Add("x-api-user",user);req.Headers.Add("x-api-key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
    private static async Task<ToolkitResult> HbPost(string path,object? body,string user,string key,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Post,$"https://habitica.com/api/v3/{path}"){Content=body!=null?JsonContent.Create(body):new StringContent("{}")};
        req.Headers.Add("x-api-user",user);req.Headers.Add("x-api-key",key);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-32 — Calendly (Bearer token required)
[Toolkit("calendly","Calendly",ToolkitCategory.Productivity)]
public sealed class CalendlyToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"calendly"; public override string Name=>"Calendly";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("token"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("token");
        return a switch{
            "get_user"        =>await ClGet("users/me",token,ct).ConfigureAwait(false),
            "list_event_types"=>await ClGet($"event_types?user={GetRequiredSetting("user_uri")}",token,ct).ConfigureAwait(false),
            "list_events"     =>await ClGet($"scheduled_events?user={GetRequiredSetting("user_uri")}&count={GetParam<int?>(p,"count")??10}",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> ClGet(string path,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,$"https://api.calendly.com/{path}");
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}

// T-33 — DocuSign (OAuth2 access_token + account_id required)
[Toolkit("docusign","DocuSign",ToolkitCategory.Productivity)]
public sealed class DocuSignToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"docusign"; public override string Name=>"DocuSign";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    public override bool IsConfigured=>!string.IsNullOrWhiteSpace(GetSetting("access_token"))&&!string.IsNullOrWhiteSpace(GetSetting("account_id"));
    protected override async Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct){
        var token=GetRequiredSetting("access_token");
        var accountId=GetRequiredSetting("account_id");
        var base_=GetSetting("base_url")??"https://na4.docusign.net/restapi/v2.1";
        return a switch{
            "list_envelopes"=>await DsGet($"{base_}/accounts/{accountId}/envelopes?from_date={GetParam<string>(p,"from_date")??DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd")}&count={GetParam<int?>(p,"count")??10}",token,ct).ConfigureAwait(false),
            "get_envelope"  =>await DsGet($"{base_}/accounts/{accountId}/envelopes/{GetRequiredParam<string>(p,"envelope_id")}",token,ct).ConfigureAwait(false),
            "get_userinfo"  =>await DsGet("https://account.docusign.com/oauth/userinfo",token,ct).ConfigureAwait(false),
            _=>throw AgxiomeToolkitException.UnknownAction(Id, a)
        };
    }
    private static async Task<ToolkitResult> DsGet(string url,string token,CancellationToken ct){
        var req=new HttpRequestMessage(HttpMethod.Get,url);
        req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
        var resp=await Http.Client.SendAsync(req,ct).ConfigureAwait(false);resp.EnsureSuccessStatusCode();
        return ToolkitResult.Success(await resp.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false));
    }
}
