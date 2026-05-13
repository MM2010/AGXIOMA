using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AGXIOME.Core.Patterns;

/// <summary>
/// AGXIOMA HierarchicalSwarm — Director → Workers → Evaluator feedback loop.
///
/// Flow:
///   1. Director LLM decomposes task into structured subtasks (JSON)
///   2. Workers execute subtasks in parallel (or sequential if ordered)
///   3. Evaluator LLM checks each worker output — can request revision
///   4. Up to MaxFeedbackRounds revisions per subtask
///   5. Director synthesises all approved outputs
///
/// Differenziatori vs Swarms:
///   - FeedbackLoop with structured critique and retry
///   - Typed SubtaskPlan parsed from director JSON output
///   - Per-subtask timeout and retry budget
///   - EvaluationMode: Pass/Fail / Scored / LLMCritic
///   - OnSubtaskComplete callback for monitoring
/// </summary>
public sealed class HierarchicalSwarm
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly IAgxiomeAgent _director;
    private readonly IReadOnlyList<IAgxiomeAgent> _workers;
    private readonly IAgxiomeAgent? _evaluator;
    private readonly HierarchicalConfig _config;

    public HierarchicalSwarm(
        IAgxiomeAgent director,
        IReadOnlyList<IAgxiomeAgent> workers,
        HierarchicalConfig? config = null,
        IAgxiomeAgent? evaluator = null)
    {
        _director = director;
        _workers = workers.Count > 0 ? workers : throw new ArgumentException("At least one worker required.");
        _evaluator = evaluator;
        _config = config ?? new HierarchicalConfig();
    }

    public async Task<HierarchicalResult> RunAsync(string objective, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Step 1: Director decomposes objective
        var planPrompt = BuildDirectorPrompt(objective);
        var planResp = await _director.RunAsync(new AgentRequest(planPrompt), ct);
        var subtasks = ParseSubtasks(planResp.Content, objective);

        // Step 2: Workers execute subtasks
        var subtaskResults = new List<SubtaskResult>();
        if (_config.ParallelExecution)
        {
            var parallel = await Task.WhenAll(subtasks.Select(s => ExecuteSubtaskAsync(s, objective, ct)));
            subtaskResults.AddRange(parallel);
        }
        else
        {
            foreach (var subtask in subtasks)
                subtaskResults.Add(await ExecuteSubtaskAsync(subtask, objective, ct));
        }

        // Step 3: Director synthesises
        var synthesisPrompt = BuildSynthesisPrompt(objective, subtaskResults);
        var finalResp = await _director.RunAsync(new AgentRequest(synthesisPrompt), ct);

        return new HierarchicalResult(
            FinalOutput: finalResp.Content,
            DirectorPlan: planResp.Content,
            SubtaskResults: subtaskResults,
            Duration: sw.Elapsed,
            SuccessRate: (double)subtaskResults.Count(r => r.Approved) / Math.Max(1, subtaskResults.Count)
        );
    }

    private async Task<SubtaskResult> ExecuteSubtaskAsync(
        SubtaskPlan subtask, string objective, CancellationToken ct)
    {
        // Pick worker (round-robin or by specialisation hint)
        var worker = SelectWorker(subtask);
        var sw = Stopwatch.StartNew();
        string workerOutput = "";
        bool approved = false;
        string? critique = null;

        for (int round = 0; round <= _config.MaxFeedbackRounds; round++)
        {
            var prompt = round == 0
                ? BuildWorkerPrompt(objective, subtask, null)
                : BuildWorkerPrompt(objective, subtask, critique);

            try
            {
                var resp = await worker.RunAsync(new AgentRequest(prompt), ct);
                workerOutput = resp.Content;
            }
            catch (Exception ex)
            {
                workerOutput = $"[ERROR: {ex.Message}]";
                break;
            }

            // Evaluate output
            if (_evaluator is not null)
            {
                var evalPrompt = BuildEvalPrompt(subtask, workerOutput);
                var evalResp = await _evaluator.RunAsync(new AgentRequest(evalPrompt), ct);
                (approved, critique) = ParseEvalResponse(evalResp.Content);
                if (approved) break;
            }
            else
            {
                approved = workerOutput.Length > 50 && !workerOutput.StartsWith("[ERROR");
                break;
            }
        }

        _config.OnSubtaskComplete?.Invoke(subtask, workerOutput, approved);

        return new SubtaskResult(
            SubtaskId: subtask.Id,
            Description: subtask.Description,
            WorkerId: worker.Id,
            Output: workerOutput,
            Approved: approved,
            Critique: critique,
            Duration: sw.Elapsed,
            FeedbackRounds: _config.MaxFeedbackRounds
        );
    }

    private IAgxiomeAgent SelectWorker(SubtaskPlan subtask)
    {
        if (_workers.Count == 1) return _workers[0];
        // Try to match worker by specialisation hint (agent ID contains role keyword)
        if (!string.IsNullOrWhiteSpace(subtask.PreferredRole))
        {
            var match = _workers.FirstOrDefault(w =>
                w.Id.Contains(subtask.PreferredRole, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        // Fallback: round-robin based on subtask index
        return _workers[subtask.Index % _workers.Count];
    }

    private static string BuildDirectorPrompt(string objective)
        => $"You are the Director Agent. Your job is to decompose the following objective into clear subtasks.\n\nOBJECTIVE: {objective}\n\nOutput a JSON array of subtasks. Each subtask must have:\n- \"id\": unique identifier (e.g. \"T1\", \"T2\")\n- \"description\": what the worker must do\n- \"preferred_role\": optional keyword (researcher/writer/coder)\n- \"dependencies\": array of subtask ids (can be empty)\n\nExample: [{{\"id\":\"T1\",\"description\":\"Research topic\",\"preferred_role\":\"researcher\",\"dependencies\":[]}},{{\"id\":\"T2\",\"description\":\"Write report\",\"preferred_role\":\"writer\",\"dependencies\":[\"T1\"]}}]\n\nRespond ONLY with the JSON array. No markdown, no commentary.";

    private static string BuildWorkerPrompt(string objective, SubtaskPlan subtask, string? critique) =>
        critique is null
            ? $"OBJECTIVE: {objective}\n\nYOUR TASK: {subtask.Description}\n\nComplete this task with high quality and detail."
            : $"OBJECTIVE: {objective}\n\nYOUR TASK: {subtask.Description}\n\nPREVIOUS FEEDBACK (your previous answer was rejected):\n{critique}\n\nRevise your answer addressing the feedback.";

    private static string BuildEvalPrompt(SubtaskPlan subtask, string output) => $"""
        Evaluate this worker output for the task: {subtask.Description}

        WORKER OUTPUT:
        {output}

        Respond with:
        APPROVED: true/false
        CRITIQUE: <specific feedback if not approved, empty if approved>
        """;

    private static string BuildSynthesisPrompt(string objective, List<SubtaskResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"OBJECTIVE: {objective}");
        sb.AppendLine("\nWorker outputs:");
        foreach (var r in results.Where(r => r.Approved))
            sb.AppendLine($"\n## {r.SubtaskId}: {r.Description}\n{r.Output}");
        sb.AppendLine("\nSynthesise all worker outputs into a single comprehensive final answer for the original objective.");
        return sb.ToString();
    }

    private static List<SubtaskPlan> ParseSubtasks(string planContent, string objectiveFallback)
    {
        try
        {
            var jsonStart = planContent.IndexOf('[');
            var jsonEnd = planContent.LastIndexOf(']') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = planContent[jsonStart..jsonEnd];
                var items = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? new();
                return items.Select((el, i) => new SubtaskPlan(
                    Id: el.TryGetProperty("id", out var id) ? id.GetString() ?? $"T{i + 1}" : $"T{i + 1}",
                    Description: el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    PreferredRole: el.TryGetProperty("preferred_role", out var r) ? r.GetString() : null,
                    Dependencies: el.TryGetProperty("dependencies", out var dep)
                        ? dep.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                        : new List<string>(),
                    Index: i
                )).ToList();
            }
        }
        catch { /* fallback */ }

        // Fallback: single subtask
        return [new SubtaskPlan("T1", objectiveFallback, null, new List<string>(), 0)];
    }

    private static (bool approved, string? critique) ParseEvalResponse(string content)
    {
        var approved = content.Contains("APPROVED: true", StringComparison.OrdinalIgnoreCase);
        var critiqueMatch = System.Text.RegularExpressions.Regex.Match(content, @"CRITIQUE:\s*(.+)", System.Text.RegularExpressions.RegexOptions.Singleline);
        var critique = critiqueMatch.Success ? critiqueMatch.Groups[1].Value.Trim() : null;
        return (approved, approved ? null : critique);
    }
}

public sealed record HierarchicalConfig
{
    public bool ParallelExecution { get; init; } = true;
    public int MaxFeedbackRounds { get; init; } = 2;
    public Action<SubtaskPlan, string, bool>? OnSubtaskComplete { get; init; }
}

public record SubtaskPlan(string Id, string Description, string? PreferredRole, List<string> Dependencies, int Index);
public record SubtaskResult(
    string SubtaskId, string Description, string WorkerId,
    string Output, bool Approved, string? Critique,
    TimeSpan Duration, int FeedbackRounds
);
public record HierarchicalResult(
    string FinalOutput, string DirectorPlan,
    IReadOnlyList<SubtaskResult> SubtaskResults,
    TimeSpan Duration, double SuccessRate
);
