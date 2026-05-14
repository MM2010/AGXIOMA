---
title: Enums
category: entity
tags: [enums, shared models, framework types]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Enums

## Summary
The `AGXIOME.Shared.Models` namespace defines a comprehensive set of enums that represent core domain concepts and configurations within the AGXIOME agent framework. These enums standardize values for guardrails, hook points, learning modes, memory types, human-in-the-loop interactions, scheduling, RAG, search, document processing, reasoning, media, MCP transport, pipeline steps, and merge/compression strategies, ensuring type safety and semantic clarity across the system.

## Key Details
- **GuardrailType**: `Input`, `Output` — discriminates guardrail application direction.
- **GuardrailSeverity**: `Log`, `Flag`, `Block` — escalation levels for guardrail violations.
- **HookPoint**: `PreRun`, `PostRun`, `PreTool`, `PostTool`, `StreamChunk`, `BackgroundPost` — lifecycle hooks for agent execution.
- **LearningMode**: `Always`, `Agentic`, `Propose` — when learning updates occur.
- **LearningSessionMode**: `Summary`, `Planning` — session types for learning.
- **EntityMemoryType**: `Fact`, `Event`, `Relationship` — kinds of stored agent memories.
- **HITLRequirementType**: `Confirmation`, `UserInput`, `ExternalExecution`, `Audit` — human-in-the-loop interaction types.
- **HITLStatus / ApprovalStatus**: `Pending`, `Approved`, `Rejected`, `Expired` — common state machines for approvals.
- **ApprovalType**: `Standard`, `Audit` — approval flow categories.
- **ScheduleStatus / ScheduleRunStatus**: lifecycles for scheduled tasks.
- **RAGMode**: `Agentic`, `Traditional` — retrieval augmented generation strategies.
- **SearchType**: `Vector`, `Keyword`, `Hybrid` — search methods.
- **DocumentType**: `PlainText`, `Markdown`, `Code`, `Pdf`, `Html`, `Csv` — supported document formats.
- **ReasoningMode**: `None`, `ReasoningModel`, `ReasoningAgent`, `ReasoningTools` — reasoning engine selection.
- **MaxIterationsMode**: `Fixed`, `Auto`, `Infinite` — iteration limit control.
- **MemoryBackend**: `InMemory`, `Postgres`, `MarkdownFiles`, `Hybrid` — persistence options.
- **MediaType / MediaSource**: multimedia classification and origin.
- **MCPTransport**: `Stdio`, `SSE`, `StreamableHttp` — Model Context Protocol transport modes.
- **RunEvent**: lifecycle events for run tracking.
- **StepType / StepStatus**: pipeline step kinds and execution states.
- **MergeStrategy**: `Concat`, `Vote`, `Weighted`, `SupervisorMerge` — merging outputs.
- **CompressionLevel**: `None`, `Gzip`, `Brotli` — data compression.
- **CheckTrigger**: `InputNotAllowed`, `OutputNotAllowed` — guardrail check activation triggers.

## Cross-References
- [[entities/guardrails]] — uses `GuardrailType`, `GuardrailSeverity`, `CheckTrigger`.
- [[entities/learning]] — relates to `LearningMode`, `LearningSessionMode`.
- [[entities/human-in-the-loop]] — references `HITLRequirementType`, `HITLStatus`, `ApprovalType`, `ApprovalStatus`.
- [[entities/scheduling]] — depends on `ScheduleStatus`, `ScheduleRunStatus`.
- [[entities/media]] — aligned with `MediaType`, `MediaSource`.
- [[entities/pipeline]] — uses `StepType`, `StepStatus`, `MergeStrategy`.

## Source Files
- `AGXIOME.Shared/Models/Enums.cs` (ingested 2026-05-14)