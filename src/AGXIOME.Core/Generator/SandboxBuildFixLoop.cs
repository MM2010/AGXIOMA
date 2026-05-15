using System.Diagnostics;
using AGXIOME.Core;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Generator;

/// <summary>
/// Layer 3: Sandbox Build-Fix Loop.
/// Compiles generated code, reads errors, sends to repair agent, re-applies fixes.
/// Repeats until build passes or max attempts reached.
/// </summary>
public sealed class SandboxBuildFixLoop
{
    private readonly IAgxiomeAgent _repairAgent;
    private readonly string _projectRoot;
    private readonly string? _cooperativePlanPath;
    private readonly int _maxAttempts;
    private readonly List<BuildAttempt> _attempts = [];

    public SandboxBuildFixLoop(
        IAgxiomeAgent repairAgent,
        string projectRoot,
        string? cooperativePlanPath,
        int maxAttempts = 5)
    {
        _repairAgent = repairAgent;
        _projectRoot = projectRoot;
        _cooperativePlanPath = cooperativePlanPath;
        _maxAttempts = maxAttempts;
    }

    /// <summary>Run the build-fix loop. Returns final build result.</summary>
    public async Task<SandboxResult> ExecuteAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var attempt = 0;

        while (attempt < _maxAttempts)
        {
            attempt++;
            var buildResult = await RunDotNetBuildAsync(ct);
            _attempts.Add(buildResult);

            if (buildResult.Success)
            {
                sw.Stop();
                return new SandboxResult(
                    Success:   true,
                    Attempts:  attempt,
                    Duration:  sw.Elapsed,
                    AttemptsLog: _attempts,
                    Message:   $"Build succeeded after {attempt} attempt(s)."
                );
            }

            // Build failed — ask repair agent to fix
            try
            {
                var fixPlan = await GenerateFixesAsync(buildResult, ct);
                await ApplyFixesAsync(fixPlan, ct);
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new SandboxResult(
                    Success: false,
                    Attempts: attempt,
                    Duration: sw.Elapsed,
                    AttemptsLog: _attempts,
                    Message: $"Build failed and repair agent is unavailable: {ex.Message.Split('\n')[0]}"
                );
            }
        }

