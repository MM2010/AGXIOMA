---
title: AgxiomeTeam
category: entity
tags: [team, agent, execution, mode, topology]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeTeam

## Summary
`AgxiomeTeam` is a sealed class implementing `IAgxiomeTeam` that orchestrates a group of agents in a configurable team structure. It supports multiple operational modes (`Coordinate`, `Route`, `Broadcast`, `Tasks`, `SocialAlgorithm`) each mapping to a specific topology (Router, Swarm, Supervisor). The team can execute an objective synchronously (returning a `TeamResponse`) or stream results from its leader agent, and allows adding agents dynamically via `AddAgent` (using a test agent factory to avoid LLM dependencies in construction).

## Key Details
- Implements `IAgxiomeTeam` (namespace `AGXIOME.Shared.Interfaces`).
- Properties: `Id` (string), `Topology` (enum `TeamTopology`), `Mode` (enum `TeamMode`), `Agents` (read-only list of `IAgxiomeAgent`).
- Constructor requires a `TeamConfig` and a leader agent (`IAgxiomeAgent`). Topology is derived from the mode:
  - `Coordinate` / `Route` → `TeamTopology.Router`
  - `Broadcast` → `TeamTopology.Swarm`
  - `Tasks` / `SocialAlgorithm` → `TeamTopology.Supervisor`
- `AddAgent(name, role, configure?)` creates a test agent via `AgentFactory.CreateTestAgent` (no LLM engine needed) and adds it to the internal `_members` list.
- `SocialAlgorithmCallable` is a custom `Func` delegate required for `SocialAlgorithm` mode; throws if null when that mode is used.
- `ExecuteAsync` dispatches the objective based on `Mode`:
  - `Coordinate`: leader decomposes objective, each member processes a subtask, results are aggregated; optional `ContinueOnPartialFailure` config controls exception handling.
  - `Route`: forwards to first member.
  - `Broadcast`: runs all members concurrently via `Task.WhenAll`.
  - `SocialAlgorithm`: invokes the callable delegate and returns its result directly.
- `ExecuteStreamAsync` yields `AgentChunk` from the leader’s streaming method.
- Uses `Stopwatch` to track elapsed time per agent execution.
- `TeamResponse` includes concatenated outputs, mode, individual `AgentResult` list, and `TeamMetadata` (counts, duration).

## Cross-References
- [[IAgxiomeTeam]] — the interface implemented by AgxiomeTeam.
- [[TeamConfig]] — configuration object used to set mode and partial-failure behavior.
- [[AgentFactory]] — used in `AddAgent` to create a test agent.
- [[TeamMode]] — enum defining the five operational modes.
- [[TeamTopology]] — enum for team topology (Router, Swarm, Supervisor).
- [[TeamResponse]] — the result object returned by `ExecuteAsync`.
- [[AgentChunk]] — streaming response unit yielded by `ExecuteStreamAsync`.

## Source Files
- `AGXIOME.Core/Teams/AgxiomeTeam.cs` (ingested 2026-05-14)