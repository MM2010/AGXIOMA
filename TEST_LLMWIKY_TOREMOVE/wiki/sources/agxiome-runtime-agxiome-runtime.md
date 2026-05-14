---  
title: Agxiome Runtime Agxiome Runtime  
category: source  
tags: [AGXIOME, runtime, .NET, csproj]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---

# Agxiome Runtime Agxiome Runtime

## Summary  
This project (`AGXIOME.Runtime`) is the main web application entry point for the AGXIOME .NET agent runtime. It composes all major framework layers—core, shared, storage, security, telemetry, dynamic runtime, and LLM wiki—into a single executable service. The inclusion of `StackExchange.Redis` adds support for distributed caching and pub/sub messaging, making it suitable for scalable multi-agent deployments.

## Key Details
- Targets `net10.0` with nullable enabled and implicit usings.
- Serves as the integration hub for all AGXIOME subsystems.
- References seven internal projects: Core, Shared, LLMWIKY, Storage, Security, Telemetry, DynamicRuntime.
- Uses `StackExchange.Redis` v2.13.1 for Redis-backed state management and communication.
- Root namespace: `AGXIOME.Runtime`.
- Defines the runtime’s composition root and startup configuration.
- Oriented as a `Microsoft.NET.Sdk.Web` project, indicating an ASP.NET Core host.

## Cross-References
- [[sources/agxiome-core]] — Provides core agent abstractions and logic.
- [[sources/agxiome-shared]] — Contains shared DTOs and utilities.
- [[sources/agxiome-storage]] — Manages persistent storage for agents and sessions.
- [[sources/agxiome-security]] — Handles authentication and authorization.
- [[sources/agxiome-telemetry]] — Captures metrics and logs.
- [[sources/agxiome-dynamicruntime]] — Enables dynamic agent loading.
- [[sources/agxiome-llmwiki]] — Integrates LLM knowledge retrieval.

## Source Files
- `AGXIOME.Runtime/AGXIOME.Runtime.csproj` (ingested 2026-05-14)