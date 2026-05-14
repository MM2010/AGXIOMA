---
title: IAgxiomeTeam Interface
category: entity
tags: [interface, team, agents, execution]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgxiomeTeam Interface

## Summary
`IAgxiomeTeam` is the core interface defining a team of agents within the AGXIOME framework. It encapsulates the team’s identity, topology (how agents are organized), mode (e.g., sequential, parallel), and the collection of agents. The interface provides methods to add new agents with optional configuration and to execute team objectives both synchronously and asynchronously via streaming.

## Key Details
- **`Id`**: A string identifier for the team.
- **`Topology`**: Specifies the organization structure (e.g., chain, star) via `TeamTopology`.
- **`Mode`**: Determines execution behavior (e.g., sequential, parallel) via `TeamMode`.
- **`Agents`**: A read-only list of `IAgxiomeAgent` instances belonging to the team.
- **`AddAgent(string name, string role, Action<AgentConfig>? configure = null)`**: Creates a new agent with the given name and role, optionally applying configuration via a delegate; returns the added `IAgxiomeAgent`.
- **`ExecuteAsync(string objective, TeamContext? context = null, CancellationToken ct = default)`**: Executes the team on a given objective, returning a `TeamResponse` asynchronously.
- **`ExecuteStreamAsync(string objective, TeamContext? context = null, CancellationToken ct = default)`**: Streams execution results as an `IAsyncEnumerable<AgentChunk>`, enabling real-time chunked output.

## Cross-References
- [[entities/iagxiomeagent]] — Individual agents managed by the team.
- [[entities/teamtopology]] — Enum for team topology configurations.
- [[entities/teammode]] — Enum for team execution modes.
- [[entities/agentconfig]] — Configuration object used when adding agents.
- [[entities/teamcontext]] — Contextual data passed during execution.
- [[entities/teamresponse]] — Result type returned by synchronous execution.
- [[entities/agentchunk]] — Chunked output streamed during async execution.

## Source Files
- `AGXIOME.Shared/Interfaces/IAgxiomeTeam.cs` (ingested 2026-05-14)