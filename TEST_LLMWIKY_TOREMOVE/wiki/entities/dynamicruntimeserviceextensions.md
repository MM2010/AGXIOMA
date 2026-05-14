---
title: DynamicRuntimeServiceExtensions
category: entity
tags: [dynamicruntime, extensionmethods, dependencyinjection, codegeneration, skills, compilation, hotfolder, registry]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DynamicRuntimeServiceExtensions

## Summary
The `DynamicRuntimeServiceExtensions` static class provides the single standard `AddDynamicAgentRuntime` extension method on `IServiceCollection` that registers all core DynamicRuntime services into the .NET dependency injection container. It wires up the agent registry, compilation pipeline, hot-folder monitoring, LLM-based code generation, and skills infrastructure, optionally accepting a `DynamicRuntimeOptions` delegate for configuration.

## Key Details
- `AddDynamicAgentRuntime(Action<DynamicRuntimeOptions>? configure = null)` — fluent extension method on `IServiceCollection`.
- Registers `DynamicRuntimeOptions` as singleton (with defaults: `AgentsBasePath` = `~/.agxioma/agents`, `SkillsBasePath` = `~/.agxioma/skills`, `SkillsShBaseUrl` = `https://api.skills.sh/v1`).
- Initializes the static `AgentServices` locator via `AgentServicesInitializer` (singleton + `IHostedService`).
- Registers `DynamicAgentRegistry`, `AgentCompiler`, `LazyAgentCompiler`, `AgentCacheStore` as singletons.
- Adds `AgentDropFolderService` as both singleton and `IHostedService` for hot-folder monitoring.
- Creates `AgentCodeGenerator` singleton using `ILLMProvider`, `AgentCompiler`, and `DynamicAgentRegistry`.
- Registers `SkillsShClient` (with configurable base URL), `SkillsShRegistry` (with configurable skills base path), and `SkillAdapter` as singletons.
- Designed to be called once during application startup (e.g., in `Host.CreateDefaultBuilder` or `Program.cs`).

## Cross-References
- [[DynamicAgentRegistry]] — core registry for dynamic agents
- [[AgentCompiler]] — compiles agent source code
- [[LazyAgentCompiler]] — deferred compilation wrapper
- [[AgentCacheStore]] — in-memory cache for compiled agents
- [[AgentDropFolderService]] — monitors user drop folders
- [[AgentCodeGenerator]] — generates agent code via LLM
- [[SkillsShClient]] — HTTP client for Skills.sh API
- [[SkillsShRegistry]] — manages installed skills on disk
- [[SkillAdapter]] — adapts skills for agent execution
- [[ILLMProvider]] — interface for LLM integration

## Source Files
- `AGXIOME.DynamicRuntime/DynamicRuntimeServiceExtensions.cs` (ingested 2026-05-14)