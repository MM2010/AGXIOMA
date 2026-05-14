---
title: OrchestratorMetrics
category: entity
tags: [OpenTelemetry, metrics, orchestration, monitoring]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# OrchestratorMetrics

## Summary
`OrchestratorMetrics` is a static class that defines OpenTelmetry metrics for the AGXIOME orchestration layer, following the same meter pattern as [[AgxiomeMetrics]] but scoped to plan execution. It provides counters, histograms, and an up-down counter to track the lifecycle and performance of plans and tasks. These metrics are consumed by OTel exporters (OTLP/Prometheus), alerting systems, and dashboards. Emission points are distributed across `PlanOrchestrator`, `SpawnAgentTool`, and `PollResultsTool`.

## Key Details
- **Meter**: `new Meter("AGXIOME.Orchestration", "1.0.0")`
- **Counters** (5):
  - `PlansStarted` — increments on `PlanOrchestrator.StartAsync()`
  - `PlansCompleted` — increments on `PlanOrchestrator.CompletePlan()`
  - `PlansFailed` — increments on `PlanOrchestrator.FailPlan()`
  - `TasksSpawned` — increments on `SpawnAgentTool.SpawnOneAsync()`
  - `HitlPauses` — increments on `PollResultsTool` HITL branch
- **Histograms** (2):
  - `StepDuration` — records per-step execution time (ms)
  - `PlanDuration` — records end-to-end plan runtime (ms)
- **UpDown Counter** (1):
  - `ActivePlans` — +1 on `StartAsync()`, -1 on `CompletePlan()` / `FailPlan()`
- Metric names follow the pattern `agxiome.orchestration.{metric}` consistent with conventions.
- Designed for real-time monitoring and alerting on plan/task throughput, failure rates, and duration.

## Cross-References
- [[AgxiomeMetrics]] — base meter pattern and other telemetry metrics
- [[PlanOrchestrator]] — primary emitter of most plan lifecycle metrics
- [[SpawnAgentTool]] — emitter of `TasksSpawned`
- [[PollResultsTool]] — emitter of `HitlPauses`

## Source Files
- `AGXIOME.Orchestration/Monitoring/OrchestratorMetrics.cs` (ingested 2026-05-14)