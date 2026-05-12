using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.Master;

/// <summary>
/// Master tool — step 5 of 6.
/// Awaits task completions from ICompletionNotifier and persists each result.
/// Respects timeout — returns partial results if deadline expires.
/// Writes results to both hot store and canonical store on each arrival.
/// </summary>
public sealed class PollResultsTool : IAgxiomeExecutableTool
{
    public string Name => "PollResults";
    public string Description => "Awaits task completions from the notifier bus and persists results to hot/cold store";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "master"];

    private readonly ICompletionNotifier _notifier;
    private readonly IHotStore _hot;
    private readonly IOrchestrationStore _cold;

    public PollResultsTool(ICompletionNotifier notifier, IHotStore hot, IOrchestrationStore cold)
    {
        _notifier = notifier;
        _hot = hot;
        _cold = cold;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("planId", out var pidObj) || !Guid.TryParse(pidObj?.ToString(), out var planId))
            return Fail("Parameter 'planId' (Guid) is required", sw.Elapsed);

        if (!p.TryGetValue("expectedTaskIds", out var idsObj) || idsObj is not IReadOnlyList<Guid> expectedIds)
            return Fail("Parameter 'expectedTaskIds' of type IReadOnlyList<Guid> is required", sw.Elapsed);

        var timeoutSec = p.TryGetValue("timeoutSeconds", out var to) && int.TryParse(to?.ToString(), out var toi)
            ? toi
            : 300;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        var pending = new HashSet<Guid>(expectedIds);
        var completed = new List<TaskResult>();
        var failed = new List<TaskResult>();
        var timedOut = new List<Guid>();

        try
        {
            await foreach (var result in _notifier.SubscribeAsync(planId, timeoutCts.Token))
            {
                if (!pending.Contains(result.TaskId)) continue;

                // P15: HITL pause — task is not complete yet; signal waiting state and keep listening.
                // The task is NOT removed from pending; a resumed result will arrive after HITL resolution.
                if (result.IsHitlPaused)
                {
                    await _hot.SetAsync(HotKeys.TaskStatus(result.TaskId),
                        OrchestratorTaskStatus.Running.ToString(), TimeSpan.FromHours(72), ct);
                    await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.WaitingForHITL, ct: ct);
                    await _hot.SetAsync(HotKeys.PlanStatus(planId),
                        OrchestratorPlanStatus.WaitingForHITL.ToString(), TimeSpan.FromHours(72), ct);
                    // Record HITL context in hot store for diagnostic / reconnect purposes
                    if (result.HitlRequirementId is not null)
                        await _hot.SetAsync($"hitl:{result.HitlRequirementId}:taskId",
                            result.TaskId.ToString(), TimeSpan.FromHours(72), ct);
                    continue; // keep waiting — resumed result will arrive via IHitlCompletionSource
                }

                // Persist to both stores immediately on arrival
                await _hot.SetAsync(HotKeys.TaskResult(result.TaskId), OrchJson.Serialize(result), TimeSpan.FromHours(48), ct);
                await _hot.SetAsync(HotKeys.TaskStatus(result.TaskId),
                    result.Success ? OrchestratorTaskStatus.Done.ToString() : OrchestratorTaskStatus.Failed.ToString(),
                    TimeSpan.FromHours(48), ct);
                await _cold.SaveResultAsync(result, ct);
                await _cold.UpdateTaskStatusAsync(result.TaskId,
                    result.Success ? OrchestratorTaskStatus.Done : OrchestratorTaskStatus.Failed, ct);

                if (result.Success) completed.Add(result); else failed.Add(result);
                pending.Remove(result.TaskId);

                if (pending.Count == 0) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout — not parent cancellation
            timedOut.AddRange(pending);
        }

        var allComplete = timedOut.Count == 0 && pending.Count == 0;

        return new ToolResult(true, new
        {
            completedTasks = completed.Select(r => new { taskId = r.TaskId.ToString(), success = true, summary = r.Summary }),
            failedTasks = failed.Select(r => new { taskId = r.TaskId.ToString(), success = false, errorMessage = r.ErrorMessage }),
            timedOutTasks = timedOut.Select(id => id.ToString()),
            allComplete
        },
        ExecutionTime: sw.Elapsed,
        Metadata: new ToolMetadata(Name, Version, ItemsProcessed: completed.Count + failed.Count));
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("PollResults", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["planId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["expectedTaskIds"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" },
            ["timeoutSeconds"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "integer" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("planId", "expectedTaskIds")
    };
}
