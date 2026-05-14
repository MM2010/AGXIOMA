--- 
title: Governance Storage Records
category: entity
tags: [governance, storage, records, data-models]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Governance Storage Records

## Summary
The `GovernanceStorageRecords` source file defines a comprehensive set of shared data models for governance, storage, and record-keeping within the AGXIOME agent framework. These records encapsulate guardrails, hooks, human-in-the-loop (HITL) requirements, approvals, audit events, memory entries, user profiles, session contexts, learned knowledge, decision logs, culture entries, skill references, schedules, and cross-link envelopes. Together they form the backbone for tracking, enforcing, and persisting agent governance and state across the system.

## Key Details
- `GuardrailContext` and `GuardrailResult` model guardrail evaluation inputs and outputs, including optional severity overrides.
- `HookContext` captures full execution context at various hook points (e.g., input, output, error) and includes session state.
- `HITLRequirement` and `ApprovalRequest`/`ApprovalResolution` model human-in-the-loop and approval workflows with configurable timeouts, audit trails, and orchestrator pause/resume support.
- `AuditEvent` and `AuditQuery` define a structured audit log with actor, action, success/failure, and temporal filtering.
- `MemoryEntry`, `UserProfile`, `EntityMemory`, `SessionContext`, `Goal` provide storage schemas for user and entity memory, session state, and goal tracking.
- `LearnedKnowledge` stores content with optional embeddings and topic tags for retrieval.
- `DecisionLogEntry` records agent decisions with reasoning and context.
- `CultureEntry` captures organizational or agent culture data with priority and category.
- `SkillRef` is a lightweight reference to a skill, optionally parameterized.
- `Schedule`, `SchedulePayload`, `ScheduleRun` support cron-based scheduling of workflows and plan orchestrations.
- `XLinkEnvelope`, `XLinkHeader`, `XLinkOptions` define a cross-link message format with checksums, signatures, compression, and retry logic.

## Cross-References
- [[Guardrails]] — uses `GuardrailContext` and `GuardrailResult` for validation.
- [[HumanInTheLoop]] — relies on `HITLRequirement` and `ApprovalRequest` for orchestration.
- [[Auditing]] — `AuditEvent` and `AuditQuery` are the core audit data models.
- [[MemorySystem]] — consumes `MemoryEntry`, `EntityMemory`, `LearnedKnowledge`, and `UserProfile`.
- [[Scheduling]] — `Schedule` and `ScheduleRun` drive scheduled task execution.
- [[XLinkMessaging]] — `XLinkEnvelope` and options define the cross-link protocol.
- [[SessionManagement]] — `SessionContext` and `HookContext.SessionState` track session lifecycle.

## Source Files
- `AGXIOME.Shared/Models/GovernanceStorageRecords.cs` (ingested 2026-05-14)