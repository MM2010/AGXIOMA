---
title: AgxiomeMetrics
category: entity
tags: [metrics, telemetry, monitoring]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeMetrics

## Summary
`AgxiomeMetrics` is a static class in the `AGXIOME.Telemetry` namespace that defines all OpenTelemetry metrics for the AGXIOME agent framework. It exposes counters, histograms, and up-down counters covering agent runs, tool calls, guardrail hits, human-in-the-loop pauses, scheduling, LLM latency, RAG search latency, storage operations, and X-Link data transfer — enabling comprehensive observability of framework components.

## Key Details
- All metrics are associated with a single `Meter` named `"AGXIOME.Core"` version `"1.0.0"`.
- **Counters** (monotonically increasing):
  - `agxiome.agent.runs` — number of agent executions
  - `agxiome.agent.errors` — number of agent errors
  - `agxiome.agent.tool_calls` — number of tool invocations
  - `agxiome.guardrail.hits` — guardrail violations
  - `agxiome.hitl.pauses` — human-in-the-loop pauses
  - `agxiome.schedule.runs` — scheduler executions
  - `agxiome.xlink.bytes_sent` — bytes transmitted over X-Link (uses `long`)
- **Histograms** (distribution measurements):
  - `agxiome.agent.run.duration` (ms) — agent execution time
  - `agxiome.llm.latency` (ms) — LLM inference time
  - `agxiome.llm.tokens` (tokens) — tokens per inference
  - `agxiome.rag.search.latency` (ms) — RAG semantic search time
  - `agxiome.storage.write.latency` (ms) — storage write time
  - `agxiome.storage.read.latency` (ms) — storage read time
  - `agxiome.xlink.latency` (ms) — X-Link communication latency
- **UpDownCounters** (can increase or decrease):
  - `agxiome.agent.active` — current active agents
  - `agxiome.workflow.active` — current active workflows
- All metric names follow a consistent `agxiome.<component>.<metric>` naming convention.
- Designed for use with .NET `System.Diagnostics.Metrics` and OpenTelemetry exporters.

## Cross-References
- [[Telemetry]] — overview of observability in AGXIOME
- [[System.Diagnostics.Metrics]] — underlying .NET instrumentation API

## Source Files
- `AGXIOME.Telemetry/AgxiomeMetrics.cs` (ingested 2026-05-14)