---
title: TaskResult
category: entity
tags: [result, orchestration, agent, hitl, audit]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TaskResult

## Summary
An immutable record produced by a sub-agent upon task completion, encapsulating the outcome, output, and metadata for a single step in a plan. `TaskResult` is serialised to JSON for both hot store and canonical store, and includes support for Human-in-the-Loop (HITL) pauses and integrity verification via HMAC. It is consumed by tools such as `AdvancePlanTool`, `PollResultsTool`, and `SaveResultTool`.

## Key Details
- **Immutable record** (`sealed record`) with init-only properties.
- **Core fields:** `TaskId`, `PlanId`, `Success` (bool), `Output` (string), `Summary` (nullable string), `Data` (nullable dictionary), `ErrorMessage`, `ErrorType`.
- **Retry and timing:** `RetryCount` (int), `CompletedAt` (default `UtcNow`), `ElapsedTime` (`TimeSpan`).
- **Budget tracking:** `TokensUsed` (nullable int).
- **HITL pause support:** `IsHitlPaused` and `HitlRequirementId` enable sub-agents to signal a pause for human review. After resolution, a resumed result has `IsHitlPaused = false`.
- **Audit integrity:** `ResultHmac` is an HMAC-SHA256 hex string computed by `SaveResultTool` using `IAgxiomeCrypto`. Empty in dev/test. Verified by the Replay Worker.
- `Output` holds raw sub-agent output (free-text or structured JSON). `Summary` is a concise synthesis used by `AdvancePlanTool` for `PriorContext`.
- `Data` exports typed key-value pairs for downstream tasks.

## Cross-References
- [[AdvancePlanTool]] — consumes `Summary` to build `PriorContext` for subsequent steps.
- [[PollResultsTool]] — transitions a plan to `WaitingForHITL` when `IsHitlPaused` is true.
- [[SaveResultTool]] — serialises the result and computes `ResultHmac`.
- [[IHitlCompletionSource]] — publishes a resumed `TaskResult` after HITL resolution.
- [[IAgxiomeCrypto]] — used to compute the HMAC integrity hash.
- [[Replay Worker]] — verifies `ResultHmac` during replay.
- [[Plan]] — the plan to which each result belongs via `PlanId`.

## Source Files
- `AGXIOME.Orchestration/Models/TaskResult.cs` (ingested 2026-05-14)