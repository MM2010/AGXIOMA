using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.DynamicRuntime.Skills;

/// <summary>
/// Adatta una SkillDefinition a IAgxiomeExecutableTool in base al tipo:
/// - HttpApi → thin HTTP wrapper (nessuna compilazione)
/// - CSharpFunction → compilazione Roslyn del SourceCode embedded
/// - Prompt → agente LLM puro (not yet supported, returns NotSupportedException)
/// </summary>
public sealed class SkillAdapter
{
    private readonly AgentCompiler _compiler;
    private readonly HttpClient _http;

    public SkillAdapter(AgentCompiler compiler, HttpClient? http = null)
    {
        _compiler = compiler;
        _http = http ?? new HttpClient();
    }

    public async Task<IAgxiomeExecutableTool> AdaptAsync(SkillDefinition skill, CancellationToken ct = default)
    {
        return skill.Type switch
        {
            SkillType.HttpApi => new HttpApiSkillTool(skill, _http),
            SkillType.CSharpFunction => await AdaptCSharpAsync(skill, ct),
            SkillType.Prompt => throw new NotSupportedException($"Prompt-type skill '{skill.Id}' is not yet supported"),
            _ => throw new ArgumentOutOfRangeException(nameof(skill.Type), skill.Type, null)
        };
    }

    private async Task<IAgxiomeExecutableTool> AdaptCSharpAsync(SkillDefinition skill, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(skill.SourceCode))
            throw new InvalidOperationException($"Skill '{skill.Id}' is CSharpFunction but has no SourceCode");

        var result = await _compiler.CompileAsync(skill.SourceCode, skill.Id, binPath: null, ct);
        if (!result.Success)
            throw new InvalidOperationException($"Failed to compile skill '{skill.Id}': {string.Join("; ", result.Errors)}");

        return new DynamicAgentSkillTool(skill, result.Agent!.Instance);
    }
}

// ── HTTP API wrapper ──────────────────────────────────────────────────────────

file sealed class HttpApiSkillTool : IAgxiomeExecutableTool
{
    private readonly SkillDefinition _def;
    private readonly HttpClient _http;

    public HttpApiSkillTool(SkillDefinition def, HttpClient http) { _def = def; _http = http; }

    public string Name => _def.Id;
    public string Description => _def.Description;
    public string? Version => null;
    public IReadOnlyList<string> Categories => ["skill", "http"];

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        if (_def.Endpoint is null)
            return new ToolResult(false, null, "No endpoint configured");

        var payload = new { input = context.Parameters?.ToString() ?? string.Empty };
        var response = await _http.PostAsJsonAsync(_def.Endpoint, payload, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        return new ToolResult(response.IsSuccessStatusCode, content,
            response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
    }

    public JsonObject ToJsonSchema() =>
        new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["input"] = new JsonObject { ["type"] = "string" } }
        };
}

// ── Compiled C# skill wrapper ─────────────────────────────────────────────────

file sealed class DynamicAgentSkillTool : IAgxiomeExecutableTool
{
    private readonly SkillDefinition _def;
    private readonly Interfaces.ISimpleDynamicAgent _agent;

    public DynamicAgentSkillTool(SkillDefinition def, Interfaces.ISimpleDynamicAgent agent)
    { _def = def; _agent = agent; }

    public string Name => _def.Id;
    public string Description => _def.Description;
    public string? Version => null;
    public IReadOnlyList<string> Categories => ["skill", "csharp"];

    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var input = context.Parameters?.ToString() ?? string.Empty;
        var output = await _agent.RunAsync(input, ct);
        return new ToolResult(true, output);
    }

    public JsonObject ToJsonSchema() =>
        new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["input"] = new JsonObject { ["type"] = "string" } }
        };
}
