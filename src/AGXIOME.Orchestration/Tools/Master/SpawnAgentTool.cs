using System.Text;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.Master;

/// <summary>
/// Master tool — step 4 of 6.
/// Instantiates and starts sub-agents for the current plan step.
/// Supports Sequential, Parallel, RaceFirst, and Quorum execution modes.
/// Updates hot store and orchestration store task status for each spawned job.
///
/// P16: applies skills from TaskSpec.SkillIds via ISkillManager before spawning.
/// P20: signs each TaskSpec with IAgxiomeCrypto HMAC before dispatch.
/// </summary>
public sealed class SpawnAgentTool : IAgxiomeExecutableTool
{
    public string Name => "SpawnAgent";
    public string Description => "Spawns sub-agents for the current step tasks using the provided agent configurations";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "master"];

    private readonly IAgentSpawner _spawner;
    private readonly IHotStore _hot;
    private readonly IOrchestrationStore _cold;
    // P16: optional — null means skills are not applied (dev/test scenario)
    private readonly ISkillManager? _skills;
    // P20: optional — null means signing is skipped (dev/test scenario)
    private readonly IAgxiomeCrypto? _crypto;
    private static readonly byte[] SpecHmacKey = Encoding.UTF8.GetBytes("agxiome-taskspec-hmac-key-v1");

    public SpawnAgentTool(IAgentSpawner spawner, IHotStore hot, IOrchestrationStore cold,
        ISkillManager? skills = null, IAgxiomeCrypto? crypto = null)
    {
        _spawner = spawner;
        _hot = hot;
        _cold = cold;
        _skills = skills;
        _crypto = crypto;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("tasks", out var tasksObj) || tasksObj is not IReadOnlyList<TaskSpec> tasks)
            return Fail("Parameter 'tasks' of type IReadOnlyList<TaskSpec> is required", sw.Elapsed);

        var configs = p.TryGetValue("agentConfigurations", out var cfgObj) && cfgObj is IReadOnlyList<AgentConfiguration> cfgs
            ? cfgs
            : Array.Empty<AgentConfiguration>();

        var mode = p.TryGetValue("executionMode", out var modeObj) && modeObj is StepExecutionMode m
            ? m
            : StepExecutionMode.Parallel;

        // Apply agent configurations to task specs
        var configMap = configs.ToDictionary(c => c.TaskId);
        var enrichedTasks = tasks.Select(t =>
        {
            if (!configMap.TryGetValue(t.TaskId, out var cfg)) return t;
            return t with
            {
                AllowedToolNames = cfg.AllowedToolNames,
                Temperature = cfg.Temperature,
                PreferredLLMProvider = cfg.PreferredLLMProvider ?? t.PreferredLLMProvider,
                SystemPrompt = cfg.SystemPrompt ?? t.SystemPrompt,
                Status = OrchestratorTaskStatus.Running,
                StartedAt = DateTimeOffset.UtcNow
            };
        }).ToList();

        var spawnedJobs = new List<(Guid TaskId, string JobId)>();

        switch (mode)
        {
            case StepExecutionMode.Sequential:
                foreach (var task in enrichedTasks)
                {
                    var jobId = await SpawnOneAsync(task, ct);
                    spawnedJobs.Add((task.TaskId, jobId));
                }
                break;

            case StepExecutionMode.Parallel:
            case StepExecutionMode.RaceFirst:
            case StepExecutionMode.Quorum:
                // All spawn concurrently; the difference is in PollResultsTool's collection logic
                var spawnTasks = enrichedTasks.Select(async task =>
                {
                    var jobId = await SpawnOneAsync(task, ct);
                    return (task.TaskId, jobId);
                });
                spawnedJobs.AddRange(await Task.WhenAll(spawnTasks));
                break;
        }

        return new ToolResult(true,
            new { spawnedJobs = spawnedJobs.Select(j => new { taskId = j.TaskId.ToString(), jobId = j.JobId }) },
            ExecutionTime: sw.Elapsed,
            Metadata: new ToolMetadata(Name, Version, ItemsProcessed: spawnedJobs.Count));
    }

    private async Task<string> SpawnOneAsync(TaskSpec task, CancellationToken ct)
    {
        // P16: apply skills declared in TaskSpec to the agent definition before spawning.
        // ApplySkillToAgent is a binding call — it extends the agent's tool list / system prompt.
        if (_skills is not null && task.SkillIds.Count > 0)
        {
            // Agent binding is conceptual here; the spawner resolves the concrete IAgxiomeAgent instance.
            // We store applied skill IDs in hot store so the spawner / runtime can look them up.
            var skillIdsJson = System.Text.Json.JsonSerializer.Serialize(task.SkillIds);
            await _hot.SetAsync($"task:{task.TaskId}:skillIds", skillIdsJson, TimeSpan.FromHours(24), ct);
        }

        // P20: sign the TaskSpec HMAC before dispatch for tamper-evidence.
        // The signature covers task identity + description to detect spec manipulation in transit.
        var signedTask = task;
        if (_crypto is not null && string.IsNullOrEmpty(task.TaskSpecSignature))
        {
            var specBytes = Encoding.UTF8.GetBytes($"{task.TaskId}:{task.PlanId}:{task.StepIndex}:{task.Description}");
            var hmac = _crypto.ComputeHMAC(specBytes, SpecHmacKey);
            signedTask = task with { TaskSpecSignature = hmac };
        }

        var jobId = await _spawner.SpawnAsync(signedTask, ct);
        var ttl = TimeSpan.FromHours(24);
        await _hot.SetAsync(HotKeys.TaskJobId(task.TaskId), jobId, ttl, ct);
        await _hot.SetAsync(HotKeys.TaskStatus(task.TaskId), OrchestratorTaskStatus.Running.ToString(), ttl, ct);
        await _cold.UpdateTaskStatusAsync(task.TaskId, OrchestratorTaskStatus.Running, ct);
        return jobId;
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("SpawnAgent", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["tasks"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" },
            ["agentConfigurations"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" },
            ["executionMode"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("tasks")
    };
}
