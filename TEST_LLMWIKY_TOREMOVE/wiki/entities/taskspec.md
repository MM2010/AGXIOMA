---
title: TaskSpec
category: entity
tags: [task-spec, orchestration, immutable, sub-agent, model]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TaskSpec

## Summary
An immutable, self-contained specification for a task assigned by the Master to a sub‑agent. It includes all information the sub‑agent needs to execute the step — natural‑language description, prior context, allowed tools, LLM parameters, skill bindings, and a cryptographic signature for integrity — so the sub‑agent has zero external dependencies.

## Key Details
- Record type (`sealed record`) enforcing immutability.
- Core identifiers: `TaskId`, `PlanId`, `StepIndex`.
- `Description`: natural‑language prompt used directly as the sub‑agent prompt.
- `PriorContext`: synthesized context from prior steps, injected by [[AdvancePlanTool]].
- `AllowedToolNames`: exact allowlist of tools the sub‑agent may invoke.
- LLM parameters: `Temperature` (default 0.7), `TopK`, `TopP`, `MaxTokens` (4096), `MaxIterations` (10).
- `PreferredLLMProvider`: optional provider shorthand (e.g. `"openai:gpt-4o"`).
- `SystemPrompt`: system‑level instructions set by [[GenerateAgentsTool]].
- `Metadata`: arbitrary key‑value bag for master‑to‑agent forwarding.
- Status tracked via [[OrchestratorTaskStatus]] (default `Pending`).
- Timestamps: `CreatedAt` (UTC auto), `StartedAt`, `Timeout`.
- Skill integration: `SkillIds` – applied by [[SpawnAgentTool]] via `ISkillManager.ApplySkillToAgent`.
- Integrity: `TaskSpecSignature` – ECDSA/HMAC signature computed by [[SpawnAgentTool]] using `IAgxiomeCrypto`; empty string in dev/test.

## Cross-References
- [[OrchestratorTaskStatus]] — status lifecycle for task execution
- [[SpawnAgentTool]] — tool that consumes TaskSpec and dispatches the sub‑agent
- [[AdvancePlanTool]] — injects `PriorContext` into the spec
- [[GenerateAgentsTool]] — generates the `SystemPrompt` value

## Source Files
- `AGXIOME.Orchestration/Models/TaskSpec.cs` (ingested 2026-05-14)