---
title: AgentServicesInitializer
category: entity
tags: [agent, initialization, lifecycle, bridge, hosted-service]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentServicesInitializer

## Summary
`AgentServicesInitializer` is a bridge component that acts as an `IHostedService` to trigger the one-time initialization of `AgentServices` with the provided `ILLMProvider` as soon as the application starts. It guarantees that the dependency injection container is fully built before calling `AgentServices.Initialize`, ensuring all required dependencies are available.

## Key Details
- **Type**: `internal sealed class` implementing `Microsoft.Extensions.Hosting.IHostedService`
- **Namespace**: `AGXIOME.DynamicRuntime.Bridge`
- **Constructor**: Accepts a single `ILLMProvider` parameter (injected via DI)
- **StartAsync**: Calls `AgentServices.Initialize(llm)` and returns `Task.CompletedTask` immediately (synchronous)
- **StopAsync**: Returns `Task.CompletedTask` without performing any cleanup
- **Role**: Decouples initialization from application startup, making it safe to run after DI container is ready
- **Design**: Sealed and internal — not intended for external consumption; used internally by the runtime hosting layer

## Cross-References
- [[AgentServices]] — the static service that gets initialized by this hosted service
- [[ILLMProvider]] — the LLM provider instance passed to `AgentServices.Initialize`
- [[DynamicRuntime]] — the broader runtime layer to which this bridge belongs

## Source Files
- `AGXIOME.DynamicRuntime/Bridge/AgentServicesInitializer.cs` (ingested 2026-05-14)