---
title: AgentStatus
category: entity
tags: [entity, enum, agent lifecycle]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentStatus

## Summary
`AgentStatus` is an enumeration that defines the possible states an agent can be in during its execution lifecycle within the AGXIOME framework. It provides a clear, finite set of statuses to track progression from idle through thinking, tool calling, streaming, potential pauses (human-in-the-loop), errors, and completion.

## Key Details
- Defined as a C# enum in the `AGXIOME.Shared.Models` namespace.
- Values: `Idle`, `Thinking`, `ToolCall`, `Streaming`, `Paused`, `Error`, `Completed`.
- `Paused` is reserved for human-in-the-loop (HITL) scenarios where execution is suspended for user input.
- `Error` indicates an unrecoverable failure; `Completed` marks successful termination.
- Provides a standardised way to monitor and react to agent state transitions.

## Cross-References
- [[entities/agent]] — the agent entity that uses this status enumeration for lifecycle management.

## Source Files
- `AGXIOME.Shared/Models/AgentStatus.cs` (ingested 2026-05-14)