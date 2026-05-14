---
title: DynamicAgentController
category: entity
tags: [controller, REST, dynamic-agents, API]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DynamicAgentController

## Summary
The `DynamicAgentController` is a sealed ASP.NET Core API controller that exposes REST endpoints for managing user-defined dynamic agents. It provides operations to list agents, generate new agents via LLM, trigger recompilation, and remove agents from the registry. The controller is the primary HTTP interface for the dynamic runtime's agent lifecycle.

## Key Details
- **Route**: `api/agents` (base path)
- **Dependencies**: `DynamicAgentRegistry`, `AgentCodeGenerator`, `AgentDropFolderService`
- **Endpoints**:
  - `GET /api/agents/user/{userId}` – Returns a list of agents for a user, including id, description, status, discovery time, and last error.
  - `POST /api/agents/generate` – Generates a new agent using LLM, accepts `GenerateAgentRequest` body (UserId, Description, optional ExampleInput/ExampleOutput). Returns agent id, total time, and file path.
  - `POST /api/agents/{agentId}/compile` – Forces recompilation by resetting agent status to `Discovered`. Requires `userId` query parameter.
  - `DELETE /api/agents/{agentId}/remove` – Removes an agent from the registry. Requires `userId` query parameter. Returns `204 No Content` on success.
- **Data Transfer Object**: `GenerateAgentRequest` (record with UserId, Description, ExampleInput, ExampleOutput)
- **Error Handling**: Returns `400` for missing required fields, `404` for not found, `422` for generation failures.

## Cross-References
- [[DynamicAgentRegistry]] — registry used to store and query agent entries
- [[AgentCodeGenerator]] — handles LLM-based agent code generation
- [[AgentDropFolderService]] — service for monitoring drop folder (injected for future use)
- [[AgentEntryStatus]] — status enum used to track agent lifecycle (e.g., Discovered)
- [[AgentGenerationRequest]] — internal request object used by `AgentCodeGenerator`

## Source Files
- `AGXIOME.Runtime/Controllers/DynamicAgentController.cs` (ingested 2026-05-14)