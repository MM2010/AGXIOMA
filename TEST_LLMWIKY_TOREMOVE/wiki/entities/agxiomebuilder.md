---
title: AgxiomeBuilder
category: entity
tags: [builder, fluent, agent, swarm, configuration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeBuilder

## Summary
The `AgxiomeBuilder` is a fluent, zero-boilerplate entry point for constructing agents and swarms in the AGXIOMA framework. It eliminates manual wiring of `AgentConfig`, `LLMWIKYConfig`, and `AgentFactory`, providing a type-safe, IntelliSense‑friendly API that lets you go from nothing to a working agent in a single line of code. Nested `AgentBuilder` and `SwarmBuilder` classes handle all configuration, provider selection, tool registration, and final build.

## Key Details
- **Entry Points**  
  - `AgxiomeBuilder.Agent(string name)` — creates an `AgentBuilder` for a single agent.  
  - `AgxiomeBuilder.Swarm()` — creates a `SwarmBuilder` for a team/swarm.  
  - `AgxiomeBuilder.QuickStart` — provides a pre‑configured helper for rapid prototyping.
- **AgentBuilder** configures an individual agent:  
  - `UseDeepSeek()`, `UseOpenAI()`, `UseAnthropic()`, `UseGemini()`, `UseOllama()`, `UseGroq()`, `UseMistral()` — one‑liners for each LLM provider (reads API key from environment unless overridden).  
  - `UseModel(provider, model, apiKey?, endpoint?)` — generic provider configuration.  
  - `AutoDetectProvider()` — scans environment variables in priority order and falls back to Ollama.  
  - `WithSystemPrompt(string)` — sets the system prompt.  
  - `WithMaxIterations(int)`, `WithAutoLoops(int)`, `WithInfiniteLoops(int)` — control iteration limits and mode (`Fixed`, `Auto`, `Infinite`).  
  - `WithTimeout(TimeSpan)` — per‑agent timeout.  
  - `WithTool(IAgxiomeTool)` — registers a custom tool.  
  - `WithLogging(ILoggerFactory)` — injects a logging factory.  
  - `Build()` — uses `AgentFactory.CreateWithEngine` to produce an `IAgxiomeAgent`, wiring the chosen `LLMWIKYEngine` and registering tools.
- **SwarmBuilder** composes multiple agents into a team:  
  - `AddAgent(string name, Action<AgentBuilder>? configure)` — adds a named agent, optionally configured inline.  
  - `AsMixtureOfAgents(int layers)` — configures a Mixture‑of‑Agents team topology.  
  - Supports sequential and social‑swarm patterns (further config via `WithSpeakerPolicy`, `WithVotingMode`, etc. – see truncated source for full list).  
  - `Build()` — returns an `IAgxiomeSwarm` (likely implementing swarm orchestration).
- All methods are fluent and return the builder instance for chaining.
- The builder reads API keys from environment variables (e.g. `DEEPSEEK_API_KEY`, `OPENAI_API_KEY`, …) when not provided explicitly.

## Cross-References
- [[AgentFactory]] — used internally by `AgentBuilder.Build()` to create agent instances.
- [[LLMWIKYEngine]] — the LLM engine wired into each agent by the builder.
- [[IAgxiomeAgent]] — the interface returned by `Build()`.
- [[IAgxiomeTool]] — tools are registered via the builder’s `WithTool()` method.
- [[AgentConfig]] — the configuration object constructed and passed to the factory.
- [[LLMWIKYConfig]] — the LLM provider configuration set by the `Use*` methods.
- [[QuickStartHelper]] — companion static helper for immediate prototyping.

## Source Files
- `AGXIOME.Core/AgxiomeBuilder.cs` (ingested 2026-05-14)