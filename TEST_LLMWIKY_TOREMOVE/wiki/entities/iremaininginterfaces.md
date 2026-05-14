---
title: IRemainingInterfaces
category: entity
tags: [interface, skills, security, xlink, audit, agent-context]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IRemainingInterfaces

## Summary
The `IRemainingInterfaces` file in `AGXIOME.Shared` consolidates several foundational contracts that did not fit into larger provider or agent interfaces. It defines the skill system (skill orchestration, loading, and packaging), human‑in‑the‑loop completion notification, audit logging, cryptographic primitives, cross‑link (XLink) routing, and agent context. These interfaces are designed to be implemented by pluggable components across different layers of the AGXIOME framework.

## Key Details
- **Skill System** – `IAgxiomeSkill`, `ISkillManager`, and `ISkillLoader` form a complete skill lifecycle: definition, discovery, loading, agent binding, and remote registry access.
- **Skill Metadata** – `SkillScript`, `SkillDocRef`, `SkillManifest`, and `SkillPackage` are immutable records for scripts, references, remote manifests, and on‑disk packages.
- **HITL Completion** – `IHitlCompletionSource` provides a decoupled notification contract for resolving paused human‑in‑the‑loop requirements.
- **Audit Logging** – `IAuditLogger` defines a consistent interface for logging and querying audit events.
- **Cryptography** – `IAgxiomeCrypto` exposes key pair generation, signing, verification, and HMAC operations.
- **XLink Routing** – `IXLinkRouter` abstracts message send, publish, request/response, and subscription patterns for cross‑link communication.
- **Agent Context** – `IAgentContext` holds system prompt, introduction message, few‑shot examples, instructions, and additional context for agent configuration.

## Cross-References
- [[IAgxiomeAgent]] – Skills are bound to agents via `ISkillManager.ApplySkillToAgent`.
- [[IProviderInterfaces]] – `IChunker`, `IEmbedder`, and `IVectorStore` are referenced in comments as defined there.
- [[IHitlManager]] – Likely consumes `IHitlCompletionSource` to resume workflows.
- [[IAgxiomeSkill]] – Core skill contract, part of the skill system.

## Source Files
- `AGXIOME.Shared/Interfaces/IRemainingInterfaces.cs` (ingested 2026-05-14)