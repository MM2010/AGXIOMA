---
title: AgentCoreTests
category: entity
tags: [testing, unit tests, AGXIOME core]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentCoreTests

## Summary
The `AgentCoreTests` class contains unit tests for the central components of the AGXIOME agent framework, including the agent factory, tool registry, guardrails, human-in-the-loop manager, session state, and tool schema generation. These tests verify correct behavior, isolation, and error handling of core services that every agent depends on.

## Key Details
- `AgentFactory.CreateTestAgent` returns agents with the specified `Id` and an initial `AgentStatus.Idle`.
- `ToolRegistry` supports global tool registration and per‑agent tool isolation; `GetAgentTools` returns only tools assigned to that agent.
- `PIIDetectionGuardrail` correctly blocks strings containing Social Security numbers (SSN format) and allows clean input.
- `HITLManager.CreateRequirement` creates a pending requirement with a specific `HITLRequirementType`; `Resolve` updates its status to `Approved` (or `Rejected`).
- `SessionState` stores and retrieves typed values, and `ToDictionary` returns a snapshot copy.
- `TestTool` (a sample `AgxiomeFunction`) demonstrates the tool schema generation, producing valid JSON with `name` and `parameters`.
- All tests use xUnit `[Fact]` attributes and are synchronous except for the guardrail tests (async).

## Cross-References
- [[AgentFactory]] — Creates agents tested here.
- [[ToolRegistry]] — Registration and isolation tested.
- [[PIIDetectionGuardrail]] — PII validation tested.
- [[HITLManager]] — Requirement lifecycle tested.
- [[SessionState]] — Key‑value state tested.
- [[ToolSchema]] — JSON schema generation tested.

## Source Files
- `AGXIOME.Tests/AgentCoreTests.cs` (ingested 2026-05-14)