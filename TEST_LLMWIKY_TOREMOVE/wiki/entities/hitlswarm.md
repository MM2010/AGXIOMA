---
title: HITLSwarm
category: entity
tags: [hitl, swarm, patterns, human-in-the-loop]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# HITLSwarm

## Summary
HITLSwarm is a Human-In-The-Loop (HITL) swarm orchestration pattern unique to AGXIOME. It enables any agent in the swarm to trigger a HITL checkpoint before executing a sensitive tool or after producing an output that matches predefined patterns. When a checkpoint is triggered, the swarm pauses, serialises its state to an in-memory store (Redis-ready), and waits for human approval via a configurable delegate or polling. On approval, execution resumes exactly from the pause point. This pattern provides safety and auditability for autonomous multi-agent workflows.

## Key Details
- **Class**: `sealed class HITLSwarm`
- **Constructor**: accepts `IReadOnlyList<IAgxiomeAgent>` agents and optional `HITLSwarmConfig`.
- **RunAsync**: runs all agents sequentially; returns `HITLSwarmResult` with per-agent results, duration, and pause counts.
- **Pre-run checkpoint**: configurable per agent via `AgentsRequiringPreApproval` — pauses before the agent runs.
- **Post-run checkpoint**: triggered when agent output matches any pattern in `SensitiveOutputPatterns`.
- **WaitForApprovalAsync**: uses `ApprovalDelegate` if provided, otherwise polls `HITLManager` with a configurable timeout (`HumanReviewTimeout`, default 5 min).
- **Auto-reject**: if no human response within timeout, the requirement is automatically rejected.
- **Config options**: `HumanReviewTimeout`, `AbortOnRejection` (stop swarm on first rejection), `OnPauseForHuman`, `OnResumed` callbacks.
- **Resolve method**: external approval/rejection of pending requirements.
- **GetPendingRequirements**: returns unresolved HITL requirements for the current run.

## Cross-References
- [[HITLManager]] — core HITL requirement management used internally by HITLSwarm.
- [[IAgxiomeAgent]] — agent interface that all swarm agents implement.
- [[Patterns Overview]] — patterns category under which this swarm pattern belongs.

## Source Files
- `AGXIOME.Core/Patterns/HITLSwarm.cs` (ingested 2026-05-14)