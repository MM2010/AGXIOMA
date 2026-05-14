---  
title: AGXIOME.Shared  
category: source  
tags: [project, shared, library, dotnet]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# AGXIOME.Shared  

## Summary  
**AGXIOME.Shared** is a .NET 10.0 class library project that serves as the shared foundation for the AGXIOME agent framework. It contains common types, interfaces, and utilities that are referenced by other projects within the solution, promoting code reuse and consistent abstractions across the framework.  

## Key Details  
- Targets `net10.0` with implicit usings and nullable reference types enabled.  
- Minimal project file structure; all shared code is expected in source files within the project.  
- Likely houses base classes, custom attributes, extension methods, or shared DTOs used by AGXIOME components.  
- Acts as a dependency for other AGXIOME projects (e.g., agent implementations, orchestration layers).  
- The project name follows a `<CompanyOrFramework>.<Subcomponent>` naming convention.  

## Cross-References  
- [[sources/agxiome-core-agxiome-core]] — likely consumes shared types from this project.  
- [[concepts/agxiome-shared-library]] — conceptual overview of shared libraries in the AGXIOME ecosystem.  

## Source Files  
- `AGXIOME.Shared/AGXIOME.Shared.csproj` (ingested 2026-05-14)