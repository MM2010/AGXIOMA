---
title: Agxiome Dynamicruntime Agxiome Dynamicruntime
category: source
tags: [dynamic-runtime, code-analysis, hosting, logging, dotnet]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Dynamicruntime Agxiome Dynamicruntime

## Summary
The `AGXIOME.DynamicRuntime` project provides dynamic code compilation and execution capabilities within the AGXIOME agent framework. It relies on Microsoft Roslyn (CodeAnalysis.CSharp) to compile C# scripts or fragments at runtime, and integrates with the shared library and LLM/wiki modules for hosting and logging abstractions.

## Key Details
- Targets `net10.0` with implicit usings and nullable enabled.
- Root namespace: `AGXIOME.DynamicRuntime`.
- Depends on [[sources/agxiome-shared-agxiome-shared|AGXIOME.Shared]] for common utilities.
- Depends on [[sources/agxiome-llmviky-agxiome-llmviky|AGXIOME.LLMWIKY]] for LLM-driven wiki interactions.
- Uses `Microsoft.CodeAnalysis.CSharp` version 5.3.0 for in-process compilation.
- Uses `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Logging.Abstractions` (both v10.0.8) for generic host and logging integration.
- Likely supports runtime script evaluation, plugin loading, or agent behavior generation.

## Cross-References
- [[sources/agxiome-shared-agxiome-shared|AGXIOME.Shared]] — shared foundational types and utilities.
- [[sources/agxiome-llmviky-agxiome-llmviky|AGXIOME.LLMWIKY]] — LLM-based knowledge and wiki access layer.

## Source Files
- `AGXIOME.DynamicRuntime/AGXIOME.DynamicRuntime.csproj` (ingested 2026-05-14)