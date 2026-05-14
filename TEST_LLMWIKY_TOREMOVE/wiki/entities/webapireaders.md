---
title: Web API Readers
category: entity
tags: [reader, HTTP, web API, HttpClient, Zero Trust]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Web API Readers

## Summary
The Web API Readers (R-13 through R-18, R-22, R-27, R-28) are a collection of `IDocumentReader` implementations in AGXIOME.Storage that retrieve documents from remote web sources via HTTP. They rely on an injected `HttpClient` and embody a Zero Trust model—servers are the sole source of truth. Each reader uses a specific URI scheme (e.g., `https://`, `wikipedia://`, `arxiv://`) to identify its target. With the exception of `WebsiteReader`, stream-based reading is not supported and throws `NotSupportedException`.

## Key Details
- All readers implement `IDocumentReader` and use an `HttpClient` field for network calls.
- They follow the Zero Trust principle: no client-side authority; servers are the definitive source.
- `ReadFromStreamAsync` is generally not applicable → throws `NotSupportedException` (except `WebsiteReader`).
- **WebsiteReader** (R-13): Fetches HTML, parses it with HtmlAgilityPack, strips scripts/styles/navigation, extracts headings as sections. Supports stream reading from HTML content.
- **WikipediaReader** (R-14): Uses the Wikipedia REST API (`/api/rest_v1/page/summary`) and the parse API for sections. URI format: `wikipedia://{lang}/{article_title}` or just the title.
- **ArxivReader** (R-15): (from truncated source) Uses the arXiv API; URI format: `arxiv://{id}`. Returns abstract, metadata, and PDF link.
- Additional readers for R-16..R-18, R-22, R-27, R-28 follow the same pattern (not shown in snippet).
- All readers require absolute HTTP/HTTPS URLs or custom URI schemes; no local file paths.
- `SupportedExtensions` is an empty list for all (no file extension association).

## Cross-References
- [[IDocumentReader]] — the interface all Web API readers implement.
- [[AGXIOME.Storage]] — the storage module containing these readers.

## Source Files
- `AGXIOME.Storage/Readers/WebApiReaders.cs` (ingested 2026-05-14)