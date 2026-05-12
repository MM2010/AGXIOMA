using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.Master;

/// <summary>
/// Master tool — step 3 of 6.
/// Determines the optimal sub-agent configuration for each task in a step.
/// Uses the LLM to select tool allowlists, system prompts, and LLM parameters per task type.
/// Does not spawn agents — SpawnAgentTool does that.
/// </summary>
public sealed class GenerateAgentsTool : IAgxiomeExecutableTool
{
    public string Name => "GenerateAgents";
    public string Description => "Determines optimal sub-agent configurations (tools, temperature, system prompt) for each task";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "master"];

    private readonly ILLMProvider _llm;

    public GenerateAgentsTool(ILLMProvider llm) => _llm = llm;

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("tasks", out var tasksObj) || tasksObj is not IReadOnlyList<TaskSpec> tasks)
            return Fail("Parameter 'tasks' of type IReadOnlyList<TaskSpec> is required", sw.Elapsed);

        var availableTools = p.TryGetValue("availableTools", out var at) && at is IReadOnlyList<string> tl
            ? tl
            : Array.Empty<string>();

        var llmProviders = p.TryGetValue("llmProviders", out var lp) && lp is IReadOnlyList<string> pl
            ? pl
            : Array.Empty<string>();

        var systemPrompt =
            """
            You are an agent configuration expert. For each task, choose the optimal sub-agent configuration.
            Respond ONLY with a valid JSON array. Each element must match:
            {
              "taskId": "guid-string",
              "agentRole": "string",
              "allowedToolNames": ["tool1"],
              "preferredLLMProvider": "string or null",
              "temperature": float,
              "systemPrompt": "string"
            }
            Temperature guidelines: factual/deterministic → 0.1-0.3, analytical → 0.4-0.6, creative → 0.7-0.9.
            Only include tools from the available list provided.
            """;

        var tasksJson = System.Text.Json.JsonSerializer.Serialize(tasks.Select(t => new
        {
            taskId = t.TaskId.ToString(),
            description = t.Description,
            stepIndex = t.StepIndex,
            suggestedTools = t.AllowedToolNames
        }));

        var userPrompt = $"Available tools: [{string.Join(", ", availableTools)}]\n" +
                         $"Available LLM providers: [{string.Join(", ", llmProviders)}]\n\n" +
                         $"Configure sub-agents for these tasks:\n{tasksJson}";

        var llmResponse = await _llm.CompleteAsync(new LLMCompletionRequest(
            SystemPrompt: systemPrompt,
            UserPrompt: userPrompt,
            History: null,
            Temperature: 0.2,
            MaxTokens: 2048,
            Model: "",
            Tools: null
        ), ct);

        List<AgentConfiguration> configs;
        try
        {
            configs = ParseConfigurations(llmResponse.Content, tasks, availableTools);
        }
        catch (Exception ex)
        {
            return Fail($"Failed to parse agent configurations: {ex.Message}", sw.Elapsed);
        }

        return new ToolResult(true,
            new { agentConfigurations = configs },
            ExecutionTime: sw.Elapsed,
            Metadata: new ToolMetadata(Name, Version, ItemsProcessed: configs.Count));
    }

    private static List<AgentConfiguration> ParseConfigurations(
        string json, IReadOnlyList<TaskSpec> tasks, IReadOnlyList<string> availableTools)
    {
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```")) cleaned = cleaned[(cleaned.IndexOf('\n') + 1)..];
        if (cleaned.EndsWith("```")) cleaned = cleaned[..cleaned.LastIndexOf("```")].TrimEnd();

        var availableSet = new HashSet<string>(availableTools, StringComparer.OrdinalIgnoreCase);
        var configs = new List<AgentConfiguration>();

        using var doc = System.Text.Json.JsonDocument.Parse(cleaned);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var taskId = item.TryGetProperty("taskId", out var tid)
                ? Guid.Parse(tid.GetString() ?? Guid.NewGuid().ToString())
                : Guid.NewGuid();

            // Validate tool allowlist — only include tools from available set
            var rawTools = item.TryGetProperty("allowedToolNames", out var tn)
                ? tn.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
                : new List<string>();
            var filteredTools = rawTools.Where(t => availableSet.Count == 0 || availableSet.Contains(t)).ToList();

            configs.Add(new AgentConfiguration(
                TaskId: taskId,
                AgentRole: item.TryGetProperty("agentRole", out var ar) ? ar.GetString() ?? "Assistant" : "Assistant",
                AllowedToolNames: filteredTools,
                PreferredLLMProvider: item.TryGetProperty("preferredLLMProvider", out var plp) ? plp.GetString() : null,
                Temperature: item.TryGetProperty("temperature", out var t) ? (float)t.GetDouble() : 0.7f,
                SystemPrompt: item.TryGetProperty("systemPrompt", out var sp) ? sp.GetString() : null
            ));
        }

        // Ensure every task has a configuration — fill gaps with defaults
        var configuredIds = configs.Select(c => c.TaskId).ToHashSet();
        foreach (var task in tasks.Where(t => !configuredIds.Contains(t.TaskId)))
        {
            configs.Add(new AgentConfiguration(
                TaskId: task.TaskId,
                AgentRole: "Assistant",
                AllowedToolNames: new List<string>(task.AllowedToolNames),
                PreferredLLMProvider: null,
                Temperature: task.Temperature,
                SystemPrompt: null
            ));
        }

        return configs;
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("GenerateAgents", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["tasks"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" },
            ["availableTools"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" },
            ["llmProviders"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "array" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("tasks")
    };
}

/// <summary>Sub-agent configuration produced by GenerateAgentsTool.</summary>
public sealed record AgentConfiguration(
    Guid TaskId,
    string AgentRole,
    IReadOnlyList<string> AllowedToolNames,
    string? PreferredLLMProvider,
    float Temperature,
    string? SystemPrompt
);
