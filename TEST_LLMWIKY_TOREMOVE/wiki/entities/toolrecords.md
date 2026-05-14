---
title: Tool Records
category: entity
tags: [tool, records, models, agent]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Tool Records

## Summary
The `ToolRecords` module defines a set of strongly-typed, immutable record types that encapsulate the lifecycle and metadata of tool execution within the AGXIOMA agent framework. These records standardize how tool context, results, metadata, registration, and call outcomes are represented, enabling consistent handling and interoperability across agents, sessions, and dependency injection systems.

## Key Details
- **ToolContext**: Captures execution environment for a tool, including `AgentId`, `RunId`, optional `TenantId`, parameters, timeout, `ISessionState`, and `IDependencyProvider`.
- **ToolResult**: Standard success/failure outcome with `Data`, `ErrorMessage`, `ExecutionTime`, and optional `ToolMetadata`.
- **ToolMetadata**: Describes the tool's identity (`ToolName`, `Version`) and performance metrics (`BytesProcessed`, `ItemsProcessed`).
- **ToolRegistration**: Used to register a tool by its `TypeName` and optional configuration dictionary.
- **ToolCallResult**: Logs a specific invocation of a tool, containing `ToolName`, `ToolCallId`, success status, `Result`, and `Duration`.
- All records are C# `record` types providing value-based equality and immutability.
- Nullable fields (e.g., `TenantId`, `ErrorMessage`) allow flexible usage without requiring default values.

## Cross-References
- [[ISessionState]] — interface used within `ToolContext` to manage session state.
- [[IDependencyProvider]] — interface used within `ToolContext` for dependency injection.

## Source Files
- `AGXIOME.Shared/Models/ToolRecords.cs` (ingested 2026-05-14)