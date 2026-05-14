---
title: ProductivityToolkits
category: entity
tags: [productivity, google-calendar, google-drive, google-docs, google-sheets, toolkit, OAuth2]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ProductivityToolkits

## Summary
The `AGXIOME.Toolkits.Productivity` namespace provides a suite of agent-ready toolkits that integrate Google Workspace services (Calendar, Drive, Docs, Sheets, and others) into the AGXIOMA framework. Each toolkit uses OAuth2 bearer tokens for authentication, shares a common HTTP client with a 30-second timeout, and follows the `ToolkitBase` action‑dispatch pattern. Together they enable agents to read, create, and manage productivity data programmatically.

## Key Details
- All toolkits derive from `ToolkitBase` and require an `access_token` setting (OAuth2 bearer token) to be considered configured.
- Actions are dispatched via a `switch` expression on the action string parameter; unknown actions return a failure result.
- A `file static class Http` provides a shared `HttpClient` instance used by all toolkits for API calls.
- The namespace currently includes 18 toolkits (T‑16 through T‑33). The snippet shows:
  - **GoogleCalendarToolkit** – actions: `list_calendars`, `list_events`, `get_event`, `create_event`. Requires `calendar_id` (defaults to `"primary"`).
  - **GoogleDriveToolkit** – actions: `list_files`, `get_file`, `create_folder`, `delete_file`. Supports query filtering via `q` parameter.
  - **GoogleDocsToolkit** – actions: `get_doc`, `create_doc`. Editing requires a separate `batchUpdate` endpoint.
  - **GoogleSheetsToolkit** – actions: `read_values` (truncated snippet), expects `spreadsheet_id` and `range` parameters.
- Each toolkit maps to a distinct Google API endpoint and uses `GoogleCalendarToolkit.GcGet` / `GcPost` as reusable helpers (shared across toolkits).
- Configuration is passed via `ToolkitConfig`; individual settings are retrieved using `GetSetting`.

## Cross-References
- [[ToolkitBase]] — base class for all toolkits; defines the execution lifecycle and configuration.
- [[ToolkitCategory]] — enumeration that categorizes toolkits (Productivity is one of its values).

## Source Files
- `AGXIOME.Toolkits/Productivity/ProductivityToolkits.cs` (ingested 2026-05-14)