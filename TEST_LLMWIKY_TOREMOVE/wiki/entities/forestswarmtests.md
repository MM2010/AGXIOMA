---
title: ForestSwarmTests
category: entity
tags: [forest-swarm, tests, xunit, routing]
source_count: 12
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ForestSwarmTests

## Summary
This test suite validates the configuration, construction, and routing logic of the ForestSwarm pattern. It covers default configuration values, structural validation for `ForestAgentEntry` and `ForestTree`, and keyword‑based best‑match selection including fallback to the first agent when no keywords overlap.

## Key Details
- `ForestSwarmConfig` has a default `MinMatchScore` of `0`.
- `ForestSwarmResult` stores task, selected tree, selected agent, output, matched keywords, and duration.
- `ForestAgentEntry` throws `ArgumentNullException` if the agent is null and `ArgumentException` if the agent ID is empty.
- `ForestTree` throws `ArgumentException` for empty agent list or empty tree name.
- `SwarmFactory.ForestSwarm` throws if the tree collection is empty.
- Routing via `FindBestMatch` selects the tree/agent with the highest keyword overlap with the input task.
- When multiple agents belong to the same tree, the agent with the highest keyword match score is chosen.
- If no keywords match, the first tree’s first agent is returned with an empty matched keyword list.
- The `Name` and `Description` of the forest swarm are preserved through the factory method.
- All tests use NSubstitute mocks of `IAgxiomeAgent`.

## Cross-References
- [[ForestSwarm]] — the pattern under test.
- [[SwarmFactory]] — factory that creates the swarm instance.
- [[ForestTree]] — tree structure tested for validation and routing.
- [[ForestAgentEntry]] — agent entry validated for null/empty checks.
- [[ForestSwarmResult]] — result record verified for field storage.
- [[ForestSwarmConfig]] — configuration defaults validated.

## Source Files
- `AGXIOME.Tests/ForestSwarmTests.cs` (ingested 2026-05-14)