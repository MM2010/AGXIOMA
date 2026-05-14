---
title: MiscRecords
category: entity
tags: [miscrecords, records, metrics, configuration, agent-framework]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MiscRecords

## Summary
The `MiscRecords` file defines a collection of immutable data records used throughout the AGXIOME agent framework for metrics, knowledge management, configuration, and event messaging. These records provide strongly-typed, lightweight data carriers that support observability, search, agent orchestration, and integration with external tools and models.

## Key Details
- **RunMetrics** – Tracks token usage, duration, tool call count, and per-model metrics for a single agent run.
- **ModelMetrics** – Captures provider, model, token counts, inference time, and estimated cost for a specific model invocation.
- **SessionMetrics** – Aggregates totals across multiple runs: tokens, tool calls, cost, and duration.
- **KnowledgeChunk** – Represents a chunk of knowledge with document ID, content, relevance score, optional metadata and embedding.
- **Document** – A base document with ID, content, type (from `DocumentType`), and optional metadata.
- **Chunk** – A text segment with index, offsets, and optional metadata, typically derived from a document.
- **KnowledgeSearchOptions** – Search parameters for knowledge retrieval: top-k, minimum score, metadata filter, search type, and optional tenant ID.
- **NotifyEvent** – A generic event structure with type, source ID, optional payload, and timestamp.
- **ProviderDescriptor** – Describes an LLM provider: name, supported models, API key requirement, local endpoint support, and embedding capability.
- **FewShotExample** – Example pair for few-shot prompting, with optional explanation.
- **AgentTemplate** – Defines an agent template with name, description, category, and optional overrides (system prompt, tools, skills, LLM config, agent config).
- **ReasoningConfig** – Controls reasoning mode (none, internal, external), optional model override, and whether full reasoning is shown.
- **MCPClientOptions** – Configuration for an MCP (Model Context Protocol) client: server name, transport type, command/args, endpoint URI, and headers.

## Cross-References
- [[LLMWIKYConfig]] – Used in AgentTemplate to override LLM settings.
- [[AgentConfig]] – Used in AgentTemplate for config overrides.
- [[DocumentType]] – Enumeration used by Document record.
- [[SearchType]] – Enumeration used by KnowledgeSearchOptions.
- [[MCPTransport]] – Enumeration used by MCPClientOptions.
- [[ReasoningMode]] – Enumeration used by ReasoningConfig.

## Source Files
- `AGXIOME.Shared/Models/MiscRecords.cs` (ingested 2026-05-14)