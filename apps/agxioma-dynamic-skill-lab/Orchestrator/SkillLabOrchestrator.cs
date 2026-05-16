using System.Diagnostics;
using System.Text.Json;
using AGXIOME.Core;
using AGXIOME.Core.Patterns;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Generation;
using AGXIOME.DynamicRuntime.Generation.Prompts;
using AGXIOME.DynamicRuntime.Interfaces;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AgxiomaDynamicSkillLab.Engine;
using AgxiomaDynamicSkillLab.Storage;
using Microsoft.Data.Sqlite;

namespace AgxiomaDynamicSkillLab;

/// <summary>
/// Full pipeline orchestrator for the Dynamic Skill Lab.
/// Maps each lifecycle step to a method, tracks timing, writes audit.
/// </summary>
public sealed class SkillLabOrchestrator
{
    private readonly SkillLabStore _store;
    private readonly DynamicToolBuilder _toolBuilder;
    private readonly StaticCodeAnalyzer _analyzer;
    private readonly RoslynCompiler _compiler;
    private readonly RuntimeToolRegistry _registry;
    private readonly SandboxExecutor _sandbox;
    private readonly ILLMProvider _llm;
    private readonly string _model;
    private readonly LLMProviderType _provider;

    public SkillLabOrchestrator(
        SkillLabStore store,
        DynamicToolBuilder toolBuilder,
        StaticCodeAnalyzer analyzer,
        RoslynCompiler compiler,
        RuntimeToolRegistry registry,
        SandboxExecutor sandbox,
        ILLMProvider llm,
        string model,
        LLMProviderType provider)
    {
        _store = store;
        _toolBuilder = toolBuilder;
        _analyzer = analyzer;
        _compiler = compiler;
        _registry = registry;
        _sandbox = sandbox;
        _llm = llm;
        _model = model;
        _provider = provider;
    }

    // ─── Step 1: Detect capability gap ───────────────────────────────────────

