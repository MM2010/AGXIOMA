---
title: SocialAlgorithmTests
category: entity
tags: [testing, social-algorithms, unit-tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SocialAlgorithmTests

## Summary
Unit tests for the `SocialAlgorithmExecutor` and built-in social algorithms (`RoundRobin`, `Debate`). These tests verify correct behavior under null parameters, ordering of agent execution, multi-round execution, and custom callable support. They ensure the social algorithm execution infrastructure integrates properly with agent factories and result types.

## Key Details
- Contains four `[Fact]` tests in the `SocialAlgorithmTests` class.
- `SocialAlgorithmExecutor_ThrowsOnNull` validates that passing `null` to the constructor throws `ArgumentNullException`.
- `SocialAlgorithm_RoundRobin_ExecutesInOrder` calls the `RoundRobin` algorithm with two agents and asserts the result uses `TeamMode.SocialAlgorithm` and contains two agent results.
- `SocialAlgorithm_Debate_ExecutesAllRounds` calls the `Debate` algorithm and checks that at least two agent results are returned.
- `SocialAlgorithm_CustomCallable_ReturnsResult` demonstrates injection of a custom `SocialAlgorithmExecutor` delegate and verifies the content and mode of the returned `TeamResponse`.
- All tests use `AgentFactory.CreateTestAgent` to create mock agents for isolation.
- Tests are asynchronous where the algorithm being tested performs async operations.

## Cross-References
- [[SocialAlgorithmExecutor]] — the class under test.
- [[SocialAlgorithms]] — provides the built-in `RoundRobin` and `Debate` algorithms.
- [[TeamResponse]] — result type returned by social algorithm execution.
- [[TeamMode]] — enum value `SocialAlgorithm` used in assertions.
- [[AgentResult]] — per-agent result stored in the response.
- [[AgentFactory]] — helper used to create test agent instances.

## Source Files
- `AGXIOME.Tests/SocialAlgorithmTests.cs` (ingested 2026-05-14)