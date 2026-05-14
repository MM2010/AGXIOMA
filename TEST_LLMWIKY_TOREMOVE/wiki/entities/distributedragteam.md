---
title: DistributedRagTeam
category: entity
tags: [rag, distributed, fan-out, knowledge-base, parallelism, orchestration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DistributedRagTeam

## Summary
The `DistributedRagTeam` is a core component of the AGXIOME framework that orchestrates parallel knowledge retrieval across multiple registered `IKnowledgeBase` sources. It fans out a search query to all active (or a filtered subset) sources concurrently, merges the results into a unified list ranked by relevance score, and returns a `DistributedRagResult` with per-source breakdown. The implementation uses bounded parallelism and per-source timeouts to prevent resource exhaustion.

## Key Details
- Defined via the `IDistributedRagTeam` interface and implemented by the `DistributedRagTeam` class.
- Uses a factory delegate (`Func<RagSourceDescriptor, IKnowledgeBase>`) to resolve source instances at query time—avoids tight coupling to specific knowledge base implementations.
- Sources are registered with a `RagSourceDescriptor` record (includes Id, Name, Namespace, Status, Metadata).
- Supported source statuses: `Active`, `Paused` (excluded from queries), `Degraded` (included but flagged), `Removed`.
- Query contract `DistributedRagQuery` includes `SearchType` enum (not strings), `TopKPerSource`, `TopKTotal`, `MinScore`, optional `SourceFilter`, and `MetadataFilter`.
- Fan-out uses `Task.WhenAll` in bounded parallel batches (default `MaxParallelSources = 8`); slower sources are soft-cancelled after `SourceTimeout` (default 10 seconds).
- Merged result `DistributedRagResult` contains `Chunks` (unified ranked list), `PerSource` dictionary, counts of sources queried and with results, and total `QueryDuration`.
- Supports dynamic source lifecycle management via `RegisterSourceAsync`, `UnregisterSourceAsync`, `UpdateSourceStatusAsync`, and `ListSourcesAsync`.

## Cross-References
- [[IKnowledgeBase]] — the interface for individual knowledge sources that the team queries.
- [[KnowledgeChunk]] — the unit of retrieved content returned in results.
- [[KnowledgeSearchOptions]] — options passed to underlying search implementations.
- [[SearchType]] — enum used in `DistributedRagQuery` to specify vector, hybrid, or other search modalities.

## Source Files
- `AGXIOME.Core/Storage/DistributedRagTeam.cs` (ingested 2026-05-14)