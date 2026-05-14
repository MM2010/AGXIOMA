---
title: SocialAlgorithmExecutor
category: entity
tags: [team, execution, social-algorithm, agent-routing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SocialAlgorithmExecutor

## Summary
The `SocialAlgorithmExecutor` class enables custom callable logic to drive team execution. It accepts a function that receives an objective and a list of agents and returns a `TeamResponse`, mirroring the Swarms `SocialAlgorithm(callable)` pattern. The static `SocialAlgorithms` class provides pre-built strategies like **RoundRobin** (sequential, context-building) and **Debate** (multi‑round argument and rebuttal).

## Key Details
- `SocialAlgorithmExecutor` is a sealed class that stores a `Func<string, IReadOnlyList<IAgxiomeAgent>, CancellationToken, Task<TeamResponse>>` callable.
- Constructor throws `ArgumentNullException` if callable is `null`.
- The single public method `ExecuteAsync` invokes the stored callable with the given objective, agents, and cancellation token.
- `SocialAlgorithms.RoundRobin`: each agent runs in order, with the previous agent’s output appended as context.
- `SocialAlgorithms.Debate`: two rounds — first each agent takes a position, then all agents see all views and produce rebuttals/syntheses.
- Both pre-built algorithms build a `TeamResponse` with `TeamMode.SocialAlgorithm`, a list of `AgentResult`, and `TeamMetadata`.
- Agents are expected to implement `IAgxiomeAgent` and respond to `AgentRequest`.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used by all team executors
- [[TeamResponse]] — the result type returned by `ExecuteAsync`
- [[AgentResult]] — individual agent output within a `TeamResponse`
- [[TeamMetadata]] — metadata (count, duration) included in the response
- [[Teams Overview]] — conceptual page on team execution modes

## Source Files
- `AGXIOME.Core/Teams/SocialAlgorithmExecutor.cs` (ingested 2026-05-14)