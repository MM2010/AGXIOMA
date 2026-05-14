using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.SubAgent;

/// <summary>
/// Sub-agent tool — step 2 of 2.
/// Reads the result already saved by SaveResultTool from the hot store
/// and publishes it to ICompletionNotifier so the Master can receive it.
/// Separation from SaveResultTool preserves single-responsibility:
/// saving and notifying are independent concerns.
/// </summary>
public sealed class NotifyCompletionTool : IAgxiomeExecutableTool
{
    public string Name => "NotifyCompletion";
    public string Description => "Reads the saved task result from hot store and publishes it to the Master via ICompletionNotifier";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "subagent"];

    private readonly ICompletionNotifier _notifier;
    private readonly IHotStore _hot;

    public NotifyCompletionTool(ICompletionNotifier notifier, IHotStore hot)
    {
        _notifier = notifier;
        _hot = hot;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("taskId", out var tidObj) || !Guid.TryParse(tidObj?.ToString(), out var taskId))
            return Fail("Parameter 'taskId' (Guid) is required", sw.Elapsed);

        if (!p.TryGetValue("planId", out var pidObj) || !Guid.TryParse(pidObj?.ToString(), out var planId))
            return Fail("Parameter 'planId' (Guid) is required", sw.Elapsed);

        // Read result from hot store — must have been saved by SaveResultTool first
        var resultJson = await _hot.GetAsync(HotKeys.TaskResult(taskId), ct);
        if (resultJson is null)
            return Fail($"No saved result found for taskId {taskId}. Call SaveResult before NotifyCompletion.", sw.Elapsed);

        var result = OrchJson.Deserialize<TaskResult>(resultJson);
        if (result is null)
            return Fail($"Failed to deserialise TaskResult for taskId {taskId}", sw.Elapsed);

        // Publish to Master
        await _notifier.PublishAsync(result, ct);

        // Update status key in hot store
        var finalStatus = result.Success
            ? OrchestratorTaskStatus.Done.ToString()
            : OrchestratorTaskStatus.Failed.ToString();
        await _hot.SetAsync(HotKeys.TaskStatus(taskId), finalStatus, TimeSpan.FromHours(48), ct);

        return new ToolResult(true,
            new { notified = true, taskId = taskId.ToString(), planId = planId.ToString(), success = result.Success },
            ExecutionTime: sw.Elapsed,
            Metadata: new ToolMetadata(Name, Version));
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("NotifyCompletion", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["taskId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["planId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("taskId", "planId")
    };
}