        sw.Stop();
        return new SandboxResult(
            Success:   false,
            Attempts:  _maxAttempts,
            Duration:  sw.Elapsed,
            AttemptsLog: _attempts,
            Message:   $"Build failed after {_maxAttempts} attempts."
        );
    }

    /// <summary>Runs dotnet build and parses compiler errors.</summary>
    private async Task<BuildAttempt> RunDotNetBuildAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var errors = new List<CompilerError>();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build --verbosity minimal",
                    WorkingDirectory = _projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(ct);

            var allOutput = output.ToString() + error.ToString();

            // Parse compiler errors
            foreach (var line in allOutput.Split('\n'))
            {
                // error CSXXXX: ...
                var match = System.Text.RegularExpressions.Regex.Match(line.Trim(),
                    @"^(?<file>.+?)\((?<line>\d+)(?:,\d+)?\)\s*:\s*error\s+(?<code>CS\d+)\s*:\s*(?<msg>.+)$");
                if (match.Success)
                {
                    errors.Add(new CompilerError(
                        File:     match.Groups["file"].Value,
                        Line:     int.Parse(match.Groups["line"].Value),
                        Code:     match.Groups["code"].Value,
                        Message:  match.Groups["msg"].Value
                    ));
                }
            }

            sw.Stop();

            return new BuildAttempt(
                AttemptNumber: _attempts.Count + 1,
                Success:       process.ExitCode == 0,
                Errors:        errors,
                Output:        allOutput,
                Duration:      sw.Elapsed
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new BuildAttempt(
                AttemptNumber: _attempts.Count + 1,
                Success:       false,
                Errors:        [new CompilerError("build", 0, "EXCEPTION", ex.Message)],
                Output:        ex.ToString(),
                Duration:      sw.Elapsed
            );
        }
    }

    /// <summary>Ask LLM to generate fixes for compiler errors.</summary>
    private async Task<FixPlan> GenerateFixesAsync(BuildAttempt build, CancellationToken ct)
    {
        var errorSummary = string.Join("\n",
            build.Errors.Select(e => $"- {e.File}({e.Line}): {e.Code}: {e.Message}"));

        var cooperativePlan = !string.IsNullOrWhiteSpace(_cooperativePlanPath) && File.Exists(_cooperativePlanPath)
            ? await File.ReadAllTextAsync(_cooperativePlanPath, ct)
            : null;

        var cooperativePlanSection = string.IsNullOrWhiteSpace(cooperativePlan)
            ? string.Empty
            : $"""

COOPERATIVE PLAN MARKDOWN:
{cooperativePlan}

PLAN-AWARE REPAIR RULES:
- Prefer fixing files that appear in Planned Steps or Generated Artifacts.
- If an error is in a file not mentioned by the cooperative plan, change it only when required by the listed compiler errors.
- Do not invent replacement architecture outside the planned files.
""";

        var prompt = $"""
You are a C# build repair specialist. Fix the following build errors precisely.

ERRORS:
{errorSummary}
{cooperativePlanSection}

RULES:
- Fix ONLY the listed errors. Do not rewrite unrelated code.
- If a type is missing, declare it as a simple record or class in the SAME file — do not create new files for it.
- If a `using` directive references a namespace that does not exist, REMOVE it.
- If a package is not in the .csproj but is used in a `using`, either remove the `using` or add the package to the .csproj.
- NEVER remove `using Microsoft.Extensions.Logging;` — ILogger<T> is always available.
- NEVER remove `using Microsoft.Extensions.DependencyInjection;` — it is always available.
- Do not use FluentValidation, Mapster, MediatR, or any mapper library unless it was already present.
- For .csproj fixes, output a complete valid XML file.
- Use file-scoped namespaces in .cs files.
- NAMESPACE FIXES: If CS0234 reports "'Domain' not in 'X'" → rename `X.Domain` usings to `X.Core` (the Core module serves as domain).
- NAMESPACE FIXES: If CS0234 reports "'Common' not in 'X.Application'" → remove the `using X.Application.Common` line (it doesn't exist).
- WRONG ROOT NAMESPACE: If the file uses a namespace root different from what the project uses, fix the entire file's namespace declaration to use the correct root (derive it from the adjacent .cs files or the .csproj assembly name).
- CRITICAL: Do NOT rewrite Program.cs from scratch. Only change the minimum lines needed to fix the listed error.
- CRITICAL: Do NOT rename DbContext classes or change their type name. If a type name causes CS0246 in Program.cs, remove that specific usage (comment it out or delete the line) rather than inventing new type names.
- CRITICAL: Do NOT add DbContext, EF Core, or UseInMemoryDatabase to Program.cs unless it was already there.
- CRITICAL: Do NOT use placeholder namespaces like 'YourNamespace', 'MyNamespace', or 'AppNamespace'. Use the actual namespace from adjacent files.
- Return fixes using exactly this format (one block per file):

FIX: path/to/file.cs
```csharp
// full corrected content
```

FIX: path/to/project.csproj
```xml
<!-- full corrected content -->
```
""";

        var response = await _repairAgent.RunAsync(
            new AgentRequest(prompt), ct);

        if (response.Status != AgentStatus.Completed)
            throw new InvalidOperationException(response.Content);

        return new FixPlan(build.Errors, response.Content);
    }

    /// <summary>Apply fixes from the repair agent to disk.</summary>
    private async Task ApplyFixesAsync(FixPlan plan, CancellationToken ct)
    {
        // Parse FIX blocks from agent response — supports both ```csharp and ```xml
        var fixBlocks = System.Text.RegularExpressions.Regex.Matches(plan.RepairOutput,
            @"FIX:\s*(.+?)\n```(?:csharp|xml|cs)?(?::\S*)?\n([\s\S]*?)```",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in fixBlocks)
        {
            var filePath = match.Groups[1].Value.Trim();
            var code = match.Groups[2].Value.Trim();

            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(code))
                continue;

            if (filePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || filePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                || filePath.EndsWith("Directory.Build.props", StringComparison.OrdinalIgnoreCase))
                continue;

            var fullPath = Path.Combine(_projectRoot, filePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
            var existingCode = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, ct) : null;

            if (ShouldSkipFix(filePath, code, existingCode))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, code, ct);
        }
    }

    private static bool ShouldSkipFix(string filePath, string code, string? existingCode)
    {
        if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return false;

        if (Path.GetFileName(filePath).Equals("Program.cs", StringComparison.OrdinalIgnoreCase))
            return true;

        if (LooksInstructional(code))
            return true;

        return false;
    }

    private static bool LooksInstructional(string code)
    {
        return code.Contains("the rest of the file remains", StringComparison.OrdinalIgnoreCase)
            || code.Contains("keep all existing", StringComparison.OrdinalIgnoreCase)
            || code.Contains("add the missing using directive", StringComparison.OrdinalIgnoreCase)
            || code.Contains("unchanged", StringComparison.OrdinalIgnoreCase) && !code.Contains("namespace", StringComparison.OrdinalIgnoreCase);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RESULT TYPES
// ══════════════════════════════════════════════════════════════════════════════

public record CompilerError(string File, int Line, string Code, string Message);

public record BuildAttempt(
    int AttemptNumber,
    bool Success,
    List<CompilerError> Errors,
    string Output,
    TimeSpan Duration
);

public record FixPlan(List<CompilerError> Errors, string RepairOutput);

public record SandboxResult(
    bool Success,
    int Attempts,
    TimeSpan Duration,
    List<BuildAttempt> AttemptsLog,
    string Message
);
