---
title: OrchestrationServiceExtensions
category: entity
tags: [orchestration, DI, service-registration, extension-methods]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OrchestrationServiceExtensions

## Summary
Static extension methods on `IServiceCollection` that register all core orchestration services into the ASP.NET Core DI container. Provides a single entry point (`AddAgxiomeOrchestration`) which installs in-memory default implementations suitable for testing and single-node scenarios. Separate override methods (`UseRedisHotStore`, `UsePostgresOrchestrationStore`) allow swapping to production‑grade storage backends. Also serves as a documentation anchor for security (`UseOrchestrationSecurity`) and OpenTelemetry metrics (`AddOrchestrationMetrics`).

## Key Details
- `AddAgxiomeOrchestration` accepts an optional `Action<OrchestratorOptions>` callback for configuration.
- Uses `TryAddSingleton` for infrastructure services so that host projects can override before or after the call.
- Registers stateless master tools (`DecomposeRequestTool`, `PersistPlanTool`, `GenerateAgentsTool`, `SpawnAgentTool`, `PollResultsTool`, `AdvancePlanTool`) and sub-agent tools (`SaveResultTool`, `NotifyCompletionTool`) as transient.
- Registers `IPlanOrchestrator` as singleton (`PlanOrchestrator`) – owns the plan execution loop state.
- Adds `PlanResumeService` as a hosted service for plan resumption.
- Default infrastructure: `InMemoryHotStore`, `InMemoryOrchestrationStore`, `InMemoryCompletionNotifier`, `BackgroundWorkerAgentSpawner`.
- `UseRedisHotStore` configures `RedisHotStoreOptions` with connection string; actual Redis implementation must be provided by host.
- `UsePostgresOrchestrationStore` configures `PostgresOrchestrationStoreOptions` with connection string; actual Postgres implementation must be provided by host.
- `UseOrchestrationSecurity` is a convention anchor—actual `IAgxiomeCrypto` and `IAuditLogger` registrations come from `AGXIOME.Security`.
- `AddOrchestrationMetrics` is a documentation anchor—the actual OpenTelemetry meter (`"AGXIOME.Orchestration"`) must be added via `meterProviderBuilder.AddMeter(...)`.
- Options classes `RedisHotStoreOptions` and `PostgresOrchestrationStoreOptions` are defined in the same file.

## Cross-References
- [[PlanOrchestrator]] — Singleton orchestrator registered by the extension.
- [[IAgentSpawner]] — Implemented by `BackgroundWorkerAgentSpawner` registered as default.
- [[IHotStore]] — Default is `InMemoryHotStore`; overridable.
- [[IOrchestrationStore]] — Default is `InMemoryOrchestrationStore`; overridable.
- [[ICompletionNotifier]] — Default is `InMemoryCompletionNotifier`.
- [[PlanResumeService]] — Hosted service registered for plan resumption.
- [[RedisHotStoreOptions]] — Configuration options for Redis override.
- [[PostgresOrchestrationStoreOptions]] — Configuration options for Postgres override.

## Source Files
- `AGXIOME.Orchestration/OrchestrationServiceExtensions.cs` (ingested 2026-05-14)