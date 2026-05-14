---
title: Shared Tests for Models and Configuration
category: entity
tags: [tests, records, immutability, config, enums]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Shared Tests for Models and Configuration

## Summary
This xUnit test suite validates the core behaviors of the AGXIOME.Shared namespace: record immutability (via `with` expressions), structural equality, null tolerance in `AgentMessage`, enum parse round-tripping for `AgentStatus`, and default values for `AgentConfig` and `LLMWIKYConfig`. It ensures that foundational types behave correctly before higher-level logic depends on them.

## Key Details
- `Record_Immutability_With_ReturnsNewInstance` confirms that `with` creates a new instance without altering the original.
- `Record_Equality_SameValuesAreEqual` and `Record_Equality_DifferentValuesNotEqual` verify value-based equality for `AgentRequest`.
- `Record_NullString_DoesNotThrow` shows `AgentMessage` accepts `null` content without exception.
- `Enum_ParseRoundtrip` ensures `AgentStatus.Completed` survives `ToString()` and `Enum.Parse`.
- `Config_HasSensibleDefaults` checks `AgentConfig.MaxIterations = 20`, `Timeout = 30s`, and `ReasoningEngine.Provider = OpenAI`.
- `LLMWIKYConfig_HasDefaults` verifies model = `"gpt-4o"`, temperature = `0.7`, and cost tracking enabled.

## Cross-References
- [[Shared Models]] — defines `AgentRequest`, `AgentMessage`, `AgentStatus` under test.
- [[Configuration]] — defines `AgentConfig` and `LLMWIKYConfig` used in default tests.

## Source Files
- `AGXIOME.Tests/SharedTests.cs` (ingested 2026-05-14)