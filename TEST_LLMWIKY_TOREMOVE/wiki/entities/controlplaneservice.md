---
title: ControlPlaneService
category: entity
tags: [control-plane, ui, dashboard, projection, read-model]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ControlPlaneService

## Summary
The `ControlPlaneService` is a read-only projection layer that provides aggregated dashboard data for the web UI. It exposes agent statuses, active runs, team statuses, knowledge store statistics, and a combined dashboard summary. The default implementation uses in-memory stubs for development and testing; production deployments replace it with a Redis-cached read model.

## Key Details
- Defined in namespace `AGXIOME.Runtime.UI` (source: `AGXIOME.Runtime/UI/ControlPlaneService.cs`).
- Service contract is `IControlPlaneService` with methods: `GetDashboardSummaryAsync`, `GetAgentStatusesAsync`, `GetActiveRunsAsync`, `GetTeamStatusesAsync`, `GetKnowledgeSummaryAsync`.
- Projection records: `AgentStatus`, `RunSummary`, `TeamStatus`, `KnowledgeSummary`, `DashboardSummary`.
- The `ControlPlaneService` class implements `IControlPlaneService` and includes `Seed` methods for injecting test data.
- `GetActiveRunsAsync` filters runs to those with status `"Running"` or `"Pending"`.
- `GetDashboardSummaryAsync` aggregates counts from agents, runs, teams, and the knowledge summary.
- Designed as a "Read-Only Projection Layer" (tagged with "MU-06") for the web dashboard.
- Production usage expects a Redis-based or other fast query store, not the in-memory stubs.

## Cross-References
- [[Agent]] — status data comes from agent lifecycle events.
- [[Run]] — active runs are derived from orchestration state.
- [[Team]] — team composition and coordination counts.
- [[KnowledgeStore]] — provides the collection/document/chunk counts.
- [[DashboardController]] — likely consumer of this service in the web layer.

## Source Files
- `AGXIOME.Runtime/UI/ControlPlaneService.cs` (ingested 2026-05-14)