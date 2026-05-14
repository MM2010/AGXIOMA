---
title: Agxiome Interfaces
category: source
tags: [interface, project, framework]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Interfaces

## Summary
The **AGXIOME.Interfaces** project defines the core abstractions (interfaces) that the AGXIOME .NET agent framework relies on. It references both `AGXIOME.Shared` and `AGXIOME.Core`, serving as the contract layer that decouples concrete implementations from the framework's core logic. This project is crucial for enabling extensibility and dependency injection within the agent architecture.

## Key Details
- Target framework: `net10.0`
- Implicit usings are enabled; nullable reference types are enabled
- Depends on `AGXIOME.Shared` for common types and utilities
- Depends on `AGXIOME.Core` for core domain logic
- Acts as the interface/contract assembly, allowing multiple implementations of the same abstraction
- Typically contains no implementation code, only interface definitions and related types
- Named to suggest it holds the "agxiome" (axiom) interfaces — the foundational contracts for agents

## Cross-References
- [[sources/agxiome-shared-agxiome-shared]] — Shared utilities and data types used by the interfaces
- [[sources/agxiome-core-agxiome-core]] — Core domain logic that the interfaces abstract

## Source Files
- `AGXIOME.Interfaces/AGXIOME.Interfaces.csproj` (ingested 2026-05-14)