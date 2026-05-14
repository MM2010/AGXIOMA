---
title: ApprovalService
category: entity
tags: [HITL, approval, workflow, human-in-the-loop]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ApprovalService

## Summary
The `ApprovalService` implements the Human-In-The-Loop (HITL) pattern within the AGXIOME agent runtime. It manages approval requests raised by agents during workflow execution, pausing the agent until the request is approved or rejected. The service provides thread-safe, in-memory storage of pending approvals, supports configurable TTLs, and exposes a clean contract (`IApprovalService`) for querying pending requests, approving, rejecting, and retrieving history.

## Key Details
- **Pattern**: HITL (Human-In-The-Loop) — agent pauses, emits `ApprovalRequest`, resumes after approve/reject.
- **Domain Records**: `ApprovalStatus` enum (`Pending`, `Approved`, `Rejected`, `Expired`), `ApprovalRequest` (immutable record with `RequestId`, `AgentId`, `RunId`, `Action`, `Context`, `Status`, `CreatedAt`, `ExpiresAt`), `ApprovalResult` (success flag, request ID, new status, optional comment).
- **Service Contract** (`IApprovalService`):
  - `GetPendingApprovalsAsync` — returns all pending requests, optionally filtered by agent.
  - `ApproveAsync` — marks a request as approved and signals the run to resume.
  - `RejectAsync` — marks a request as rejected; run receives rejection signal.
  - `GetApprovalRequestAsync` — retrieves a single request by ID.
  - `GetApprovalHistoryAsync` — returns approved/rejected requests, ordered by creation descending, with optional agent filter and limit.
- **Implementation** (`ApprovalService`): In-memory using `ConcurrentDictionary<string, ApprovalRequest>` for thread safety.
  - `CreateRequest` — called by the runtime to create a new pending request with a TTL (default 30 minutes).
  - State transitions are guarded: only pending requests can be approved/rejected; duplicate transitions return failure.
- **Thread Safety**: All methods are thread-safe via concurrent dictionary and stateless operations.
- **Extensibility**: The interface allows swapping the in-memory implementation with a persistent store (e.g., database, event-sourced).

## Cross-References
- [[Agent]] — Agents raise approval requests during workflow execution.
- [[Runtime]] — The host runtime that pauses and resumes agent runs based on approval outcomes.
- [[WorkflowExecution]] — Approval requests are part of the workflow lifecycle; see HITL pattern.

## Source Files
- `AGXIOME.Runtime/UI/ApprovalService.cs` (ingested 2026-05-14)