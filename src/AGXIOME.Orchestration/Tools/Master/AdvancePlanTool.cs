using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.Master;

/// <summary>
/// Master tool — step 6 of 6.
/// Analyses step results, synthesises context for the next step, and decides:
///   Advance → move to next step and inject synthesised context into task specs
///   Complete → emit final summary and close the plan
///   Fail → cancel all active jobs and mark plan failed
///   Retry → re-queue failed tasks in the same step
/// Uses the LLM to make the advance/complete decision based on result summaries.
/// </summary>
public sealed class AdvancePlanTool : IAgxiomeExecutableTool
{
    public string Name => "AdvancePlan";
    public string Description => "Evaluates step results and advances, completes, fails, or retries the plan";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "master"];

    private readonly ILLMProvider _llm;
    private readonly IOrchestrationStore _cold;
    private readonly IHotStore _hot;
    private readonly IAgentSpawner _spawner;

    public AdvancePlanTool(ILLMProvider llm, IOrchestrationStore cold, IHotStore hot, IAgentSpawner spawner)
    {
        _llm = llm;
        _cold = cold;
        _hot = hot;
        _spawner = spawner;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("planId", out var pidObj) || !Guid.TryParse(pidObj?.ToString(), out var planId))
            return Fail("Parameter 'planId' (Guid) is required", sw.Elapsed);

        if (!p.TryGetValue("completedResults", out var resObj) || resObj is not IReadOnlyList<TaskResult> stepResults)
            return Fail("Parameter 'completedResults' of type IReadOnlyList<TaskResult> is required", sw.Elapsed);

        var isLastStep = p.TryGetValue("isLastStep", out var ilsObj) && ilsObj is bool ils && ils;
        var currentStep = p.TryGetValue("currentStep", out var csObj) && int.TryParse(csObj?.ToString(), out var csi) ? csi : 0;

        // Build synthesis prompt from result summaries
        var summaries = string.Join("\n\n", stepResults.Select((r, i) =>
            $"Task {i + 1} ({(r.Success ? "SUCCESS" : "FAILED")}): {r.Summary ?? r.Output[..Math.Min(r.Output.Length, 300)]}"));

        var systemPrompt =
            $$"""
            You are an orchestration controller. Evaluate these task results from step {{currentStep}} and decide the next action.
            
            Valid decisions:
            - "Advance": results are sufficient, proceed to next step
            - "Complete": this was the last step and results are satisfactory — provide a final summary
            - "Fail": results are unrecoverable, cannot proceed
            - "Retry": some tasks failed but should be retried
            
            Respond ONLY with valid JSON:
            {
              "decision": "Advance|Complete|Fail|Retry",
              "synthesizedContext": "string — concise synthesis of results for the next step",
              "finalSummary": "string — only if decision is Complete",
              "retryTaskIds": ["guid"] -- only if decision is Retry
            }
            """;

        var userPrompt = $"Step {currentStep} results:\n{summaries}\n\nIs this the last step: {isLastStep}";

        var llmResponse = await _llm.CompleteAsync(new LLMCompletionRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            History: null,
            Temperature: 0.1,
            MaxTokens: 1024,
            Model: "",
            Tools: null
        ), ct);

        AdvanceResponse advResp;
        try
        {
            advResp = ParseResponse(llmResponse.Content);
        }
        catch (Exception ex)
        {
            return Fail($"Failed to parse advance decision: {ex.Message}", sw.Elapsed);
        }

        var resultMap = stepResults.ToDictionary(r => r.TaskId);
        var newStep = currentStep + 1;

        switch (advResp.Decision)
        {
            case AdvanceDecision.Advance:
                await _cold.AdvancePlanStepAsync(planId, newStep, resultMap, ct);
                await _hot.SetAsync(HotKeys.PlanStep(planId), newStep.ToString(), TimeSpan.FromHours(72), ct);
                await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Running.ToString(), TimeSpan.FromHours(72), ct);
                break;

            case AdvanceDecision.Complete:
                await _cold.AdvancePlanStepAsync(planId, newStep, resultMap, ct);
                await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.Completed, advResp.FinalSummary, ct);
                await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Completed.ToString(), TimeSpan.FromHours(72), ct);
                break;

            case AdvanceDecision.Fail:
                await _cold.UpdatePlanStatusAsync(planId, OrchestratorPlanStatus.Failed, ct: ct);
                await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Failed.ToString(), TimeSpan.FromHours(72), ct);
                // Cancel all active jobs for this plan
                var taskIds = await _hot.SetMembersAsync(HotKeys.PlanTasks(planId), ct);
                foreach (var tidStr in taskIds)
                {
                    if (!Guid.TryParse(tidStr, out var tid)) continue;
                    var jobId = await _hot.GetAsync(HotKeys.TaskJobId(tid), ct);
                    if (jobId is not null)
                        await _spawner.CancelAsync(jobId, ct);
                }
                break;

            case AdvanceDecision.Retry:
                // Status stays Running; SpawnAgentTool will be called again by the orchestrator
                await _hot.SetAsync(HotKeys.PlanStatus(planId), OrchestratorPlanStatus.Running.ToString(), TimeSpan.FromHours(72), ct);
                break;
        }

        return new ToolResult(true, new
        {
            decision = advResp.Decision.ToString(),
            nextStep = advResp.Decision == AdvanceDecision.Advance ? newStep : currentStep,
            synthesizedContext = advResp.SynthesizedContext,
            finalSummary = advResp.FinalSummary,
            retryTaskIds = advResp.RetryTaskIds
        },
        ExecutionTime: sw.Elapsed,
        Metadata: new ToolMetadata(Name, Version));
    }

    private static AdvanceResponse ParseResponse(string json)
    {
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```")) cleaned = cleaned[(cleaned.IndexOf('\n') + 1)..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..cleaned.LastIndexOf("```")].TrimEnd();

        using var doc = System.Text.Json.JsonDocument.Parse(cleaned);
        var root = doc.RootElement;

        var decision = root.TryGetProperty("decision", out var d)
            ? Enum.Parse<AdvanceDecision>(d.GetString() ?? "Advance", ignoreCase: true)
            : AdvanceDecision.Advance;

        var context = root.TryGetProperty("synthesizedContext", out var sc) ? sc.GetString() : null;
        var summary = root.TryGetProperty("finalSummary", out var fs) ? fs.GetString() : null;
        var retryIds = root.TryGetProperty("retryTaskIds", out var ri)
            ? ri.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : new List<string>();

        return new AdvanceResponse(decision, context, summary, retryIds);
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("AdvancePlan", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["planId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["completedResults"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" },
            ["isLastStep"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "boolean" },
            ["currentStep"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "integer" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("planId", "completedResults")
    };

    private sealed record AdvanceResponse(
        AdvanceDecision Decision,
        string? SynthesizedContext,
        string? FinalSummary,
        IReadOnlyList<string> RetryTaskIds
    );
}
