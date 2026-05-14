---
title: SkillsShClient
category: entity
tags: [http-client, caching, marketplace, skills, sealed-class]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SkillsShClient

## Summary
`SkillsShClient` is a sealed HTTP client for the Skills.sh marketplace API, providing search, retrieval, and category-listing functionality. It includes an in-memory cache with a 1-hour time-to-live and gracefully handles transient failures by returning empty results or `null`. The implementation respects `CancellationToken` and allows configuration of the base URL for testing or alternative endpoints.

## Key Details
- Sealed class implementing `IDisposable`; disposes the underlying `HttpClient`.
- Default timeout of 10 seconds; configurable via injected `HttpClient`.
- In-memory cache stored in a `ConcurrentDictionary<string, (SkillDefinition Def, DateTime Expires)>` with 1-hour TTL.
- `BaseUrl` property defaults to `https://api.skills.sh/v1` and is settable (e.g., for mocking).
- `SearchAsync(string query, int limit = 10, CancellationToken ct)` – returns `IReadOnlyList<SkillDefinition>`, empty list on HTTP failure.
- `GetSkillAsync(string skillId, CancellationToken ct)` – checks cache first, fetches if expired/missing, returns `null` on failure.
- `ListCategoriesAsync(CancellationToken ct)` – returns `IReadOnlyList<string>`, empty on failure.
- All public methods catch `HttpRequestException` to prevent exceptions from propagating.

## Cross-References
- [[SkillDefinition]] — the model type used for search results and cached skill definitions.
- [[AGXIOME.DynamicRuntime]] — the namespace context for the runtime skills infrastructure.

## Source Files
- `AGXIOME.DynamicRuntime/Skills/SkillsShClient.cs` (ingested 2026-05-14)