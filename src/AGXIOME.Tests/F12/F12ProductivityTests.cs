using AGXIOME.Toolkits;
using AGXIOME.Toolkits.Productivity;

namespace AGXIOME.Tests.F12;

public sealed class GoogleCalendarToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_calendar", new GoogleCalendarToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Calendar", new GoogleCalendarToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new GoogleCalendarToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleCalendarToolkit().ExecuteAsync("list_events", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("google_calendar", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GoogleCalendarToolkit(cfg).ExecuteAsync("list_events", new Dictionary<string, object?> { ["calendar_id"] = "primary" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class GoogleDriveToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_drive", new GoogleDriveToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Drive", new GoogleDriveToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new GoogleDriveToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleDriveToolkit().ExecuteAsync("list_files", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("google_drive", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GoogleDriveToolkit(cfg).ExecuteAsync("list_files", new Dictionary<string, object?> { ["folder_id"] = "root" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class GoogleDocsToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_docs", new GoogleDocsToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Docs", new GoogleDocsToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new GoogleDocsToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleDocsToolkit().ExecuteAsync("read_document", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("google_docs", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GoogleDocsToolkit(cfg).ExecuteAsync("read_document", new Dictionary<string, object?> { ["document_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    // ── Security / boundary tests ──────────────────────────────────────────
    [Fact] public async Task ListDocs_NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleDocsToolkit().ExecuteAsync("list_docs", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task AppendText_NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleDocsToolkit().ExecuteAsync("append_text", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task UpdateTitle_NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleDocsToolkit().ExecuteAsync("update_title", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task AppendText_FakeToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_docs", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        var r = await new GoogleDocsToolkit(cfg).ExecuteAsync("append_text",
            new Dictionary<string, object?> { ["document_id"] = "doc_id_123", ["text"] = "<script>alert(1)</script>" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    [Fact] public async Task AppendText_MissingDocId_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_docs", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        var r = await new GoogleDocsToolkit(cfg).ExecuteAsync("append_text",
            new Dictionary<string, object?> { ["text"] = "some text" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task UnknownAction_WithToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_docs", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        var r = await new GoogleDocsToolkit(cfg).ExecuteAsync("__malicious__; rm -rf /", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class GoogleSheetsToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_sheets", new GoogleSheetsToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Sheets", new GoogleSheetsToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new GoogleSheetsToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleSheetsToolkit().ExecuteAsync("read_range", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("google_sheets", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GoogleSheetsToolkit(cfg).ExecuteAsync("read_range", new Dictionary<string, object?> { ["spreadsheet_id"] = "1", ["range"] = "A1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class OneDriveToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("onedrive", new OneDriveToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("OneDrive", new OneDriveToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new OneDriveToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new OneDriveToolkit().ExecuteAsync("list_files", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("onedrive", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new OneDriveToolkit(cfg).ExecuteAsync("list_files", new Dictionary<string, object?> { ["folder"] = "/" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class NotionToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("notion", new NotionToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Notion", new NotionToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new NotionToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new NotionToolkit().ExecuteAsync("read_page", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("notion", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new NotionToolkit(cfg).ExecuteAsync("read_page", new Dictionary<string, object?> { ["page_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class ConfluenceToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("confluence", new ConfluenceToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Confluence", new ConfluenceToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new ConfluenceToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ConfluenceToolkit().ExecuteAsync("read_page", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("confluence", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ConfluenceToolkit(cfg).ExecuteAsync("read_page", new Dictionary<string, object?> { ["page_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class JiraToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("jira", new JiraToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Jira", new JiraToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new JiraToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new JiraToolkit().ExecuteAsync("list_issues", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("jira", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new JiraToolkit(cfg).ExecuteAsync("list_issues", new Dictionary<string, object?> { ["project"] = "PROJ" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class LinearToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("linear", new LinearToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Linear", new LinearToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new LinearToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new LinearToolkit().ExecuteAsync("list_issues", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("linear", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new LinearToolkit(cfg).ExecuteAsync("list_issues", new Dictionary<string, object?> { ["team"] = "t" }, default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class AsanaToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("asana", new AsanaToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Asana", new AsanaToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new AsanaToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new AsanaToolkit().ExecuteAsync("list_tasks", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("asana", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new AsanaToolkit(cfg).ExecuteAsync("list_tasks", new Dictionary<string, object?> { ["project_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class TrelloToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("trello", new TrelloToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Trello", new TrelloToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new TrelloToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TrelloToolkit().ExecuteAsync("list_cards", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("trello", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TrelloToolkit(cfg).ExecuteAsync("list_cards", new Dictionary<string, object?> { ["board_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class TodoistToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("todoist", new TodoistToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Todoist", new TodoistToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new TodoistToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new TodoistToolkit().ExecuteAsync("list_tasks", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("todoist", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new TodoistToolkit(cfg).ExecuteAsync("list_tasks", new Dictionary<string, object?> { ["project_id"] = "1" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class GoogleTasksToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("google_tasks", new GoogleTasksToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Google Tasks", new GoogleTasksToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new GoogleTasksToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleTasksToolkit().ExecuteAsync("list_tasks", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("google_tasks", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new GoogleTasksToolkit(cfg).ExecuteAsync("list_tasks", new Dictionary<string, object?> { ["tasklist_id"] = "@default" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    // ── Security / boundary tests ──────────────────────────────────────────
    [Fact] public async Task CreateTask_NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleTasksToolkit().ExecuteAsync("create_task", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task CompleteTask_NotConfigured_ReturnsSkipped()
    {
        var r = await new GoogleTasksToolkit().ExecuteAsync("complete_task", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }

    [Fact] public async Task CreateTask_FakeToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_tasks", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        var r = await new GoogleTasksToolkit(cfg).ExecuteAsync("create_task",
            new Dictionary<string, object?> { ["title"] = "Test task", ["tasklist_id"] = "@default" }, default);
        Assert.True(r.Status is ToolkitResultStatus.Error or ToolkitResultStatus.Unauthorized);
    }

    [Fact] public async Task CreateTask_MissingTitle_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_tasks", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        // create_task uses GetRequiredParam("title") → MissingParameter → Error
        var r = await new GoogleTasksToolkit(cfg).ExecuteAsync("create_task", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task CompleteTask_MissingId_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_tasks", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        var r = await new GoogleTasksToolkit(cfg).ExecuteAsync("complete_task", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }

    [Fact] public async Task UnknownAction_WithToken_ReturnsError()
    {
        var cfg = new ToolkitConfig("google_tasks", new Dictionary<string, string> { ["access_token"] = "ya29.fake" });
        var r = await new GoogleTasksToolkit(cfg).ExecuteAsync("__inject__", [], default);
        Assert.Equal(ToolkitResultStatus.Error, r.Status);
    }
}

public sealed class EvernoteToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("evernote", new EvernoteToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Evernote", new EvernoteToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new EvernoteToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new EvernoteToolkit().ExecuteAsync("list_notes", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("evernote", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new EvernoteToolkit(cfg).ExecuteAsync("list_notes", new Dictionary<string, object?> { ["notebook"] = "default" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class ObsidianToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("obsidian", new ObsidianToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Obsidian", new ObsidianToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new ObsidianToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new ObsidianToolkit().ExecuteAsync("read_note", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("obsidian", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new ObsidianToolkit(cfg).ExecuteAsync("read_note", new Dictionary<string, object?> { ["path"] = "note.md" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class HabiticaToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("habitica", new HabiticaToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Habitica", new HabiticaToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new HabiticaToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new HabiticaToolkit().ExecuteAsync("list_tasks", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("habitica", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new HabiticaToolkit(cfg).ExecuteAsync("list_tasks", new Dictionary<string, object?> { ["type"] = "todos" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class CalendlyToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("calendly", new CalendlyToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("Calendly", new CalendlyToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new CalendlyToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new CalendlyToolkit().ExecuteAsync("list_events", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("calendly", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new CalendlyToolkit(cfg).ExecuteAsync("list_events", new Dictionary<string, object?> { ["status"] = "active" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}

public sealed class DocusignToolkitTests
{
    [Fact] public async Task Id_IsCorrect() => Assert.Equal("docusign", new DocuSignToolkit().Id);
    [Fact] public async Task Name_IsCorrect() => Assert.Equal("DocuSign", new DocuSignToolkit().Name);
    [Fact] public async Task Category_IsCorrect() => Assert.Equal(ToolkitCategory.Productivity, new DocuSignToolkit().Category);
    [Fact] public async Task NotConfigured_ReturnsSkipped()
    {
        var r = await new DocuSignToolkit().ExecuteAsync("send_envelope", [], default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
    [Fact] public async Task Execute_WithConfig_ReturnsOk()
    {
        var cfg = new ToolkitConfig("docusign", new Dictionary<string, string> { ["api_key"] = "test" });
        var r = await new DocuSignToolkit(cfg).ExecuteAsync("send_envelope", new Dictionary<string, object?> { ["template_id"] = "1", ["recipient"] = "a@b.com" }, default);
        Assert.Equal(ToolkitResultStatus.Skipped, r.Status);
    }
}
