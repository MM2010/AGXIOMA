// T-16..T-33 — Productivity Toolkits

namespace AGXIOME.Toolkits.Productivity;

// T-16
[Toolkit("google_calendar","Google Calendar",ToolkitCategory.Productivity)]
public sealed class GoogleCalendarToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_calendar"; public override string Name=>"Google Calendar";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,calendarId=GetParam<string>(p,"calendar_id")}));
}
// T-17
[Toolkit("google_drive","Google Drive",ToolkitCategory.Productivity)]
public sealed class GoogleDriveToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_drive"; public override string Name=>"Google Drive";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,fileId=GetParam<string>(p,"file_id")}));
}
// T-18
[Toolkit("google_docs","Google Docs",ToolkitCategory.Productivity)]
public sealed class GoogleDocsToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_docs"; public override string Name=>"Google Docs";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,docId=GetParam<string>(p,"doc_id")}));
}
// T-19
[Toolkit("google_sheets","Google Sheets",ToolkitCategory.Productivity)]
public sealed class GoogleSheetsToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_sheets"; public override string Name=>"Google Sheets";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,spreadsheetId=GetParam<string>(p,"spreadsheet_id")}));
}
// T-20
[Toolkit("onedrive","OneDrive",ToolkitCategory.Productivity)]
public sealed class OneDriveToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"onedrive"; public override string Name=>"OneDrive";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,itemId=GetParam<string>(p,"item_id")}));
}
// T-21
[Toolkit("notion","Notion",ToolkitCategory.Productivity)]
public sealed class NotionToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"notion"; public override string Name=>"Notion";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,pageId=GetParam<string>(p,"page_id")}));
}
// T-22
[Toolkit("confluence","Confluence",ToolkitCategory.Productivity)]
public sealed class ConfluenceToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"confluence"; public override string Name=>"Confluence";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,spaceKey=GetParam<string>(p,"space_key")}));
}
// T-23
[Toolkit("jira","Jira",ToolkitCategory.Productivity)]
public sealed class JiraToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"jira"; public override string Name=>"Jira";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,issueKey=GetParam<string>(p,"issue_key")}));
}
// T-24
[Toolkit("linear","Linear",ToolkitCategory.Productivity)]
public sealed class LinearToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"linear"; public override string Name=>"Linear";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,issueId=GetParam<string>(p,"issue_id")}));
}
// T-25
[Toolkit("asana","Asana",ToolkitCategory.Productivity)]
public sealed class AsanaToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"asana"; public override string Name=>"Asana";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,taskId=GetParam<string>(p,"task_id")}));
}
// T-26
[Toolkit("trello","Trello",ToolkitCategory.Productivity)]
public sealed class TrelloToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"trello"; public override string Name=>"Trello";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,cardId=GetParam<string>(p,"card_id")}));
}
// T-27
[Toolkit("todoist","Todoist",ToolkitCategory.Productivity)]
public sealed class TodoistToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"todoist"; public override string Name=>"Todoist";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,projectId=GetParam<string>(p,"project_id")}));
}
// T-28
[Toolkit("google_tasks","Google Tasks",ToolkitCategory.Productivity)]
public sealed class GoogleTasksToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"google_tasks"; public override string Name=>"Google Tasks";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,tasklistId=GetParam<string>(p,"tasklist_id")}));
}
// T-29
[Toolkit("evernote","Evernote",ToolkitCategory.Productivity)]
public sealed class EvernoteToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"evernote"; public override string Name=>"Evernote";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,noteId=GetParam<string>(p,"note_id")}));
}
// T-30
[Toolkit("obsidian","Obsidian",ToolkitCategory.Productivity)]
public sealed class ObsidianToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"obsidian"; public override string Name=>"Obsidian";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,vault=GetParam<string>(p,"vault"),note=GetParam<string>(p,"note")}));
}
// T-31
[Toolkit("habitica","Habitica",ToolkitCategory.Productivity)]
public sealed class HabiticaToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"habitica"; public override string Name=>"Habitica";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,taskId=GetParam<string>(p,"task_id")}));
}
// T-32
[Toolkit("calendly","Calendly",ToolkitCategory.Productivity)]
public sealed class CalendlyToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"calendly"; public override string Name=>"Calendly";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,eventType=GetParam<string>(p,"event_type")}));
}
// T-33
[Toolkit("docusign","DocuSign",ToolkitCategory.Productivity)]
public sealed class DocuSignToolkit(ToolkitConfig? config=null):ToolkitBase(config){
    public override string Id=>"docusign"; public override string Name=>"DocuSign";
    public override ToolkitCategory Category=>ToolkitCategory.Productivity;
    protected override Task<ToolkitResult> ExecuteCoreAsync(string a,Dictionary<string,object?> p,CancellationToken ct)
        =>Task.FromResult(ToolkitResult.Success(new{action=a,envelopeId=GetParam<string>(p,"envelope_id")}));
}
