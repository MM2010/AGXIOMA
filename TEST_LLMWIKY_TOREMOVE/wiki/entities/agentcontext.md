---
title: AgentContext
category: entity
tags: [agent, context, dependency injection, instruction resolver]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentContext

## Summary
`AgentContext` is the central configuration object for an agent, holding the system prompt, introduction message, few-shot examples, and a list of instructions. It provides a simple API to add instructions and few-shot pairs. The same source file also defines `DefaultDependencyProvider`, a lightweight DI container suitable for testing or small deployments, and `DynamicInstructionResolver`, which replaces `{key}` placeholders with values from an `ISessionState` instance.

## Key Details
- `AgentContext` implements `IAgentContext` and exposes properties: `SystemPrompt`, `IntroductionMessage`, `FewShotExamples`, `Instructions`, and `AdditionalContext`.
- `AddInstruction(string)` appends a single instruction to the `Instructions` list.
- `AddFewShot(AgentMessage input, AgentMessage output)` appends both input and output messages to `FewShotExamples` (lazy-initialized).
- `DefaultDependencyProvider` uses internal dictionaries for singleton instances and factory functions; throws `InvalidOperationException` if a dependency is not registered.
- `RegisterDependency<T>(T instance)` registers an existing instance; `RegisterDependency<T>(Func<T> factory)` registers a factory delegate.
- `DynamicInstructionResolver` uses a compiled regex `\{(\w+)\}` to find placeholders and resolves them via `ISessionState.Get<object>(key)`.
- If the key is not found in session state, the original placeholder (e.g., `{key}`) is returned unchanged.
- The resolver is designed to be stateless and injectable.

## Cross-References
- [[IAgentContext]] — interface that `AgentContext` implements
- [[ISessionState]] — interface used by `DynamicInstructionResolver` for placeholder resolution
- [[AgentMessage]] — model used for few-shot examples
- [[IDependencyProvider]] — interface implemented by `DefaultDependencyProvider`

## Source Files
- `AGXIOME.Core/Context/AgentContext.cs` (ingested 2026-05-14)