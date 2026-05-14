---
title: Session, Team, and Workflow Records
category: entity
tags: [session, team, workflow, data-models]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Session, Team, and Workflow Records

## Summary
This file defines the core data structures for managing sessions, team collaboration, and workflow execution within the AGXIOME framework. The records provide strongly-typed, immutable models for session state, runtime context, team responses, agent results, workflow steps, and execution metadata. They serve as the foundational types for all state tracking, context passing, and result aggregation across the framework.

## Key Details
- All models are implemented as `record` types, ensuring value equality and immutability.
- **Session** tracks individual user sessions with identity, type, state (`Dictionary<string, object>`), and metadata (`Dictionary<string, string>`).
- **SessionOptions** controls history enablement, max history messages, and summary generation.
- **RunContext** combines user identity, session state (`ISessionState`), dependencies, metadata, and knowledge filters for runtime execution.
- **TeamResponse** aggregates a team’s output content, the mode used (`TeamMode`), individual agent results, and optional metadata.
- **AgentResult** records each agent’s ID, role, status (`AgentStatus`), output, and execution time.
- **TeamContext** holds a session ID, shared state, and optional global input for team execution.
- **TeamMetadata** captures the number of members used, total duration, and total tool calls.
- **WorkflowResult** groups step results with a workflow ID, status (`WorkflowStatus`), and metadata.
- **StepResult** captures individual step name, status (`StepStatus`), output, execution time, and optional error message.
- **WorkflowContext** provides session ID, shared state, and a cancellation token for workflow execution.
- **WorkflowMetadata** records the number of steps executed, total duration, and total inferences.
- **RunOutputEvent** wraps an event (`RunEvent`) with optional content, tool call result, or human-in-the-loop requirement.

## Cross-References
- [[ISessionState]] — interface used by `RunContext` for session state abstraction
- [[TeamMode]] — enum referenced by `TeamResponse.ModeUsed`
- [[AgentStatus]] / [[StepStatus]] / [[WorkflowStatus]] — status enums used in result records
- [[RunEvent]] — base event type for `RunOutputEvent`
- [[ToolCallResult]] — optional payload in `RunOutputEvent`
- [[HITLRequirement]] — optional human-in-the-loop payload

## Source Files
- `AGXIOME.Shared/Models/SessionTeamWorkflowRecords.cs` (ingested 2026-05-14)