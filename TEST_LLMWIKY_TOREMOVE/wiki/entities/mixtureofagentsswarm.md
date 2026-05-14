---
title: MixtureOfAgentsSwarm
category: entity
tags: [agent, swarm, pattern, mixture of agents]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MixtureOfAgentsSwarm

## Summary
The `MixtureOfAgentsSwarm` class implements a layered multi-agent reasoning pattern inspired by arxiv 2406.04692 (Mixture of Agents). It orchestrates multiple [[IAgxiomeAgent]] instances across configurable layers — each layer runs agents in parallel (or sequentially) and the combined outputs become context for the next layer. A final agent can aggregate the last layer into a single authoritative response. This swarm supports flexible strategies (parallel, sequential, hybrid), multiple aggregation modes (vote, merge, critic synthesis, weighted score), per-layer temperature control, real‑time callbacks, and automatic fallback on agent failures. It is designed to improve response quality by leveraging diverse agent perspectives and iterative refinement.

## Key Details
- **Namespace:** `AGXIOME.Core.Patterns`
- **Source File:** `AGXIOME.Core/Patterns/MixtureOfAgentsSwarm.cs`
- **Constructor:** Accepts `IReadOnlyList<IAgxiomeAgent> agents`, optional `MoAConfig` (defaults to `new MoAConfig()`), and optional `IAgxiomeAgent? finalAgent`.
- **Core Method:** `RunAsync(string task, CancellationToken ct = default)` returns `MoAResult` containing final output, layer results, duration, and total agent calls.
- **Batch Support:** `RunBatchedAsync` runs multiple tasks with a concurrency cap; `RunConcurrentlyAsync` runs all tasks fully concurrent.
- **Layer Execution (`ExecuteLayerAsync`):**
  - Applies per-layer temperature from `MoAConfig.LayerTemperatures` or uses `DefaultTemperature`.
  - Under `LayerStrategy.Hybrid`, even-numbered layers use half the agents.
  - `LayerStrategy.Sequential` feeds each agent’s output as context to the next; `Parallel` (default) runs all agents simultaneously.
- **Aggregation Modes:**
  - `Vote`: picks the most frequent response (by first 100 characters).
  - `WeightedScore`: concatenates all outputs (placeholder — full implementation uses weighting logic).
  - `CriticSynth` and `Merge` are referenced but not shown in snippet; typical implementations involve critic feedback or merging.
- **Final Agent:** If provided, a dedicated agent synthesises all last‑layer outputs into a definitive answer; otherwise the built‑in aggregation mode is used.
- **Error Handling:** `ContinueOnAgentFailure` (in `MoAConfig`) controls whether a failing agent halts the layer.
- **Callback:** `OnLayerComplete` in `MoAConfig` allows real‑time monitoring per layer.
- **Id:** Each swarm instance has a short unique ID (first 8 hex chars of a GUID).

## Cross-References
- [[IAgxiomeAgent]] — the agent interface that swarm orchestrates.
- [[MoAConfig]] — configuration object driving layers, strategies, temperatures, and callbacks.
- [[MoAResult]] — result type returned by the swarm.
- [[MoALayerResult]] — per‑layer result containing agent outputs.
- [[AgentLayerOutput]] — output from a single agent within a layer.
- [[AggregationMode]] — enum defining how outputs are combined (Vote, Merge, CriticSynth, WeightedScore).
- [[LayerStrategy]] — enum for execution order (Parallel, Sequential, Hybrid).

## Source Files
- `AGXIOME.Core/Patterns/MixtureOfAgentsSwarm.cs` (ingested 2026-05-14)