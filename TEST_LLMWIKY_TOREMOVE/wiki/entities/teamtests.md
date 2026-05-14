---
title: TeamTests
category: entity
tags: [testing, teams, team-modes, xunit]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TeamTests

## Summary
The `TeamTests` class validates core behaviors of the [[AgxiomeTeam]] entity across its operational modes—Coordinate (default), Route, and Broadcast. These tests ensure that teams can be created with a leader and optional members, execute tasks, and return results consistent with the selected mode. The test suite is written using xUnit and covers empty teams, single-member teams, and multi-member broadcast scenarios.

## Key Details
- Defines a helper `CreateTeam(name, mode)` that instantiates a leader agent via `AgentFactory.CreateTestAgent` and a `TeamConfig` with the given name and mode.
- `Team_NoMembers_Executes` verifies that a team with no additional members can still execute and return a non-null result.
- `Team_WithMember_Executes` adds a single worker agent (`"m1"`) and asserts `result.Content` is not null.
- `RouteMode_ReturnsSingleResponse` sets mode to `TeamMode.Route`, adds a specialist agent, and checks that `result.ModeUsed` equals `TeamMode.Route`.
- `BroadcastMode_AllMembersCalled` configures mode as `TeamMode.Broadcast`, adds two workers, and confirms `result.AgentResults.Count == 2`.
- The tests rely on [[AgxiomeTeam]], [[TeamConfig]], [[TeamMode]], and [[AgentFactory]] for setup and assertions.

## Cross-References
- [[AgxiomeTeam]] — the core team class exercised in these tests.
- [[TeamConfig]] — configuration object used to set name and mode.
- [[TeamMode]] — enum specifying Coordinate, Route, or Broadcast behaviour.
- [[AgentFactory]] — factory for creating test agents used as team leader.

## Source Files
- `AGXIOME.Tests/TeamTests.cs` (ingested 2026-05-14)