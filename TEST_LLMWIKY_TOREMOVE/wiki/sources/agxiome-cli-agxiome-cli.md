---  
title: AGXIOME.CLI Project  
category: source  
tags: [cli, executable, project]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# AGXIOME.CLI Project  

## Summary  
The `AGXIOME.CLI` project is the command-line interface executable for the AGXIOME agent framework. It acts as the primary entry point, aggregating all core modules—including storage, real-time, security, telemetry, and agent behavior—into a single runnable application. This project references every other major library in the solution, making it the composition root of the framework.  

## Key Details  
- **Output type**: `Exe` (console application)  
- **Target framework**: `net10.0`  
- **Language features**: Implicit usings enabled, nullable reference types enabled  
- References **8 internal projects**:  
  - `AGXIOME.Core` — core abstractions and agent logic  
  - `AGXIOME.Interfaces` — shared contracts  
  - `AGXIOME.LLMWIKY` — LLM integration  
  - `AGXIOME.Storage` — persistence layer  
  - `AGXIOME.XLink` — cross-platform linking  
  - `AGXIOME.Realtime` — real-time communications  
  - `AGXIOME.Security` — authentication/authorization  
  - `AGXIOME.Telemetry` — monitoring and diagnostics  
- No external NuGet dependencies are listed in this project file (all functionality is drawn from internal references).  

## Cross-References  
- [[sources/agxiome-core-agxiome-core]] — core library referenced by CLI  
- [[sources/agxiome-interfaces-agxiome-interfaces]] — shared interfaces used by CLI  
- [[sources/agxiome-storage-agxiome-storage]] — storage backend wired by CLI  
- [[sources/agxiome-realtime-agxiome-realtime]] — real-time capabilities started from CLI  
- [[sources/agxiome-security-agxiome-security]] — security layer configured in CLI  
- [[sources/agxiome-telemetry-agxiome-telemetry]] — telemetry sink consumed by CLI  
- [[concepts/command-line-interface]] — conceptual page for CLI usage (if exists)  

## Source Files  
- `AGXIOME.CLI/AGXIOME.CLI.csproj` (ingested 2026-05-14)