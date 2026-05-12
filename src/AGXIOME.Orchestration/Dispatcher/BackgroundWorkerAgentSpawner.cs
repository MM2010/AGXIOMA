using AGXIOME.Core.Execution;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Dispatcher;

/// <summary>
/// IAgentSpawner implementation backed by the existing BackgroundRunWorker.
/// Bridges the orchestration layer to the core execution infrastructure without coupling them.
/// The JobId returned is the BackgroundRunWorker's JobId string.
/// </summary>
public sealed class BackgroundWorkerAgentSpawner : IAgentSpawner
{
    private readonly IBackgroundRunWorker _worker;

    public BackgroundWorkerAgentSpawner(IBackgroundRunWorker worker) => _worker = worker;

    public Task<string> SpawnAsync(TaskSpec spec, CancellationToken ct = default)
    {
        // Build context dict from TaskSpec so the worker can reconstruct it
        var ctx = new Dictionary<string, object>
        {
            ["taskId"]  = spec.TaskId.ToString(),
            ["planId"]  = spec.PlanId.ToString(),
            ["stepIndex"] = spec.StepIndex,
            ["temperature"] = spec.Temperature,
            ["maxTokens"] = spec.MaxTokens,
            ["maxIterations"] = spec.MaxIterations,
            ["allowedToolNames"] = spec.AllowedToolNames,
            ["preferredLLMProvider"] = spec.PreferredLLMProvider ?? "",
            ["systemPrompt"] = spec.SystemPrompt ?? ""
        };
        if (spec.Metadata is not null)
            ctx["metadata"] = spec.Metadata;

        var job = new RunJob(
            JobId: Guid.NewGuid().ToString("N"),
            JobType: BackgroundJobType.RunExecution,
            AgentId: $"sub-agent-{spec.TaskId:N}",
            Prompt: spec.Description + (spec.PriorContext is not null ? $"\n\nContext from prior steps:\n{spec.PriorContext}" : ""),
            SessionId: spec.PlanId.ToString(),
            Context: ctx
        );

        var jobId = _worker.Enqueue(job);
        return Task.FromResult(jobId);
    }

    public Task CancelAsync(string jobId, CancellationToken ct = default)
    {
        _worker.Cancel(jobId);
        return Task.CompletedTask;
    }

    public Task<bool> IsActiveAsync(string jobId, CancellationToken ct = default)
    {
        var state = _worker.GetState(jobId);
        var isActive = state is not null &&
                       state.Status is not BackgroundJobStatus.Completed and
                                       not BackgroundJobStatus.Failed and
                                       not BackgroundJobStatus.Cancelled;
        return Task.FromResult(isActive);
    }
}
