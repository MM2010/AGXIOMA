using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.Master;

/// <summary>
/// Master tool — step 2 of 6.
/// Persists a decomposed plan into both hot store and orchestration store.
/// Initialises step counter and task status keys in the hot store.
/// </summary>
public sealed class PersistPlanTool : IAgxiomeExecutableTool
{
    public string Name => "PersistPlan";
    public string Description => "Persists the decomposed plan and all task specs to hot store and orchestration store";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "master"];

    private readonly IHotStore _hot;
    private readonly IOrchestrationStore _cold;

    public PersistPlanTool(IHotStore hot, IOrchestrationStore cold)
    {
        _hot = hot;
        _cold = cold;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("plan", out var planObj) || planObj is not PlanState plan)
            return Fail("Parameter 'plan' of type PlanState is required", sw.Elapsed);

        // 1. Persist to canonical store first
        await _cold.SavePlanAsync(plan, ct);
        foreach (var task in plan.Tasks)
            await _cold.SaveTaskSpecAsync(task, ct);

        // 2. Populate hot store for fast read access
        var ttl72h = TimeSpan.FromHours(72);
        var ttl24h = TimeSpan.FromHours(24);

        var planKey = HotKeys.PlanMeta(plan.PlanId);
        await _hot.SetAsync(planKey, OrchJson.Serialize(plan), ttl72h, ct);
        await _hot.SetAsync(HotKeys.PlanStatus(plan.PlanId), plan.Status.ToString(), ttl72h, ct);
        // IncrementAsync to 0 is not possible; we set "0" directly for step counter
        await _hot.SetAsync(HotKeys.PlanStep(plan.PlanId), "0", ttl72h, ct);

        foreach (var task in plan.Tasks)
        {
            await _hot.SetAsync(HotKeys.TaskSpec(task.TaskId), OrchJson.Serialize(task), ttl24h, ct);
            await _hot.SetAsync(HotKeys.TaskStatus(task.TaskId), task.Status.ToString(), ttl24h, ct);
            await _hot.SetAddAsync(HotKeys.PlanTasks(plan.PlanId), task.TaskId.ToString(), ct);
        }

        return new ToolResult(true,
            new { persisted = true, planId = plan.PlanId.ToString(), taskCount = plan.Tasks.Count, hotStoreTtlHours = 72 },
            ExecutionTime: sw.Elapsed,
            Metadata: new ToolMetadata(Name, Version, ItemsProcessed: plan.Tasks.Count + 1));
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("PersistPlan", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["plan"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "object", ["description"] = "PlanState instance" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("plan")
    };
}
