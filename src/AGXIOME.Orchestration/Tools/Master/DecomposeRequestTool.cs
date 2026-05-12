using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.Master;

/// <summary>
/// Master tool — step 1 of 6.
/// Transforms a user request into an ordered list of TaskSpec objects.
/// Does NOT persist — persistence is the responsibility of PersistPlanTool.
/// Uses the LLM (via ILLMProvider) to decompose the request into atomic tasks.
/// </summary>
public sealed class DecomposeRequestTool : IAgxiomeExecutableTool
{
    public string Name => "DecomposeRequest";
    public string Description => "Transforms a user request into an ordered list of atomic task specs for sub-agents";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "master"];

    private readonly ILLMProvider _llm;

    public DecomposeRequestTool(ILLMProvider llm) => _llm = llm;

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        var request = p.TryGetValue("request", out var r) ? r?.ToString() ?? "" : "";
        var sessionId = p.TryGetValue("sessionId", out var s) ? s?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
        var planId = p.TryGetValue("planId", out var pid) ? Guid.Parse(pid?.ToString() ?? Guid.NewGuid().ToString()) : Guid.NewGuid();
        var maxTasks = p.TryGetValue("maxTasks", out var mt) && int.TryParse(mt?.ToString(), out var mti) ? mti : 8;
        var ctx = p.TryGetValue("context", out var cx) ? cx?.ToString() : null;

        if (string.IsNullOrWhiteSpace(request))
            return Fail("Parameter 'request' is required", sw.Elapsed);

        var systemPrompt =
            "You are a task decomposition expert. Break the user's request into atomic, independently executable tasks.\n" +
            "Rules:\n" +
            "- Each task must have a single clear objective\n" +
            "- Tasks that depend on previous results get a higher stepIndex\n" +
            "- Tasks that can run in parallel share the same stepIndex (starting from 0)\n" +
            $"- Maximum tasks: {maxTasks}\n" +
            "- Respond ONLY with a valid JSON array of task objects, no markdown, no explanation.\n" +
            "Task object schema: { \"description\": string, \"stepIndex\": int, \"allowedToolNames\": string[], \"temperature\": float }";

        var userPrompt = string.IsNullOrWhiteSpace(ctx)
            ? $"Decompose this request into tasks:\n{request}"
            : $"Context:\n{ctx}\n\nDecompose this request into tasks:\n{request}";

        var llmResponse = await _llm.CompleteAsync(new LLMCompletionRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            History: null,
            Temperature: 0.2,
            MaxTokens: 2048,
            Model: "",
            Tools: null
        ), ct);

        // Parse the LLM output into TaskSpec list
        List<TaskSpec> tasks;
        try
        {
            tasks = ParseTaskSpecs(llmResponse.Content, planId);
        }
        catch (Exception ex)
        {
            return Fail($"Failed to parse LLM decomposition output: {ex.Message}", sw.Elapsed);
        }

        if (tasks.Count == 0)
            return Fail("Decomposition produced zero tasks", sw.Elapsed);

        var result = new
        {
            planId = planId.ToString(),
            sessionId,
            tasks,
            taskCount = tasks.Count,
            decompositionRationale = $"Decomposed into {tasks.Count} tasks across {tasks.Select(t => t.StepIndex).Distinct().Count()} steps"
        };

        return new ToolResult(true, result, ExecutionTime: sw.Elapsed,
            Metadata: new ToolMetadata(Name, Version, ItemsProcessed: tasks.Count));
    }

    private static List<TaskSpec> ParseTaskSpecs(string json, Guid planId)
    {
        // Strip possible markdown fences
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```")) cleaned = cleaned[(cleaned.IndexOf('\n') + 1)..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..cleaned.LastIndexOf("```")].TrimEnd();

        using var doc = System.Text.Json.JsonDocument.Parse(cleaned);
        var tasks = new List<TaskSpec>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var step = item.TryGetProperty("stepIndex", out var si) ? si.GetInt32() : 0;
            var temp = item.TryGetProperty("temperature", out var t) ? (float)t.GetDouble() : 0.7f;
            var tools = item.TryGetProperty("allowedToolNames", out var tn)
                ? tn.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                : new List<string>();

            if (string.IsNullOrWhiteSpace(desc)) continue;

            tasks.Add(new TaskSpec
            {
                TaskId = Guid.NewGuid(),
                PlanId = planId,
                StepIndex = step,
                Description = desc,
                Temperature = temp,
                AllowedToolNames = tools
            });
        }
        return tasks;
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("DecomposeRequest", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["request"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["sessionId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["planId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["maxTasks"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "integer" },
            ["context"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("request")
    };
}
