using System.Diagnostics;
using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Generator;

/// <summary>
/// Layer 2: Agentic Code Writer.
/// Uses an LLM agent to generate real C# code based on a manifest step.
/// Not static scaffolding — real implementation code.
/// </summary>
public sealed class AgenticCodeWriter
{
    private readonly IAgxiomeAgent _codeAgent;
    private readonly ProjectManifest _manifest;

    public AgenticCodeWriter(IAgxiomeAgent codeAgent, ProjectManifest manifest)
    {
        _codeAgent = codeAgent;
        _manifest = manifest;
    }

    /// <summary>Generate a code artifact for a specific generation step.</summary>
    public async Task<CodeArtifact> GenerateArtifactAsync(
        GenStep step,
        ModuleContract? contract = null,
        CooperativeExecutionContext? executionContext = null,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(step, contract, executionContext);
        var sw = Stopwatch.StartNew();

        var response = await _codeAgent.RunAsync(
            new AgentRequest(prompt),
            ct);

        if (response.Status != AgentStatus.Completed)
            throw new InvalidOperationException(response.Content);

        sw.Stop();

        var (code, filePath) = ExtractCodeAndPath(response.Content, step);

        return new CodeArtifact(
            StepId:      step.Id,
            FilePath:    filePath,
            Code:        code,
            Language:    "csharp",
            RawResponse: response.Content,
            Duration:    sw.Elapsed
        );
    }

    private string BuildPrompt(GenStep step, ModuleContract? contract, CooperativeExecutionContext? executionContext)
    {
        var contractSection = contract is null ? string.Empty : $"""

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⛔ NAMESPACE CONSTRAINT — ABSOLUTE MANDATORY ⛔
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
The ONLY valid root namespace for THIS file is: {contract.ModuleNamespace}

  ✅ CORRECT:   namespace {contract.ModuleNamespace};
  ✅ CORRECT:   namespace {contract.ModuleNamespace}.Services;
  ✅ CORRECT:   namespace {contract.ModuleNamespace}.Interfaces;
  ✅ CORRECT:   namespace {contract.ModuleNamespace}.Entities;
  ❌ WRONG:     namespace {step.TargetModule}; (do NOT use just the module name — use the FULL namespace above)
  ❌ WRONG:     namespace {step.TargetModule}.Services; (WRONG — must start with {contract.ModuleNamespace})
  ❌ WRONG:     any namespace not starting with exactly "{contract.ModuleNamespace}"

The project root is "{_manifest.ProjectName}" — do NOT shorten, expand, or translate this name.
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

MODULE CONTRACT — STRICT: do not use any type, namespace, or package outside this contract.
- Root namespace for this module: {contract.ModuleNamespace}
- Target framework: {contract.TargetFramework}
- Packages available in this module's .csproj (ONLY these may appear in `using` for NuGet types):
{string.Join("\n", contract.AvailablePackages.Select(p => $"  • {p}"))}
- Namespaces from directly referenced modules (may be used via `using`):
{(contract.ReferencedModuleNamespaces.Count > 0 ? string.Join("\n", contract.ReferencedModuleNamespaces.Select(ns => $"  • {ns}")) : "  (none — declare all needed types in THIS file)")}
- ALL module namespaces in this project (DO NOT invent namespaces outside this list):
{string.Join("\n", contract.AllModuleNamespaces.Select(ns => $"  • {ns}"))}
{(contract.EfContextNames.Count > 0 ? $"- EF Core DbContext(s) available: {string.Join(", ", contract.EfContextNames)}" : string.Empty)}

STRICT RULES:
1. ONLY add `using` directives for the packages listed above or for namespaces from referenced modules.
2. NEVER invent namespaces (e.g., .Common, .Features, .Models, .Requests, .Responses, .Domain) unless listed.
3. Declare ALL types you need directly in this file — do not assume other files exist.
4. Do NOT reference packages not listed (e.g., no FluentValidation, Mapster, MediatR unless listed above).
5. The file-scoped namespace MUST start with exactly "{contract.ModuleNamespace}" — no exceptions.
""";

    var collaborationSection = executionContext is null
        ? string.Empty
        : $"""

TEAM EXECUTION CONTEXT:
- Specialist owner for this step: {step.Owner}
- Planner goal for this step: {step.PlanGoal}
- Collaboration notes: {step.CollaborationNotes}
- Steps already completed by the team:
{(executionContext.CompletedSteps.Count > 0 ? string.Join("\n", executionContext.CompletedSteps.Select(s => $"  • {s}")) : "  (none yet)")}
- Relevant outputs already produced:
{(executionContext.CompletedArtifacts.Count > 0 ? string.Join("\n", executionContext.CompletedArtifacts.Select(a => $"  • {a}")) : "  (none yet)")}

COOPERATION RULES:
1. Respect outputs produced by previous specialists — extend them coherently, do not contradict them.
2. Generate code that composes with the deterministic scaffold already present in the project.
3. If this step depends on data flow from another layer, preserve those contracts explicitly in the generated code.
4. Prefer completing the user flow end-to-end over adding optional abstractions.
""";

        return $"""
You are an expert C# .NET developer. Generate production-quality code for the following specification.

PROJECT: {_manifest.ProjectName}
LANGUAGE: {_manifest.Language.Name} {_manifest.Language.Version}
ARCHITECTURE: {_manifest.Architecture}
MODULE: {step.TargetModule}
WHAT TO GENERATE: {step.Description}

CONTEXT:
- File to write: {step.TargetPath}
- Paradigms: {string.Join(", ", _manifest.Paradigms.Select(p => p.Name))}
- EF Core: {(_manifest.EfCore.Enabled ? "Yes, snake_case naming, IEntityTypeConfiguration per entity" : "No")}
- Auth: {(_manifest.Auth.Enabled ? $"{_manifest.Auth.Scheme} with {string.Join(",", _manifest.Auth.Policies)} policies" : "None")}
{contractSection}
{collaborationSection}
REQUIREMENTS:
1. Generate ONLY the code for {step.TargetPath}
2. Use file-scoped namespaces
3. Use primary constructors where appropriate
4. Use async/await with CancellationToken
5. No static mutable state
6. Follow SOLID principles
7. Add XML documentation on public APIs
8. Declare ALL types needed in this file — no assumptions about types defined elsewhere

OUTPUT FORMAT:
Return the code wrapped in a code block with the file path as the language tag:
```csharp:{step.TargetPath}
// code here
```

Do NOT include any explanation outside the code block.
""";
    }

