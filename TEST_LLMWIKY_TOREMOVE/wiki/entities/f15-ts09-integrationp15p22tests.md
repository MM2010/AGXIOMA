---
title: F15 TS09 Integration P15–P22 Tests
category: entity
tags: [tests, integration, hitl, skills, notifier, crypto, metrics, scheduling, xlink]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15 TS09 Integration P15–P22 Tests

## Summary
This test suite (22 tests) validates the integrations introduced in P15 (HITL), P16 (Skills), P19 (SignalR notifier), P20 (Crypto HMAC), P21 (Metrics), P17 (Schedule adapter), and P18 (XLink envelope). All tests are self-contained using in-memory fakes and require no external services. They cover HITLManager standalone and bridge modes, HITL pause handling in PollResultsTool, persistence of HITL flags in SaveResultTool, and behavior of InMemoryCompletionNotifier.

## Key Details
- **HITLManager standalone**: Creates and resolves requirements without an orchestrator context; status updates correctly.
- **HITLManager with IHitlCompletionSource**: Resolving approved/rejected requirements notifies the bridge asynchronously.
- **InMemoryCompletionNotifier**: Correctly delivers resumed results after HITL pause; validates TaskResult fields.
- **HITLRequirement new fields**: `OrchestratorTaskId` and `OrchestratorPlanId` default to null.
- **PollResultsTool HITL handling**: Paused results are ignored until a resumed result arrives; only resumed result counts as completion.
- **SaveResultTool HITL flag**: Persists `IsHitlPaused` and `HitlRequirementId` in the cold store.
- **TaskResult defaults**: `IsHitlPaused` is false, `HitlRequirementId` is null by default.
- Additional tests (not shown in snippet) cover P16–P22 integrations (Skills, SignalR notifier, Crypto HMAC, Metrics, Schedule adapter, XLink envelope).

## Cross-References
- [[HITLManager]] — core component tested for requirement lifecycle and bridge notification
- [[PollResultsTool]] — tested for filtering HITL-paused task results
- [[SaveResultTool]] — tested for persisting HITL pause flag
- [[InMemoryCompletionNotifier]] — in-memory implementation used in tests
- [[InMemoryHotStore]] / [[InMemoryOrchestrationStore]] — fake stores used in test infrastructure

## Source Files
- `AGXIOME.Tests/F15/F15_TS09_IntegrationP15P22Tests.cs` (ingested 2026-05-14)