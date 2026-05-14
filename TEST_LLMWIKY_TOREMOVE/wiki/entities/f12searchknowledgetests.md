---
title: F12 Search Knowledge Tests
category: entity
tags: [tests, search-knowledge, toolkits]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Search Knowledge Tests

## Summary
This test suite validates the identity, configuration handling, and basic execution of all search-knowledge toolkits in the AGXIOME framework. Each toolkit is verified for correct `Id`, `Name`, `Category`, a skipped result when unconfigured, and a successful execution when provided with a minimal configuration. The tests ensure consistency across the `SearchKnowledge` toolkit category.

## Key Details
- Eight main toolkit test classes: `GoogleSearchToolkitTests`, `BingToolkitTests`, `SearXNGToolkitTests`, `TavilyToolkitTests`, `ExaToolkitTests`, `BraveToolkitTests`, `SerpApiToolkitTests`, `SerperToolkitTests` (and likely `FirecrawlToolkitTests`)
- Each test class verifies:
  - Id matches expected string (e.g., `"google_search"`)
  - Name matches expected display name (e.g., `"Google Search"`)
  - Category equals `ToolkitCategory.SearchKnowledge`
  - Unconfigured toolkit returns `ToolkitResultStatus.Skipped`
  - Configured toolkit (with at least `api_key`) returns `ToolkitResultStatus.Ok`
- Tests use `ToolkitConfig` with a dictionary of string keys/values and execute the `"search"` operation with a `query` parameter.
- Parameter names vary per toolkit: e.g., SerpApi and Serper use `"q"` instead of `"query"`.
- All tests are `[Fact]` and asynchronous.

## Cross-References
- [[entities/google_search_toolkit]] – Google Search toolkit tested here
- [[entities/bing_search_toolkit]] – Bing Search toolkit tested here
- [[entities/searxng_toolkit]] – SearXNG toolkit tested here
- [[entities/tavily_toolkit]] – Tavily toolkit tested here
- [[entities/exa_toolkit]] – Exa toolkit tested here
- [[entities/brave_search_toolkit]] – Brave Search toolkit tested here
- [[entities/serpapi_toolkit]] – SerpAPI toolkit tested here
- [[entities/serper_toolkit]] – Serper toolkit tested here
- [[entities/firecrawl_toolkit]] – Firecrawl toolkit tested here (if present)
- [[concepts/toolkit_category]] – Documents the `ToolkitCategory` enum

## Source Files
- `AGXIOME.Tests/F12/F12SearchKnowledgeTests.cs` (ingested 2026-05-14)