    private static (string code, string filePath) ExtractCodeAndPath(string response, GenStep step)
    {
        // Try to extract from markdown code block with file path
        var lines = response.Split('\n');
        var codeLines = new List<string>();
        var inBlock = false;
        var extractedPath = step.TargetPath;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("```"))
            {
                // csharp:src/Api/SomeFile.cs
                if (!inBlock && line.Contains(":"))
                {
                    var pathStart = line.IndexOf(':') + 1;
                    if (pathStart < line.Length)
                        extractedPath = line[pathStart..].Trim();
                }
                inBlock = !inBlock;
                continue;
            }
            if (inBlock)
                codeLines.Add(lines[i]);
        }

        var code = codeLines.Count > 0
            ? string.Join("\n", codeLines)
            : response; // fallback: use raw response

        return (code, extractedPath);
    }
}

/// <summary>A single generation step — what to generate, where, and why.</summary>
public record GenStep(
    string Id,
    string TargetModule,
    string TargetPath,
    string Description,
    GenStepKind Kind = GenStepKind.Code,
    AgentSpecialization Owner = AgentSpecialization.General,
    string PlanGoal = "Implement the requested slice end-to-end.",
    string CollaborationNotes = "Keep contracts aligned with adjacent layers."
);

public enum GenStepKind { Code, Config, Test, Documentation }

public enum AgentSpecialization
{
    Planner,
    Api,
    Backend,
    Frontend,
    Infrastructure,
    Agents,
    General
}

/// <summary>
/// Describes what types, namespaces, and packages are legally available to the agentic code writer
/// when generating code for a specific module. Prevents the LLM from inventing references.
/// </summary>
/// <param name="ModuleId">Module identifier (e.g. "Api", "Domain").</param>
/// <param name="ModuleNamespace">Root C# namespace for this module (e.g. "MyProject.Api").</param>
/// <param name="TargetFramework">Target framework moniker (e.g. "net10.0").</param>
/// <param name="AvailablePackages">NuGet package IDs listed in this module's .csproj.</param>
/// <param name="ReferencedModuleNamespaces">Namespaces of directly project-referenced modules.</param>
/// <param name="EfContextNames">EF Core DbContext names available in this module.</param>
/// <param name="AllModuleNamespaces">Every module namespace in the project — LLM must not invent others.</param>
public record ModuleContract(
    string ModuleId,
    string ModuleNamespace,
    string TargetFramework,
    IReadOnlyList<string> AvailablePackages,
    IReadOnlyList<string> ReferencedModuleNamespaces,
    IReadOnlyList<string> EfContextNames,
    IReadOnlyList<string> AllModuleNamespaces
);

/// <summary>Code artifact produced by the agent.</summary>
public record CodeArtifact(
    string StepId,
    string FilePath,
    string Code,
    string Language,
    string RawResponse,
    TimeSpan Duration
);

public record CooperativeExecutionContext(
    IReadOnlyList<string> CompletedSteps,
    IReadOnlyList<string> CompletedArtifacts
);
