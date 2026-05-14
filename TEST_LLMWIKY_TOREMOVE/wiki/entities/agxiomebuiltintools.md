---
title: AgxiomeBuiltinTools
category: entity
tags: [tools, builtin-tools, http-fetch, web-search, file-save, IAgxiomeExecutableTool]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeBuiltinTools

## Summary
The `AgxiomeBuiltinTools` namespace provides a set of real, executable tools (`HttpFetchTool`, `WebSearchTool`, `FileSaveTool`) that implement the `IAgxiomeExecutableTool` interface. These tools are designed for dynamic agent instantiation: each exposes an OpenAI-compatible `ToJsonSchema()` for LLM function calling, receives parameters via `ToolContext.Parameters`, and returns results as `ToolResult.Data`. Together they enable agents to fetch web pages, perform DuckDuckGo searches, and save files to disk.

## Key Details
- Base class `AgxiomeTool` abstracts schema generation (`BuildSchema`) and parameter extraction (`Param`), plus HTML stripping (`StripHtml`).
- `HttpFetchTool` (name: `http_fetch`) – HTTP GET a URL, returns plain text (HTML removed). Parameters: `url` (required), `max_chars` (optional, default 3000, max 8000). Uses a custom `HttpClient` with User‑Agent, Accept headers, and consent‑bypass cookies.
- `WebSearchTool` (name: `web_search`) – Searches DuckDuckGo HTML and returns top‑N results (title, URL, snippet). Parameters: `query` (required), `num_results` (optional, default 5, max 10). Implements fallback engine logic.
- `FileSaveTool` (name: `file_save`) – Saves text content to a local file and returns the absolute path. Provides persistent storage for agent outputs.
- All tools return a `ToolResult` with success flag, data string, and optional metadata (`ToolMetadata`).
- Designed to be registered with the agent runtime and invoked automatically when the LLM selects them.

## Cross-References
- [[IAgxiomeExecutableTool]] — interface contract all builtin tools implement
- [[ToolContext]] — passed to `ExecuteAsync`, contains parameters populated by LLM
- [[ToolResult]] — return type that feeds results back as a `tool_result` message
- [[AgxiomeCLIDemo]] — demo context where these tools are used

## Source Files
- `AGXIOME.CLI/Demo/Tools/AgxiomeBuiltinTools.cs` (ingested 2026-05-14)