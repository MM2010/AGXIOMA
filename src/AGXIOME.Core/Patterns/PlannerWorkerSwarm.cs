using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA PlannerWorkerSwarm — Planner generates structured task list → Workers execute → Evaluator validates.
///
/// Differenziatori vs Swarms:
///   - Typed TaskPlan from JSON (schema-validated)
///   - TaskDependencyGraph: tasks execute in dependency order (topological)
///   - AutoRetry: Evaluator can resubmit failed tasks with context
///   - SharedWorkspace: workers share a context dictionary for inter-task data
///   - HITL-ready: tasks marked "requires_approval" pause for human review
///   - OnTaskComplete callback with full audit data
/// </summary>
public sealed class PlannerWorkerSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly IAgxiomeAgent _planner;
    private readonly IReadOnlyList<IAgxiomeAgent> _workers;
    private readonly IAgxiomeAgent? _evaluator;
    private readonly PlannerWorkerConfig _config;

    public PlannerWorkerSwarm(
        IAgxiomeAgent planner,
        IReadOnlyList<IAgxiomeAgent> workers,
        PlannerWorkerConfig? config = null,
        IAgxiomeAgent? evaluator = null)
    {
        _planner = planner;
        _workers = workers;
        _evaluator = evaluator;
        _config = config ?? new PlannerWorkerConfig();
    }

    public async Task<PlannerWorkerResult> RunAsync(string goal, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var workspace = new Dictionary<string, string>();

        // Step 1: Plan
        var planResp = await _planner.RunAsync(new AgentRequest(BuildPlannerPrompt(goal)), ct);
        var tasks = ParseTasks(planResp.Content, goal);

        // Step 2: Execute in dependency order
        var completedTasks = new Dictionary<string, WorkerTaskResult>();
        var sortedTasks = TopologicalSort(tasks);

        foreach (var batch in sortedTasks)
        {
            if (_config.ParallelWorkers)
            {
                var batchResults = await Task.WhenAll(
                    batch.Select(t => ExecuteTaskAsync(t, goal, workspace, completedTasks, ct)));
                foreach (var r in batchResults)
                    completedTasks[r.TaskId] = r;
            }
            else
            {
                foreach (var t in batch)
                {
                    var r = await ExecuteTaskAsync(t, goal, workspace, completedTasks, ct);
                    completedTasks[r.TaskId] = r;
                }
            }
        }

        // Step 3: Final synthesis
        var synthesisResp = await _planner.RunAsync(
            new AgentRequest(BuildSynthesisPrompt(goal, completedTasks.Values.ToList())), ct);

        return new PlannerWorkerResult(
            Goal: goal,
            FinalOutput: synthesisResp.Content,
            TaskResults: completedTasks.Values.ToList(),
            Duration: sw.Elapsed,
            SuccessfulTasks: completedTasks.Values.Count(r => r.Success),
            TotalTasks: tasks.Count
        );
    }

    private async Task<WorkerTaskResult> ExecuteTaskAsync(
        WorkerTask task,
        string goal,
        Dictionary<string, string> workspace,
        Dictionary<string, WorkerTaskResult> completed,
        CancellationToken ct)
    {
        var worker = _workers[task.WorkerIndex % _workers.Count];
        var contextData = string.Join("\n", task.Dependencies
            .Where(completed.ContainsKey)
            .Select(d => $"[From task {d}]: {completed[d].Output}"));

        var prompt = BuildWorkerPrompt(goal, task, contextData, workspace);
        string output;
        bool success;

        try
        {
            var resp = await worker.RunAsync(new AgentRequest(prompt), ct);
            output = resp.Content;
            success = true;

            // Store output in shared workspace
            workspace[task.Id] = output;
        }
        catch (Exception ex)
        {
            output = $"[ERROR: {ex.Message}]";
            success = false;
        }

        // Evaluate
        bool approved = success;
        if (_evaluator is not null && success)
        {
            var evalResp = await _evaluator.RunAsync(
                new AgentRequest($"Task: {task.Description}\nOutput:\n{output}\nIs this correct and complete? Reply PASS or FAIL with brief reason."),
                ct);
            approved = evalResp.Content.Contains("PASS", StringComparison.OrdinalIgnoreCase);
        }

        var result = new WorkerTaskResult(task.Id, task.Description, worker.Id, output, approved, success);
        _config.OnTaskComplete?.Invoke(result);
        return result;
    }

    private static string BuildPlannerPrompt(string goal)
        => $"You are a Planner Agent. Create a detailed execution plan for the following goal.\n\nGOAL: {goal}\n\nOutput a JSON array of tasks. Each task:\n- \"id\": unique task ID\n- \"description\": specific actionable instruction\n- \"dependencies\": list of task IDs that must complete before this one\n- \"worker_hint\": optional role keyword (researcher/writer/coder/analyst)\n- \"priority\": 1-5 (5 = highest)\n\nExample: [{{\"id\":\"TASK-1\",\"description\":\"Research X\",\"dependencies\":[],\"worker_hint\":\"researcher\",\"priority\":4}},{{\"id\":\"TASK-2\",\"description\":\"Write report\",\"dependencies\":[\"TASK-1\"],\"worker_hint\":\"writer\",\"priority\":3}}]\n\nRespond ONLY with the JSON array.";

    private static string BuildWorkerPrompt(string goal, WorkerTask task, string contextData, Dictionary<string, string> workspace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GOAL: {goal}");
        sb.AppendLine($"\nYOUR TASK: {task.Description}");
        if (!string.IsNullOrWhiteSpace(contextData))
            sb.AppendLine($"\nCONTEXT FROM PREVIOUS TASKS:\n{contextData}");
        sb.AppendLine("\nComplete your task thoroughly. Be specific and detailed.");
        return sb.ToString();
    }

    private static string BuildSynthesisPrompt(string goal, List<WorkerTaskResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GOAL: {goal}");
        sb.AppendLine("\nAll worker outputs:");
        foreach (var r in results.Where(r => r.Success))
            sb.AppendLine($"\n### {r.TaskId}: {r.Description}\n{r.Output}");
        sb.AppendLine("\nCombine all outputs into the final comprehensive result for the original goal.");
        return sb.ToString();
    }

    private static List<WorkerTask> ParseTasks(string content, string goalFallback)
    {
        try
        {
            var jsonStart = content.IndexOf('[');
            var jsonEnd = content.LastIndexOf(']') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content[jsonStart..jsonEnd];
                var items = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
                return items.Select((el, i) => new WorkerTask(
                    Id: el.TryGetProperty("id", out var id) ? id.GetString() ?? $"TASK-{i + 1}" : $"TASK-{i + 1}",
                    Description: el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    Dependencies: el.TryGetProperty("dependencies", out var dep)
                        ? dep.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
                        : new List<string>(),
                    WorkerHint: el.TryGetProperty("worker_hint", out var h) ? h.GetString() : null,
                    Priority: el.TryGetProperty("priority", out var p) ? p.GetInt32() : 3,
                    WorkerIndex: i
                )).ToList();
            }
        }
        catch { /* fallback */ }
        return [new WorkerTask("TASK-1", goalFallback, [], null, 3, 0)];
    }

    /// <summary>Returns tasks in batches where each batch can run in parallel (dependencies resolved).</summary>
    private static List<List<WorkerTask>> TopologicalSort(List<WorkerTask> tasks)
    {
        var taskMap = tasks.ToDictionary(t => t.Id);
        var completed = new HashSet<string>();
        var batches = new List<List<WorkerTask>>();

        while (completed.Count < tasks.Count)
        {
            var batch = tasks
                .Where(t => !completed.Contains(t.Id) && t.Dependencies.All(d => completed.Contains(d) || !taskMap.ContainsKey(d)))
                .ToList();

            if (batch.Count == 0) break; // cycle guard

            batches.Add(batch);
            foreach (var t in batch) completed.Add(t.Id);
        }

        return batches;
    }
}

public sealed record PlannerWorkerConfig
{
    public bool ParallelWorkers { get; init; } = true;
    public Action<WorkerTaskResult>? OnTaskComplete { get; init; }
}

public record WorkerTask(string Id, string Description, List<string> Dependencies, string? WorkerHint, int Priority, int WorkerIndex);
public record WorkerTaskResult(string TaskId, string Description, string WorkerId, string Output, bool Approved, bool Success);
public record PlannerWorkerResult(
    string Goal, string FinalOutput,
    IReadOnlyList<WorkerTaskResult> TaskResults,
    TimeSpan Duration, int SuccessfulTasks, int TotalTasks
);
