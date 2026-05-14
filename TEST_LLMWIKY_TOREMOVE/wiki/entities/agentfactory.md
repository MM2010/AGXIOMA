---
title: AgentFactory
category: entity
tags: [AgentFactory, agent, factory, test, LLMWIKY]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentFactory

## Summary
`AgentFactory` is the central static factory for creating `IAgxiomeAgent` instances in the AGXIOME framework. It provides multiple overloads to configure agents with or without a reasoning engine, including a convenient test agent constructor that uses `TestLLMWIKYEngine` for unit testing without external LLM dependencies. The factory is designed to be extended by installing the LLM engine provider (e.g., LLMWIKY) and wiring it via `Configure()`.

## Key Details
- Static class; no instantiation required.
- `Configure(ILoggerFactory)` sets up logging for all agents created thereafter.
- `Create(AgentConfig)` — primary factory method that creates an `AgxiomeAgent` with a reasoning engine built from `config.ReasoningEngine`.
- `Create(string name, Action<AgentConfig>? configure)` — convenience overload for inline configuration.
- `CreateTestAgent(string name)` — creates an agent backed by `TestLLMWIKYEngine`, which returns a fixed string `"Test response"` for every inference (no real LLM needed).
- `CreateWithEngine(AgentConfig, ILLMWIKYEngine)` — allows callers to inject a pre-built engine instance (e.g., from demo wiring).
- The private `CreateEngine` method currently throws `NotImplementedException`, indicating the LLM engine package must be installed and wired manually.
- A `NullLogger<T>` internal class avoids null checks for logging.
- `TestLLMWIKYEngine` implements `ILLMWIKYEngine` with synchronous/asynchronous inference and support for streaming (single chunk).

## Cross-References
- [[AgxiomeAgent]] — concrete agent class instantiated by the factory.
- [[AgentConfig]] — configuration object passed to `Create()`.
- [[ILLMWIKYEngine]] — engine interface that must be implemented by LLM providers.
- [[TestLLMWIKYEngine]] — lightweight test implementation used by `CreateTestAgent()`.
- [[NullLogger]] — internal no-op logger to simplify null handling.

## Source Files
- `AGXIOME.Core/Agent/AgentFactory.cs` (ingested 2026-05-14)