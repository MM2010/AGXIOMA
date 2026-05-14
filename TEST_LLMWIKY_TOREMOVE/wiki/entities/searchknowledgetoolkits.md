---
title: Search & Knowledge Toolkits
category: entity
tags: [toolkit, search, knowledge, web-search, integration]
source_count: 5
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Search & Knowledge Toolkits

## Summary
The Search & Knowledge Toolkits provide a unified interface for performing web searches and retrieving external knowledge from multiple providers: Google Custom Search, Bing Web Search, SearXNG (self-hosted), Tavily AI Search, and Exa Neural Search. Each toolkit shares a common `ToolkitBase` abstraction, requiring API keys or endpoint URLs as configuration, and returns structured results (title, URL, snippet). They enable agents to augment answers with real-time or semantic search results.

## Key Details
- **ID pattern**: `google_search`, `bing_search`, `searxng`, `tavily`, `exa`, etc.
- **Category**: `ToolkitCategory.SearchKnowledge` – all toolkits in this group.
- **Configuration**: Each toolkit requires specific settings (e.g., `api_key`, `cx`, `base_url`) stored in `ToolkitConfig` and retrieved via `GetSetting()`.
- **Execution**: `ExecuteCoreAsync` accepts a `query` parameter (and optional `num`/`count`/`max_results`) and returns a `ToolkitResult` containing an array of result objects.
- **Shared HTTP client**: A file-scoped `Http` class provides a single `HttpClient` with a 30-second timeout.
- **Google Search** (`google_search`): Requires `api_key` and `cx` (custom search engine ID). Supports `num` parameter (default 10).
- **Bing Search** (`bing_search`): Requires `api_key` (sent as `Ocp-Apim-Subscription-Key` header). Supports `count` parameter (default 10).
- **SearXNG** (`searxng`): Self-hosted – requires `base_url`. Supports `format` (default `json`).
- **Tavily** (`tavily`): Requires `api_key`. Supports `search_depth` (basic/advanced), `max_results`, and `include_answer`.
- **Exa** (`exa`): Requires `api_key` (sent as `x-api-key` header). Supports `num_results`, `use_autoprompt`, and `type`.
- Each toolkit implements `IsConfigured` to verify required settings are present before execution.

## Cross-References
- [[toolkitbase]] — base class for all toolkits
- [[toolkitconfig]] — configuration container for API keys and endpoints
- [[toolkitresult]] — result type returned by all toolkit executions
- [[entities/toolkits]] — overview of the toolkit system

## Source Files
- `AGXIOME.Toolkits/SearchKnowledge/SearchKnowledgeToolkits.cs` (ingested 2026-05-14)