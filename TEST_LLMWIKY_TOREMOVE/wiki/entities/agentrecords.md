---
title: Agent Records
category: entity
tags: [agent, records, request, response, streaming, swarm, memory, context]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agent Records

## Summary
The `AgentRecords` namespace defines the core data transfer objects (DTOs) for the AGXIOMA agent framework. These records encapsulate the lifecycle of agent execution — from request envelopes and streaming chunks to final responses, memory interactions, and specialized swarm results (Heavy, Grok, Grok-Heavy, Forest). They provide a strongly typed, immutable contract for inter-component communication within the agent runtime.

## Key Details
- **AgentRequest** — Envelope for invoking an agent: contains prompt, optional context, conversation history, and execution options.
- **AgentResponse** — Final result of agent execution: includes content text, status, tool call results, and metadata.
- **AgentChunk** — Streaming chunk for real-time output; carries partial content, optional tool call ID, final/paused flags, and active human-in-the-loop (HITL) requirements.
- **AgentMessage** — Single turn in conversation history; captures role, content, tool call details, and optional media attachments.
- **AutoStopCriteria** — Defines conditions for stopping agent execution (max tokens, confidence threshold, stop phrases, or no-tool-call detection).
- **MediaContent** — Represents inline media (image/audio) with type, base64 data, MIME type, and source indicator.
- **AgentRequestOptions** — Configuration flags for streaming, retry, iteration limit, and timeout.
- **AgentMetadata** — Runtime statistics: inference count, tool call count, duration, and total tokens used.
- **AgentContext** — Injected session/ user context plus dependency provider for the run.
- **AgentInteraction** — Persisted record in agent memory store: agent ID, role, content, tool calls JSON, and timestamp.
- **HeavySwarmResult** — Output of a Heavy-mode swarm: question, research findings, analysis, alternatives, synthesis, and phase timings.
- **GrokSwarmResult** — 4-agent Grok swarm result: outputs from Harper, Benjamin, Lucas plus captain synthesis and timing.
- **GrokHeavySwarmResult** — 16-agent Grok-Heavy swarm: dictionary of specialist outputs, captain synthesis, and timing.
- **ForestSwarmResult** — ForestSwarm routing result: selected tree, agent, output, matched keywords, and duration.

## Cross-References
- [[entities/agent]] — Entity that consumes these records during execution.
- [[entities/swarm]] — Swarm orchestrators produce the specialized swarm result records.
- [[concepts/streaming]] — AgentChunk is the atomic unit of streaming output.
- [[concepts/memory]] — AgentInteraction is the persistence unit for memory stores.

## Source Files
- `AGXIOME.Shared/Models/AgentRecords.cs` (ingested 2026-05-14)