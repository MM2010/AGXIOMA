---
title: ISimpleDynamicAgent
category: entity
tags: [interface, agent, dynamic, contract]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ISimpleDynamicAgent

## Summary
`ISimpleDynamicAgent` defines the minimal contract every dynamic agent (whether user-dropped or LLM-generated) must implement. It deliberately avoids dependencies on the `AGXIOME.Shared` assembly to maximize portability across runtimes.

## Key Details
- Interface in namespace `AGXIOME.DynamicRuntime.Interfaces`.
- Requires three members: `AgentId`, `Description`, and `RunAsync`.
- `AgentId` (string): unique identifier supplied by the [[AgxiomeAgent]] attribute.
- `Description` (string): human-readable description also from the attribute.
- `RunAsync(string input, CancellationToken ct = default)`: executes the agent with text input and returns text output asynchronously.
- Designed to be lightweight and self-contained; no base class or additional dependencies.
- Purpose: enables seamless integration of third-party or generated agents into the dynamic runtime.

## Cross-References
- [[IAgxiomeAgent]] — the core agent interface in AGXIOME.Shared (contrast with this portable contract)
- [[DynamicAgentRuntime]] — the runtime that instantiates and invokes `ISimpleDynamicAgent` implementations

## Source Files
- `AGXIOME.DynamicRuntime/Interfaces/ISimpleDynamicAgent.cs` (ingested 2026-05-14)