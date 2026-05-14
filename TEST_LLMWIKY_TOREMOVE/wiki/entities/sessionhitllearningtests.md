---
title: SessionHITLLearningTests
category: entity
tags: [testing, HITL, session, learning, skills, scheduling, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SessionHITLLearningTests

## Summary

The `SessionHITLLearningTests` class is a comprehensive test suite covering core components of the AGXIOME .NET agent framework. It validates the behavior of HITL (Human-In-The-Loop) requirement management, session lifecycle, session state isolation, dynamic instruction resolution, dependency provider factory semantics, learning machine auto-save modes, skill manager listing and download error handling, and schedule manager creation, pausing, resuming, and run history. The tests ensure idempotent operations, graceful handling of unknown entities, correct filtering, default value fallbacks, and exception propagation where expected.

## Key Details

- `HITL_DoubleResolve_NoOp`: confirming that resolving an already‑approved HITL requirement a second time (with `false`) is a no‑op and does not change its status (`Approved`).
- `HITL_ResolveUnknown_NoThrow`: resolving a non‑existent requirement does not throw an exception.
- `HITL_GetPendingForRun_FiltersCorrectly`: pending requirements are correctly scoped to their run identifier.
- `SessionManager_CreateAndRetrieve`: sessions can be created and retrieved by ID, preserving the user identifier.
- `SessionManager_Delete_Removes`: deleting a session makes subsequent retrieval return `null`.
- `SessionState_GetUnknown_Default`: accessing a missing key returns `0` for `int` and `null` for `string` (no throw).
- `SessionState_ToDictionary_IsolatedCopy`: `ToDictionary()` returns a deep copy; mutations to the dictionary do not affect the original state.
- `DynamicInstructionResolver_UnknownKey_KeepsPlaceholder`: unresolvable placeholders like `{unknown_key}` are left unchanged in the output.
- `DependencyProvider_FactoryResolvedAtRuntime`: a registered factory is invoked each time its dependency is requested (lazy resettable behaviour).
- `Learning_AlwaysMode_AutoSaves`: when `UserMemoryMode` and `UserProfileMode` are set to `Always`, `ProcessAsync` completes without throwing.
- `SkillManager_ListByCategory_NoMatch_Empty`: requesting skills for a non‑existent category returns an empty list.
- `SkillManager_Download_InvalidUrl_Throws`: downloading from an invalid local URL throws `HttpRequestException`.
- `ScheduleManager_CreateAndDelete`: schedules can be created and then deleted, resulting in an empty list.
- `ScheduleManager_PauseResume`: disabling a schedule sets its status to `Paused`; re‑enabling sets it back to `Active`.
- `ScheduleManager_RunHistory`: a newly created schedule has no run history.

## Cross-References

- [[HITLManager]] — core class for HITL requirement creation and resolution tested here.
- [[SessionManager]] — manages session creation, retrieval, and deletion.
- [[SessionState]] — thread-safe key-value store with default values and isolated copies.
- [[DynamicInstructionResolver]] — resolves placeholders using session state; keeps unknown placeholders intact.
- [[DefaultDependencyProvider]] — runtime factory registration and resolution.
- [[LearningMachine]] — processes learning events based on configurable modes.
- [[SkillManager]] — lists and downloads skills; handles missing categories and invalid URLs.
- [[ScheduleManager]] — creates, pauses, resumes, and deletes schedules; retrieves run history.

## Source Files

- `AGXIOME.Tests/SessionHITLLearningTests.cs` (ingested 2026-05-14)