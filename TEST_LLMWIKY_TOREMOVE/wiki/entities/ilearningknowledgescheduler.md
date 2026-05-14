---  
title: ILearningKnowledgeScheduler  
category: entity  
tags: [interface, learning, knowledge-base, workflow-scheduler, dependency-provider]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# ILearningKnowledgeScheduler  

## Summary  
The `ILearningKnowledgeScheduler` source file defines four foundational interfaces for the AGXIOME agent framework: `ILearningMachine` (agent‑level learning and memory), `IKnowledgeBase` (document‑driven knowledge ingestion and retrieval), `IWorkflowScheduler` (cron‑based workflow scheduling and execution history), and `IDependencyProvider` (lightweight DI container for registering and resolving dependencies). Together they form the core abstractions for persistent learning, external knowledge integration, scheduled automation, and service location.  

## Key Details  
- `ILearningMachine` exposes methods for user memory, entity facts, session context, learned insights, decision logging, and an end‑to‑end `ProcessAsync`.  
- `IKnowledgeBase` supports single/batch document ingestion, deletion, and semantic search with `KnowledgeSearchOptions`.  
- `IWorkflowScheduler` enables creating, updating, deleting, listing, and manually triggering schedules, plus retrieving run history.  
- `IDependencyProvider` provides a generic `GetDependency<T>()`, `RegisterDependency<T>(T instance)`, and a factory overload.  
- All methods default to `CancellationToken` support for async cancellation.  
- The interfaces are contained in a single file (`ILearningKnowledgeScheduler.cs`) but are logically independent.  

## Cross-References  
- [[ILearningMachine]] — core interface for agent learning and memory (if separate page exists).  
- [[IKnowledgeBase]] — document knowledge abstraction (if separate page exists).  
- [[IWorkflowScheduler]] — schedule management interface (if separate page exists).  
- [[IDependencyProvider]] — simple service locator / DI container (if separate page exists).  

## Source Files  
- `AGXIOME.Shared/Interfaces/ILearningKnowledgeScheduler.cs` (ingested 2026-05-14)