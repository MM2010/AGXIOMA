---
title: AdvancedTests
category: entity
tags: [tests, workflows, context, chunkers, cost, skills, scheduling, agents, hooks, guardrails]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AdvancedTests

## Summary
The `AdvancedTests` class is a comprehensive integration and unit test suite for the AGXIOMA .NET agent framework. It validates core components including sequential and conditional workflows, context placeholder resolution, chunking algorithms (FixedSize, Heading), provider cost estimation, skill registration/categorization, schedule creation, and agent hook/guardrail interactions using `TestAgent`. These tests ensure framework correctness and robustness across multiple subsystems.

## Key Details
- **Workflow Tests**: Verify sequential execution order, conditional branching (skipping steps), loop iteration limits, and parallel step completion.
- **Context Engineering**: Tests `DynamicInstructionResolver` placeholder replacement and `DefaultDependencyProvider` registration/retrieval (including missing dependency error).
- **Chunker Tests**: Validate `FixedSizeChunker` splits text by token count with overlap, and `HeadingChunker` splits at markdown headings.
- **Cost Tracker Tests**: Confirm `ProviderCostTracker` estimates OpenAI costs, returns zero for local providers (Ollama) and unknown providers.
- **Skill Manager Tests**: Ensure `RegisterSkill` and `GetSkill` return the same instance, and `ListSkills` filters by category.
- **Schedule Manager Tests**: Validate async creation and listing of schedules.
- **Agent Factory Tests**: `TestAgent` with hooks executes pre-run hooks; with PII guardrail blocks sensitive input and returns error status.
- Uses test implementations `TestSkill` and `DelegateHook` for isolated testing.

## Cross-References
- [[Workflow]] — Core workflow engine tested here (sequential, conditional, loop, parallel).
- [[ContextEngine]] — Placeholder resolution and dependency injection patterns.
- [[Chunker]] — Fixed-size and heading-based chunking algorithms.
- [[ProviderCostTracker]] — Cost estimation logic validated.
- [[SkillManager]] — Registration, retrieval, and categorization of skills.
- [[ScheduleManager]] — Schedule creation and listing tested.
- [[AgentFactory]] — TestAgent creation with hooks and guardrails.
- [[Guardrails]] — PII detection guardrail behavior verified.

## Source Files
- `AGXIOME.Tests/AdvancedTests.cs` (ingested 2026-05-14)