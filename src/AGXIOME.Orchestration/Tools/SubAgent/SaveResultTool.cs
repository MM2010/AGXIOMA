using System.Text;
using AGXIOME.Orchestration.Abstractions;
using AGXIOME.Orchestration.Infrastructure;
using AGXIOME.Orchestration.Models;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Orchestration.Tools.SubAgent;

/// <summary>
/// Sub-agent tool — step 1 of 2.
/// Saves the task output to hot store and orchestration store.
/// Does NOT notify — notification is the responsibility of NotifyCompletionTool.
/// Single responsibility: persist result only.
///
/// P15: supports isHitlPaused=true to signal a HITL pause without completing the task.
/// P20: optionally computes ResultHmac via IAgxiomeCrypto when the service is registered.
/// </summary>
public sealed class SaveResultTool : IAgxiomeExecutableTool
{
    public string Name => "SaveResult";
    public string Description => "Saves the sub-agent task output to hot store and orchestration store";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories { get; } = ["orchestration", "subagent"];

    private readonly IHotStore _hot;
    private readonly IOrchestrationStore _cold;
    // P20: optional — null means crypto is not wired (dev/test scenario).
    private readonly IAgxiomeCrypto? _crypto;
    // Stable HMAC key per deployment. In production, load from secrets provider, not hardcoded.
    private static readonly byte[] HmacKey = Encoding.UTF8.GetBytes("agxiome-result-hmac-key-v1");

    public SaveResultTool(IHotStore hot, IOrchestrationStore cold, IAgxiomeCrypto? crypto = null)
    {
        _hot = hot;
        _cold = cold;
        _crypto = crypto;
    }

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? new Dictionary<string, object>();

        if (!p.TryGetValue("taskId", out var tidObj) || !Guid.TryParse(tidObj?.ToString(), out var taskId))
            return Fail("Parameter 'taskId' (Guid) is required", sw.Elapsed);

        if (!p.TryGetValue("planId", out var pidObj) || !Guid.TryParse(pidObj?.ToString(), out var planId))
            return Fail("Parameter 'planId' (Guid) is required", sw.Elapsed);

        var output = p.TryGetValue("output", out var outObj) ? outObj?.ToString() ?? "" : "";
        var summary = p.TryGetValue("summary", out var sumObj) ? sumObj?.ToString() : null;
        var success = !p.TryGetValue("success", out var succObj) || succObj is not bool sb || sb;
        var tokensUsed = p.TryGetValue("tokensUsed", out var tok) && int.TryParse(tok?.ToString(), out var ti) ? (int?)ti : null;
        var errorMessage = p.TryGetValue("errorMessage", out var errObj) ? errObj?.ToString() : null;
        // P15: sub-agent signals a HITL pause rather than a real completion
        var isHitlPaused = p.TryGetValue("isHitlPaused", out var hitlObj) && hitlObj is bool hb && hb;
        var hitlRequirementId = p.TryGetValue("hitlRequirementId", out var hrObj) ? hrObj?.ToString() : null;

        // Capture elapsed time before async operations — reflects actual sub-agent execution time
        var elapsed = p.TryGetValue("elapsedMs", out var elObj) && long.TryParse(elObj?.ToString(), out var eli)
            ? TimeSpan.FromMilliseconds(eli)
            : sw.Elapsed;

        var data = p.TryGetValue("data", out var dataObj) && dataObj is IReadOnlyDictionary<string, object> d ? d : null;

        // P20: compute HMAC over canonical payload bytes for tamper-evidence
        var payloadBytes = Encoding.UTF8.GetBytes($"{taskId}:{planId}:{output}:{success}");
        var resultHmac = _crypto is not null ? _crypto.ComputeHMAC(payloadBytes, HmacKey) : string.Empty;

        var result = new TaskResult
        {
            TaskId = taskId,
            PlanId = planId,
            Success = success,
            Output = output,
            Summary = summary,
            Data = data,
            ErrorMessage = errorMessage,
            CompletedAt = DateTimeOffset.UtcNow,
            ElapsedTime = elapsed,
            TokensUsed = tokensUsed,
            IsHitlPaused = isHitlPaused,
            HitlRequirementId = hitlRequirementId,
            ResultHmac = resultHmac
        };

        // Persist to hot store first (fast path)
        await _hot.SetAsync(HotKeys.TaskResult(taskId), OrchJson.Serialize(result), TimeSpan.FromHours(48), ct);

        // Persist to canonical store (audit)
        await _cold.SaveResultAsync(result, ct);

        return new ToolResult(true,
            new { saved = true, taskId = taskId.ToString(), planId = planId.ToString(), isHitlPaused },
            ExecutionTime: sw.Elapsed,
            Metadata: new ToolMetadata(Name, Version));
    }

    private static ToolResult Fail(string msg, TimeSpan elapsed) =>
        new(false, null, msg, elapsed, new ToolMetadata("SaveResult", "1.0"));

    public System.Text.Json.Nodes.JsonObject ToJsonSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new System.Text.Json.Nodes.JsonObject
        {
            ["taskId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["planId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["output"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["summary"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["success"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "boolean" },
            ["tokensUsed"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "integer" },
            ["errorMessage"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" },
            ["isHitlPaused"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "boolean" },
            ["hitlRequirementId"] = new System.Text.Json.Nodes.JsonObject { ["type"] = "string" }
        },
        ["required"] = new System.Text.Json.Nodes.JsonArray("taskId", "planId", "output")
    };
}
