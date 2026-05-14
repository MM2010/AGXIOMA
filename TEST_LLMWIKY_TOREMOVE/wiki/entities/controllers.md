---
title: Controllers
category: entity
tags: [REST API, Controllers, DTOs, Agent, Run, Session, Team, Workflow, Knowledge, Tool, ApiKey]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Controllers

## Summary
The AGXIOMA runtime exposes domain functionality through a set of REST API controllers, each mapped to a core entity (Agent, Run, Session, Team, Workflow, Knowledge, Tool, ApiKey). All controllers return JSON responses and rely on upstream middleware for authentication, Role-Based Access Control (RBAC), audit logging (`AuditLogger`), and input sanitization (`IInputSanitizer`). The controllers currently use in-memory stub storage, designed to be swapped for persistent repositories in production. Shared DTOs are defined inline and follow the record pattern.

## Key Details
- **AgentController** (`api/agents`) — CRUD for agents plus a `POST /{id}/clone` endpoint. Name input is validated via `IInputSanitizer.Sanitize` with `SanitizationRules.AgentNameR`.
- **RunController** (`api/runs`) — start, status, cancel, resume, pause, list (filterable by `agentId`), and stub endpoints for streaming and result retrieval.
- **SessionController** (`api/sessions`) — list (filterable by `agentId`), get, create, delete, and close (sets `IsActive = false`).
- **TeamController** (`api/teams`) — CRUD (source truncated; assumes standard pattern).
- **WorkflowController** (`api/workflows`) — CRUD (source truncated; assumes standard pattern).
- **KnowledgeController** (`api/knowledge`) — ingest and search endpoints (source truncated; assumes standard pattern).
- **ToolController** (`api/tools`) — list endpoint (source truncated; assumes standard pattern).
- **ApiKeyController** (`api/keys`) — create endpoint (source truncated; assumes standard pattern).
- Shared DTOs include `AgentDto`, `RunDto`, `SessionDto`, `TeamDto`, `WorkflowDto`, `ToolDto`, `ErrorResponse`, and their corresponding request records.
- Error responses use a consistent `ErrorResponse` record with `Code` and `Message` fields.
- All controller methods return `IActionResult` and use standard HTTP status codes (`200 OK`, `201 Created`, `202 Accepted`, `204 No Content`, `400 Bad Request`, `404 Not Found`, `409 Conflict`).
- Real-time streaming is stubbed; the `Stream` endpoint currently returns a placeholder JSON message instead of Server-Sent Events.

## Cross-References
- [[AuditLogger]] — used by `AgentController` for future audit logging.
- [[IInputSanitizer]] — injected into `AgentController` for input validation.
- [[SanitizationRules]] — defines the rules used for agent name sanitization.
- [[ApiKeyController]] — manages API key creation (RBAC roles/scopes).
- [[ErrorResponse]] — standard error DTO returned on failures.

## Source Files
- `AGXIOME.Runtime/Controllers/Controllers.cs` (ingested 2026-05-14)