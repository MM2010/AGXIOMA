using System.Diagnostics;
using System.Text.RegularExpressions;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Generation.Prompts;
using AGXIOME.DynamicRuntime.Registry;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.DynamicRuntime.Generation;

/// <summary>
/// Pipeline: descrizione naturale → LLM → C# → validazione → compilazione → registrazione.
/// Retry automatico (max 3 tentativi) con feedback errori Roslyn all'LLM.
/// </summary>
public sealed class AgentCodeGenerator
{
    private readonly ILLMProvider _llm;
    private readonly AgentCompiler _compiler;
    private readonly DynamicAgentRegistry _registry;
    private readonly string _basePath;
    private const int MaxRetries = 3;

    // Estrae il blocco ```csharp ... ```
    private static readonly Regex CodeBlockRegex =
        new(@"```csharp\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.Compiled);

    public AgentCodeGenerator(
        ILLMProvider llm,
        AgentCompiler compiler,
        DynamicAgentRegistry registry,
        string? basePath = null)
    {
        _llm = llm;
        _compiler = compiler;
        _registry = registry;
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agxioma");
    }

    public async Task<AgentGenerationResult> GenerateAsync(
        AgentGenerationRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var systemPrompt = AgentGenerationSystemPrompt.Build(request.AllowedNamespaces);

        var userPrompt = BuildUserPrompt(request);
        var errors = new List<string>();
        string? lastCode = null;

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            // Append errori precedenti al prompt se siamo in retry
            var fullUserPrompt = attempt == 0
                ? userPrompt
                : $"{userPrompt}\n\nPrevious attempt had these compiler/validation errors:\n{string.Join("\n", errors)}\nPlease fix them.";

            var llmRequest = new LLMCompletionRequest(
                SystemPrompt: systemPrompt,
                UserPrompt: fullUserPrompt,
                History: null,
                Temperature: 0.2,
                MaxTokens: 2000,
                Model: string.Empty,
                Tools: null
            );

            LLMWIKYResponse response;
            try
            {
                response = await _llm.CompleteAsync(llmRequest, ct);
            }
            catch (Exception ex)
            {
                return new AgentGenerationResult(
                    AgentId: string.Empty, GeneratedCode: lastCode ?? string.Empty,
                    Success: false, Errors: [$"LLM call failed: {ex.Message}"],
                    TotalTime: sw.Elapsed);
            }

            // Estrai blocco csharp
            var match = CodeBlockRegex.Match(response.Content);
            if (!match.Success)
            {
                errors = [$"LLM did not return a ```csharp code block. Raw: {response.Content[..Math.Min(200, response.Content.Length)]}"];
                continue;
            }

            var code = match.Groups[1].Value.Trim();
            lastCode = code;

            // Validazione statica
            var validation = AgentCodeValidator.Validate(code);
            if (!validation.Ok)
            {
                errors = [.. validation.Violations];
                continue;
            }

            // Compilazione
            var agentId = ExtractAgentId(code) ?? $"generated-agent-{Guid.NewGuid():N[..8]}";
            var srcPath = Path.Combine(_basePath, "agents", request.UserId, "src");
            var binPath = Path.Combine(_basePath, "agents", request.UserId, "bin");
            Directory.CreateDirectory(srcPath);

            var compileResult = await _compiler.CompileAsync(code, agentId, binPath, ct);
            if (!compileResult.Success)
            {
                errors = [.. compileResult.Errors];
                continue;
            }

            // Salva .cs su disco (hot-folder lo rileverà)
            var filePath = Path.Combine(srcPath, $"{agentId}.cs");
            await File.WriteAllTextAsync(filePath, code, ct);

            // Registra nel catalogo
            var entry = new DiscoveredAgentEntry(
                AgentId: agentId,
                Description: request.Description,
                FilePath: filePath,
                SourceHash: HotFolder.AgentFileScanner.ComputeHash(code),
                Status: AgentEntryStatus.Compiled,
                DiscoveredAt: DateTime.UtcNow
            );
            _registry.Register(request.UserId, entry);

            return new AgentGenerationResult(
                AgentId: agentId, GeneratedCode: code,
                Success: true, Errors: [],
                TotalTime: sw.Elapsed, SavedFilePath: filePath);
        }

        return new AgentGenerationResult(
            AgentId: string.Empty, GeneratedCode: lastCode ?? string.Empty,
            Success: false, Errors: errors, TotalTime: sw.Elapsed);
    }

    private static string BuildUserPrompt(AgentGenerationRequest req)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Create an agent that: {req.Description}");
        if (req.ExampleInput is not null)
            sb.AppendLine($"Example input: {req.ExampleInput}");
        if (req.ExampleOutput is not null)
            sb.AppendLine($"Example output: {req.ExampleOutput}");
        return sb.ToString();
    }

    private static readonly Regex AgentIdRegex =
        new(@"\[AgxiomeAgent\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

    private static string? ExtractAgentId(string code)
    {
        var m = AgentIdRegex.Match(code);
        return m.Success ? m.Groups[1].Value : null;
    }
}
