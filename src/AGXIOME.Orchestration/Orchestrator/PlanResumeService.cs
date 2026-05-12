using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Orchestration.Orchestrator;

/// <summary>
/// Hosted service that detects Running/Paused plans from the orchestration store on startup
/// and resumes them. Handles crash recovery: if the process dies mid-plan, this service
/// picks up where it left off using the canonical store as the source of truth.
/// </summary>
public sealed class PlanResumeService : IHostedService
{
    private readonly IPlanOrchestrator _orchestrator;
    private readonly IOrchestrationStore _cold;
    private readonly ILogger<PlanResumeService> _log;

    public PlanResumeService(
        IPlanOrchestrator orchestrator,
        IOrchestrationStore cold,
        ILogger<PlanResumeService> log)
    {
        _orchestrator = orchestrator;
        _cold = cold;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var activePlans = await _cold.GetActivePlansAsync(cancellationToken);
        if (activePlans.Count == 0)
        {
            _log.LogDebug("PlanResumeService: no active plans to resume");
            return;
        }

        _log.LogInformation("PlanResumeService: resuming {Count} active plan(s)", activePlans.Count);

        foreach (var plan in activePlans)
        {
            try
            {
                await _orchestrator.ResumeAsync(plan.PlanId, cancellationToken);
                _log.LogInformation("Resumed plan {PlanId} (step {Step}/{Max})",
                    plan.PlanId, plan.CurrentStep, plan.MaxStepIndex);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to resume plan {PlanId}", plan.PlanId);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
