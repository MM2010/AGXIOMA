---
title: AgxiomeServiceCollectionExtensions
category: entity
tags: [DI, dependency-injection, aspnetcore, agent-framework, service-collection]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeServiceCollectionExtensions

## Summary
The `AgxiomeServiceCollectionExtensions` static class provides the `.AddAgxiome()` extension method on `IServiceCollection`, enabling seamless integration of the AGXIOMA agent framework into ASP.NET Core applications. It registers core LLM config, agent factories, named agents, swarm patterns, and a model router—all with a fluent configuration API via `AgxiomeDIOptions`. This is a unique feature of the .NET implementation, with no equivalent in the Python Swarms library.

## Key Details
- **Extension method:** `IServiceCollection.AddAgxiome(Action<AgxiomeDIOptions>? configure = null)`
- Registers a singleton `LLMWIKYConfig` (from `options.DefaultLLMConfig`)
- Registers a factory delegate `Func<LLMWIKYConfig, ILLMProvider>` backed by `LLMProviderFactory.Create`
- Registers `IAgxiomeAgentFactory` as singleton; internally caches agents per name using a `ConcurrentDictionary`
- Registers each named agent (from `AddAgent()`) as transient via the factory
- Registers `MixtureOfAgentsSwarm` as transient, created from all registered agents
- Registers `AgxiomeModelRouter` as transient using `options.RouterEntries` and default routing strategy
- `AgxiomeDIOptions` supports `UseDeepSeek()`, `UseOpenAI()`, `UseAnthropic()`, `AddAgent()`, `WithRoutingStrategy()`, and `AddRouterEntry()`
- `IAgxiomeAgentFactory` provides `GetAgent(string name)` and `CreateAnonymous(Action<AgentBuilder> configure)`
- `AgxiomeAgentFactory` is internal; builds agents via `AgxiomeBuilder`

## Cross-References
- [[AgxiomeDIOptions]] — the configuration options object used to set up LLM providers and agents
- [[IAgxiomeAgentFactory]] — the service interface for retrieving named or anonymous agents
- [[AgxiomeBuilder]] — used internally to construct agents with fluent configuration
- [[AgxiomeModelRouter]] — registered as a transient service for routing decisions
- [[MixtureOfAgentsSwarm]] — swarm pattern registered using all defined agents
- [[LLMProviderFactory]] — static factory for creating ILLMProvider instances

## Source Files
- `AGXIOME.Core/DependencyInjection/AgxiomeServiceCollectionExtensions.cs` (ingested 2026-05-14)