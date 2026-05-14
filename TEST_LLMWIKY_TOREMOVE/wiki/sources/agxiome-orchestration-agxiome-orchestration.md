---
title: Agxiome Orchestration
category: source
tags: [orchestration, agents, dotnet, framework, agxiome]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Orchestration

## Summary
The `AGXIOME.Orchestration` project supplies the orchestration primitives and abstractions for coordinating multi-agent workflows within the AGXIOME framework. It builds on Microsoft.Extensions hosting, dependency injection, and logging to enable modular, scalable agent lifecycle management and inter-agent communication.

## Key Details
- Target framework: `net10.0` with nullable reference types and implicit usings enabled.
- Depends on `Microsoft.Extensions.DependencyInjection.Abstractions` (10.0.8), `Microsoft.Extensions.Hosting.Abstractions` (10.0.8), and `Microsoft.Extensions.Logging.Abstractions` (10.0.8).
- References the shared library `AGXIOME.Shared` and core library `AGXIOME.Core`.
- Intended to provide the glue between agent definitions (Core) and shared utilities (Shared), exposing orchestration services such as agent runners, schedulers, or workflow coordinators.
- Designed for integration with the standard .NET generic host, enabling background service patterns for long-running agent processes.

## Cross-References
- [[sources/agxiome-shared]] — Shared contracts and utilities used by orchestration
- [[sources/agxiome-core]] — Core agent abstractions that orchestration coordinates
- [[concepts/agent-orchestration]] — Conceptual overview of agent orchestration patterns

## Source Files
- `AGXIOME.Orchestration/AGXIOME.Orchestration.csproj` (ingested 2026-05-14)