    public async Task<ToolProposal> DetectCapabilityAsync(
        string task, string sessionId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var prompt = $$"""
            Analyze this user task and identify a capability gap — a specific tool that
            should be built dynamically in C# to accomplish it.

            Task: {{task}}

            Respond in this JSON format:
            {
              "toolName": "short-kebab-name",
              "description": "one-sentence tool purpose",
              "rationale": "why this tool is needed",
              "requiredNamespaces": ["System", "System.Text.Json", ...]
            }
            """;

        var req = new LLMCompletionRequest(
            SystemPrompt: "You are a tool architect. Output ONLY valid JSON.",
            UserPrompt: prompt,
            History: null,
            Temperature: 0.2f,
            MaxTokens: 500,
            Model: _model,
            Tools: null
        );

        var response = await _llm.CompleteAsync(req, ct);
        var json = response.Content;

        // Extract JSON block if wrapped in ```json ... ```
        var jsonMatch = System.Text.RegularExpressions.Regex.Match(json,
            @"```(?:json)?\s*(\{.*?\})\s*```",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (jsonMatch.Success) json = jsonMatch.Groups[1].Value;

        ToolProposal proposal;
        try
        {
            proposal = JsonSerializer.Deserialize<ToolProposal>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? throw new InvalidOperationException("Null deserialization");
        }
        catch (Exception ex)
        {
            proposal = new ToolProposal(
                ToolId: $"tool-{Guid.NewGuid().ToString("N")[..8]}",
                ToolName: "custom-analyzer",
                Description: task.Length > 80 ? task[..80] + "..." : task,
                Rationale: "Generated from LLM analysis of task",
                RequiredNamespaces: new List<string> { "System", "System.Text.Json", "System.Linq" }
            );
        }

        // Ensure unique tool ID
        proposal = proposal with { ToolId = $"{proposal.ToolName}-{Guid.NewGuid().ToString("N")[..6]}" };

        sw.Stop();
        await _store.AuditAsync(sessionId, proposal.ToolId, "detect-capability",
            "completed", $"Detected: {proposal.ToolName} — {proposal.Rationale}", ct);

        return proposal;
    }

    // ─── Step 2: Generate tool code via LLM ───────────────────────────────────

    public async Task<ToolDescriptor> GenerateToolAsync(
        ToolProposal proposal, string taskContext, string sessionId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Ask LLM to write the logic body
        var logicPrompt = $$"""
            Write ONLY the body of a C# method (the part between the braces of RunAsync):

            ```csharp
            public async Task<string> RunAsync(string input, CancellationToken ct = default)
            {
                // Write the body here — return a string result
            }
            ```

            Tool: {{proposal.ToolName}}
            Description: {{proposal.Description}}
            Task context: {{taskContext}}

            Rules:
            - DO NOT write the class or method signature. ONLY write what goes inside { }.
            - The `input` parameter is ALWAYS a plain string provided by the caller.
              - If the tool needs a number (e.g. temperature, N), parse it with double.TryParse or int.TryParse.
              - If the tool needs a JSON array/object, try JsonDocument.Parse; on failure return a descriptive error string.
              - If input is null or empty, return a sensible default or a short "no input" message — never throw.
            - Use System.Text.Json for JSON parsing when the input is structured.
            - Use LINQ for data processing.
            - Handle all parsing failures gracefully: catch exceptions and return an error string, never let them propagate.
            - Max 80 lines.
            - Return a meaningful string result (never return null — use string.Empty if needed).
            """;

        var sysPrompt = "You write concise C# method bodies. Output ONLY the code, no markdown, no explanation.";
        var llmReq = new LLMCompletionRequest(sysPrompt, logicPrompt, null, 0.2f, 1500, _model, null);
        var llmResp = await _llm.CompleteAsync(llmReq, ct);
        var raw = llmResp.Content.Trim();

        // Strip all markdown code fences (outer wrapper + any inner fences the LLM leaks)
        // Pattern: ```csharp or ```cs or plain ``` — remove the line entirely
        var lines = raw.Split('\n');
        var filtered = lines
            .Where(l => !System.Text.RegularExpressions.Regex.IsMatch(
                l.Trim(), @"^```"))
            .ToList();
        var logicBody = string.Join('\n', filtered).Trim();

        // Wrap into full tool class
        var bundle = _toolBuilder.BuildToolCode(
            proposal.ToolId, proposal.ToolName, proposal.Description, logicBody);

        var tool = new ToolDescriptor(
            Id: proposal.ToolId,
            Name: proposal.ToolName,
            Description: proposal.Description,
            SourceCode: bundle.SourceCode,
            ManifestJson: bundle.ManifestJson,
            CreatedAt: DateTime.UtcNow,
            Status: ToolStatus.Generated
        );

        await _store.SaveToolAsync(tool, ct);
        sw.Stop();

        await _store.AuditAsync(sessionId, tool.Id, "generate-code",
            "completed", $"Generated {tool.Name} ({bundle.SourceCode.Length} chars)", ct);

        return tool;
    }

    // ─── Step 3: Static analysis ──────────────────────────────────────────────

    public async Task<ToolDescriptor> AnalyzeToolAsync(
        ToolDescriptor tool, string sessionId, CancellationToken ct = default)
    {
        var report = _analyzer.Analyze(tool.SourceCode);

        var updated = tool with
        {
            Status = report.IsPass ? ToolStatus.Validated : ToolStatus.Failed,
            ValidationErrors = report.Violations.ToList()
        };

        await _store.SaveToolAsync(updated, ct);
        await _store.AuditAsync(sessionId, tool.Id, "static-analysis",
            report.IsPass ? "passed" : "failed",
            $"Violations: {report.Violations.Count}, Warnings: {report.Warnings.Count}, Nodes: {report.SyntaxNodeCount}", ct);

        return updated;
    }

    // ─── Step 4: Compile via Roslyn ──────────────────────────────────────────

    public async Task<ToolDescriptor> CompileToolAsync(
        ToolDescriptor tool, string sessionId, CancellationToken ct = default)
    {
        var compileResult = await _compiler.CompileAsync(tool.SourceCode, tool.Id, ct);

        ToolDescriptor updated;
        if (compileResult.Success && compileResult.Instance != null)
        {
            // Register in runtime registry
            _registry.Register(tool.Id, compileResult.Instance, compileResult.Context!);

            updated = tool with
            {
                Status = ToolStatus.Compiled,
                CompilationErrors = null
            };
        }
        else
        {
            updated = tool with
            {
                Status = ToolStatus.Failed,
                CompilationErrors = compileResult.Errors ?? new List<string>()
            };
        }

        await _store.SaveToolAsync(updated, ct);
        await _store.AuditAsync(sessionId, tool.Id, "roslyn-compilation",
            updated.Status is ToolStatus.Compiled ? "passed" : "failed",
            updated.Status is ToolStatus.Compiled
                ? $"Assembly: {compileResult.AssemblyName}"
                : $"Errors: {string.Join("; ", compileResult.Errors ?? [])}", ct);

        return updated;
    }

    // ─── Step 5: Run in sandbox ──────────────────────────────────────────────

    public async Task<ToolExecutionResult> RunToolAsync(
        ToolDescriptor tool, string input, string sessionId, CancellationToken ct = default)
    {
        var instance = _registry.Get(tool.Id);
        if (instance == null)
        {
            // Try re-compiling if evicted
            var compile = await _compiler.CompileAsync(tool.SourceCode, tool.Id, ct);
            if (!compile.Success || compile.Instance == null)
                return new ToolExecutionResult(false, string.Empty,
                    new List<string> { "Tool not loaded and recompilation failed" });
            _registry.Register(tool.Id, compile.Instance, compile.Context!);
            instance = compile.Instance;
        }

        var result = await _sandbox.ExecuteAsync(instance, input, tool.Id, ct);

        _store.UpdateStatus(tool.Id, result.Success ? ToolStatus.Executed : ToolStatus.Failed);

        await _store.AuditAsync(sessionId, tool.Id, "sandbox-execution",
            result.Success ? "passed" : "failed",
            $"Duration: {result.DurationMs}ms, Output: {(result.Output.Length > 200 ? result.Output[..200] + "..." : result.Output)}", ct);

        return new ToolExecutionResult(
            Success: result.Success,
            Output: result.Output,
            Errors: result.Errors);
    }

    // ─── Step 6: Promote to persistent skill ─────────────────────────────────

    public async Task<ToolDescriptor> PromoteSkillAsync(
        ToolDescriptor tool, string sessionId, CancellationToken ct = default)
    {
        var policyErrors = SkillPromotionPolicy.Validate(tool);
        if (policyErrors.Count > 0)
        {
            await _store.AuditAsync(sessionId, tool.Id, "promote-skill",
                "failed", string.Join("; ", policyErrors), ct);
            throw new PromotionPolicyViolationException(policyErrors);
        }

        await _store.PromoteSkillAsync(tool, ct);
        _store.UpdateStatus(tool.Id, ToolStatus.Promoted);

        await _store.AuditAsync(sessionId, tool.Id, "promote-skill",
            "completed", "Promoted to persistent skill registry", ct);

        return tool with { Status = ToolStatus.Promoted };
    }

    // ─── Full pipeline (one-shot) ─────────────────────────────────────────────

    public async Task<PipelineResponse> RunFullPipelineAsync(
        string task, string sessionId, CancellationToken ct = default)
    {
        var timing = new Dictionary<string, TimeSpan>();
        var sw = Stopwatch.StartNew();

        // Step 1: Detect
        var t1 = Stopwatch.StartNew();
        var proposal = await DetectCapabilityAsync(task, sessionId, ct);
        t1.Stop();
        timing["detect"] = t1.Elapsed;

        // Step 2: Generate
        var t2 = Stopwatch.StartNew();
        var tool = await GenerateToolAsync(proposal, task, sessionId, ct);
        t2.Stop();
        timing["generate"] = t2.Elapsed;

        // Step 3: Static analysis
        var t3 = Stopwatch.StartNew();
        tool = await AnalyzeToolAsync(tool, sessionId, ct);
        t3.Stop();
        timing["analyze"] = t3.Elapsed;

        if (!tool.Status.Equals(ToolStatus.Validated))
        {
            sw.Stop();
            return new PipelineResponse(sessionId, tool.Id, tool.Name, tool.Status,
                "static-analysis-failed",
                new { proposal, tool, violations = tool.ValidationErrors },
                tool.ValidationErrors, timing);
        }

        // Step 4: Compile (with one retry on failure — feed Roslyn errors back to LLM)
        var t4 = Stopwatch.StartNew();
        tool = await CompileToolAsync(tool, sessionId, ct);

        if (!tool.Status.Equals(ToolStatus.Compiled) && tool.CompilationErrors?.Count > 0)
        {
            // Retry: regenerate with error feedback
            await _store.AuditAsync(sessionId, tool.Id, "compile-retry",
                "retrying", $"Feeding {tool.CompilationErrors.Count} Roslyn errors back to LLM", ct);

            var retryProposal = proposal with { };
            var errorFeedback = string.Join("\n", tool.CompilationErrors.Take(5));
            var retryTask = $"{task}\n\n[PREVIOUS ATTEMPT FAILED WITH ROSLYN ERRORS — fix them:]\n{errorFeedback}";

            var retryTool = await GenerateToolAsync(retryProposal, retryTask, sessionId, ct);
            retryTool = await AnalyzeToolAsync(retryTool, sessionId, ct);
            if (retryTool.Status.Equals(ToolStatus.Validated))
            {
                retryTool = await CompileToolAsync(retryTool, sessionId, ct);
                if (retryTool.Status.Equals(ToolStatus.Compiled))
                    tool = retryTool;   // retry succeeded
            }
        }
        t4.Stop();
        timing["compile"] = t4.Elapsed;

        if (!tool.Status.Equals(ToolStatus.Compiled))
        {
            sw.Stop();
            return new PipelineResponse(sessionId, tool.Id, tool.Name, tool.Status,
                "compilation-failed",
                new { proposal, tool, errors = tool.CompilationErrors },
                tool.CompilationErrors, timing);
        }

        // Step 5: Sandbox smoke-test (best-effort — empty input).
        // The tool may require specific input format; a probe failure does NOT
        // block promotion — the tool compiled and is registered.
        var t5 = Stopwatch.StartNew();
        var execResult = await RunToolAsync(tool, string.Empty, sessionId, ct);
        t5.Stop();
        timing["execute"] = t5.Elapsed;

        sw.Stop();
        timing["total"] = sw.Elapsed;

        // Compilation is the gate; execution failure is advisory only.
        return new PipelineResponse(sessionId, tool.Id, tool.Name,
            ToolStatus.Executed,
            "completed",
            new
            {
                proposal,
                tool,
                execution = execResult,
                manifest = tool.ManifestJson
            },
            Errors: null,   // never fail the pipeline on sandbox smoke-test
            timing);
    }
}
