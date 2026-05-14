using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Scheduling;

/// <summary>
/// P17: Bridges the scheduling layer (ScheduleManager) with IPlanOrchestrator.
/// When a schedule fires and its SchedulePayload.PlanRequest is set,
/// this adapter calls IPlanOrchestrator.StartAsync to launch an orchestrated plan.
///
/// Uses a delegate to avoid a direct assembly reference from AGXIOME.Core to AGXIOME.Orchestration.
/// The host project wires the factory during DI setup.
///
/// Usage example (in host DI setup, after both projects are referenced):
///   services.AddSingleton&lt;IScheduledPlanBridge&gt;(sp =>
///   {
///       var orchestrator = sp.GetRequiredService&lt;AGXIOME.Orchestration.Abstractions.IPlanOrchestrator&gt;();
///       return new PlanScheduleAdapter(
///           (req, sess, ct) => orchestrator.StartAsync(req, sess, ct: ct),
///           sp.GetRequiredService&lt;ILogger&lt;PlanScheduleAdapter&gt;&gt;());
///   });
/// </summary>
public sealed class PlanScheduleAdapter : IScheduledPlanBridge
{
    // Delegate signature matches IPlanOrchestrator.StartAsync minimal surface.
    // Using a delegate keeps AGXIOME.Core free of a reference to AGXIOME.Orchestration.
    private readonly Func<string, string, CancellationToken, Task<Guid>> _startPlan;
    private readonly ILogger<PlanScheduleAdapter> _log;

    public PlanScheduleAdapter(
        Func<string, string, CancellationToken, Task<Guid>> startPlan,
        ILogger<PlanScheduleAdapter> log)
    {
        _startPlan = startPlan;
        _log = log;
    }

    /// <summary>
    /// Called by SchedulePoller when a schedule fires.
    /// Only launches a plan if the payload carries a non-empty PlanRequest.
    /// </summary>
    public async Task<Guid?> TriggerAsync(Schedule schedule, CancellationToken ct = default)
    {
        if (schedule.Payload?.PlanRequest is not { Length: > 0 } request)
        {
            _log.LogDebug("Schedule {ScheduleId} fired but has no PlanRequest \u2014 skipping orchestrator.",
                schedule.Id);
            return null;
        }

        var sessionId = schedule.Payload.SessionId ?? $"schedule:{schedule.Id}";
        try
        {
            var planId = await _startPlan(request, sessionId, ct);
            _log.LogInformation("Schedule {ScheduleId} launched plan {PlanId} for session {SessionId}.",
                schedule.Id, planId, sessionId);
            return planId;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to launch plan for schedule {ScheduleId}.", schedule.Id);
            return null;
        }
    }
}

/// <summary>
/// Abstraction so PlanScheduleAdapter can be unit-tested without IPlanOrchestrator.
/// Defined in AGXIOME.Core to stay within the layer boundary.
/// </summary>
public interface IScheduledPlanBridge
{
    Task<Guid?> TriggerAsync(Schedule schedule, CancellationToken ct = default);
}
