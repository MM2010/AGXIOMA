using System.Diagnostics.Metrics;

namespace AGXIOME.Orchestration.Monitoring;

/// <summary>
/// P21: OpenTelemetry metrics for the orchestration layer.
/// Follows the same meter pattern as AgxiomeMetrics (AGXIOME.Telemetry) but scoped to plan execution.
/// Consumers: OTel exporters (OTLP/Prometheus), alerting rules, dashboards.
///
/// Emit locations:
///   PlanOrchestrator.StartAsync()           → PlansStarted++, ActivePlans+1
///   PlanOrchestrator.ContinuePlanLoopAsync  → StepDuration per step
///   PlanOrchestrator FailPlan()             → PlansFailed++, ActivePlans-1
///   PlanOrchestrator CompletePlan()         → PlansCompleted++, ActivePlans-1
///   SpawnAgentTool.SpawnOneAsync()          → TasksSpawned++
///   PollResultsTool (HITL branch)           → HitlPauses++
/// </summary>
public static class OrchestratorMetrics
{
    public static readonly Meter Meter = new("AGXIOME.Orchestration", "1.0.0");

    // ── Counters ──────────────────────────────────────────────────────────

    /// <summary>Total plans started (includes restarts from resume).</summary>
    public static readonly Counter<int> PlansStarted =
        Meter.CreateCounter<int>("agxiome.orchestration.plans.started",
            description: "Total number of plans started by the orchestrator.");

    /// <summary>Plans that reached PlanCompletedEvent.</summary>
    public static readonly Counter<int> PlansCompleted =
        Meter.CreateCounter<int>("agxiome.orchestration.plans.completed",
            description: "Total number of plans completed successfully.");

    /// <summary>Plans that reached PlanFailedEvent.</summary>
    public static readonly Counter<int> PlansFailed =
        Meter.CreateCounter<int>("agxiome.orchestration.plans.failed",
            description: "Total number of plans that failed.");

    /// <summary>Individual tasks spawned across all plans.</summary>
    public static readonly Counter<int> TasksSpawned =
        Meter.CreateCounter<int>("agxiome.orchestration.tasks.spawned",
            description: "Total number of sub-agent tasks spawned.");

    /// <summary>Tasks paused waiting for HITL resolution.</summary>
    public static readonly Counter<int> HitlPauses =
        Meter.CreateCounter<int>("agxiome.orchestration.tasks.hitl_paused",
            description: "Number of tasks paused for human-in-the-loop approval.");

    // ── Histograms ────────────────────────────────────────────────────────

    /// <summary>Duration of a single step execution (spawn → all results collected), in milliseconds.</summary>
    public static readonly Histogram<double> StepDuration =
        Meter.CreateHistogram<double>("agxiome.orchestration.step.duration", "ms",
            description: "Duration of a single orchestration step from spawn to results collection.");

    /// <summary>End-to-end plan duration from StartAsync to terminal state, in milliseconds.</summary>
    public static readonly Histogram<double> PlanDuration =
        Meter.CreateHistogram<double>("agxiome.orchestration.plan.duration", "ms",
            description: "End-to-end plan execution duration.");

    // ── UpDown Counters ───────────────────────────────────────────────────

    /// <summary>Number of plans currently executing (fire-and-forget loops in flight).</summary>
    public static readonly UpDownCounter<int> ActivePlans =
        Meter.CreateUpDownCounter<int>("agxiome.orchestration.plans.active",
            description: "Number of plans currently executing.");
}
