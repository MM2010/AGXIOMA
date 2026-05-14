---
title: Agxiome Core
category: source
tags: [net, core, dependency-injection, logging]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Core

## Summary
The `AGXIOME.Core` project is the central assembly of the AGXIOMA .NET agent framework. It orchestrates core abstractions by referencing shared utilities (`AGXIOME.Shared`), LLM integration (`AGXIOME.LLMWIKY`), and dynamic runtime capabilities (`AGXIOME.DynamicRuntime`). It also depends on Microsoft.Extensions.DependencyInjection and Logging.Abstractions for dependency injection and logging infrastructure.

## Key Details
- **Target**: .NET 10.0 (`net10.0`)
- **Implicit usings**: enabled; **nullable reference types**: enabled
- **Internal dependencies**:
  - `AGXIOME.Shared` — common types and utilities
  - `AGXIOME.LLMWIKY` — LLM interaction layer
  - `AGXIOME.DynamicRuntime` — runtime code generation/evaluation
- **External packages**:
  - `Microsoft.Extensions.DependencyInjection` (v10.0.8)
  - `Microsoft.Extensions.Logging.Abstractions` (v10.0.8)
- Serves as the composition root for core framework services.

## Cross-References
- [[sources/agxiome-shared]] — shared models and helpers used by Core
- [[sources/agxiome-llmwiky]] — LLM integration consumed by Core
- [[sources/agxiome-dynamicruntime]] — dynamic execution support linked from Core

## Source Files
- `AGXIOME.Core/AGXIOME.Core.csproj` (ingested 2026-05-14)