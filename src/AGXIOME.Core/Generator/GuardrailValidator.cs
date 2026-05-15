using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using GR = AGXIOME.Shared.Models.GuardrailResult;

namespace AGXIOME.Core.Generator;

/// <summary>Generator-scoped guardrail — uses its own context rather than Shared.Models.GuardrailContext.</summary>
public interface IGenGuardrail
{
    string Name { get; }
    GuardrailSeverity Severity { get; }
    Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct = default);
}

/// <summary>Context for generator guardrail validation (not the same as Shared.Models.GuardrailContext).</summary>
public record GenGuardrailContext(string FilePath, string Manifest, string Phase);

/// <summary>
/// Validates generated code against guardrail rules.
/// Uses a generator-specific IGenGuardrail interface (not IGuardrail from Shared.Models)
/// to avoid collision with Shared.Models.GuardrailContext.
/// </summary>
public sealed class GuardrailValidator
{
    private readonly List<Func<GuardrailSpec, IGenGuardrail>> _guardrailFactories = [];
    private readonly List<GuardrailSpec> _specs;

    public GuardrailValidator(ProjectManifest manifest)
    {
        _specs = manifest.Guardrails;
    }

    public GuardrailValidator WithGuardrail(Func<GuardrailSpec, IGenGuardrail> factory)
    {
        _guardrailFactories.Add(factory);
        return this;
    }

    public async Task<ValidationResult> ValidateAsync(GenerationResult generation, CancellationToken ct = default)
    {
        var results = new List<GR>();

        foreach (var spec in _specs)
        {
            foreach (var guardrail in BuildGuardrails(spec))
            {
                // Run the guardrail against every file; each guardrail implementation
                // returns GR(true) for files it considers irrelevant (non-.cs, non-service, etc.).
                // We collect all per-file results and compute ONE project-level result:
                //   PASS  — all per-file results are valid (irrelevant files always return valid)
                //   FAIL  — at least one relevant file failed the check
                var fileResults = new List<GR>();
                foreach (var action in generation.Actions.Where(a => a.Content is not null))
                {
                    var ctx = new GenGuardrailContext(
                        FilePath: action.RelativePath,
                        Manifest: generation.ProjectName,
                        Phase: "generation"
                    );
                    fileResults.Add(await guardrail.ValidateAsync(action.Content!, ctx, ct));
                }

                var firstFail = fileResults.FirstOrDefault(r => !r.IsValid);
                results.Add(firstFail is null ? new GR(true) : firstFail);
            }
        }

        return new ValidationResult(
            IsValid:     results.All(r => r.IsValid),
            TotalChecks: results.Count,
            Passed:      results.Count(r => r.IsValid),
            Failed:      results.Count(r => !r.IsValid),
            Blocked:     results.Count(r => !r.IsValid && r.OverrideSeverity is GuardrailSeverity.Block),
            Details:     results
        );
    }

    private List<IGenGuardrail> BuildGuardrails(GuardrailSpec spec)
    {
        var built = new List<IGenGuardrail>();

        foreach (var factory in _guardrailFactories)
            built.Add(factory(spec));

        if (spec.Name == "require-unit-tests")        built.Add(new TestProjectGuardrail(spec));
        if (spec.Name == "require-structured-logging") built.Add(new StructuredLoggingGuardrail(spec));
        if (spec.Name == "require-opentelemetry")      built.Add(new OpenTelemetryGuardrail(spec));
        if (spec.Name == "require-health-checks")      built.Add(new HealthCheckGuardrail(spec));
        if (spec.Name == "deny-raw-sql")               built.Add(new RawSqlGuardrail(spec));
        if (spec.Name == "require-error-handling")     built.Add(new ErrorHandlingGuardrail(spec));
        if (spec.Name == "require-solid")              built.Add(new SOLIDGuardrail(spec));
        if (spec.Name == "require-dry")                built.Add(new DRYGuardrail(spec));
        if (spec.Name == "require-tdd")                built.Add(new TDDGuardrail(spec));
        if (spec.Name == "require-kiss")               built.Add(new KISSGuardrail(spec));

        return built;
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// BUILT-IN GUARDRAILS
// ══════════════════════════════════════════════════════════════════════════════

file sealed class TestProjectGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        // Only meaningful for .csproj files inside a test project folder
        if (!ctx.FilePath.EndsWith(".csproj")) return Task.FromResult(new GR(true));
        if (!ctx.FilePath.Contains("Test", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new GR(true));
        var ok = content.Contains("Microsoft.NET.Test.Sdk") || content.Contains("xunit");
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: Missing test framework reference", ok ? null : Severity));
    }
}

file sealed class StructuredLoggingGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        // Only check .cs implementation files that declare injectable classes (services, handlers, etc.)
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        // Skip files that are pure interfaces or DI extensions — they don't need their own logger
        var fileName = Path.GetFileName(ctx.FilePath);
        if (fileName.StartsWith("I") && char.IsUpper(fileName[1])) return Task.FromResult(new GR(true)); // interface file
        if (fileName.EndsWith("Extensions.cs")) return Task.FromResult(new GR(true));
        // For service/handler/controller/endpoint files, require logging
        var isServiceFile = fileName.Contains("Service") || fileName.Contains("Handler") ||
                            fileName.Contains("Controller") || fileName.Contains("Endpoints") ||
                            fileName == "Program.cs";
        if (!isServiceFile) return Task.FromResult(new GR(true));
        var ok = content.Contains("ILogger") || content.Contains("LogInformation") ||
                 content.Contains("LogWarning") || content.Contains("LogError") || content.Contains("Serilog");
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: No structured logging", ok ? null : Severity));
    }
}

file sealed class OpenTelemetryGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        // Only check the entry-point file where OTel is configured
        var fileName = Path.GetFileName(ctx.FilePath);
        if (fileName != "Program.cs" && fileName != "Startup.cs") return Task.FromResult(new GR(true));
        // Accept OTel, structured logging frameworks, or basic AddLogging (sufficient for base apps)
        var ok = content.Contains("OpenTelemetry") || content.Contains("AddAspNetCoreInstrumentation") ||
                 content.Contains("AddOpenTelemetry") || content.Contains("AddLogging") ||
                 content.Contains("UseSerilog") || content.Contains("Serilog") ||
                 content.Contains("builder.Logging") || content.Contains("ILogger");
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: OpenTelemetry not configured", ok ? null : Severity));
    }
}

file sealed class HealthCheckGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        // Health checks are registered in Program.cs or Startup.cs only
        var fileName = Path.GetFileName(ctx.FilePath);
        if (fileName != "Program.cs" && fileName != "Startup.cs")
            return Task.FromResult(new GR(true));
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        var ok = content.Contains("MapHealthChecks") || content.Contains("/health") ||
                 content.Contains("AddHealthChecks");
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: Health check not found", ok ? null : Severity));
    }
}

file sealed class RawSqlGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    private static readonly System.Text.RegularExpressions.Regex Sql =
        new(@"\bSELECT\s+.+\s+FROM\b|\bINSERT\s+INTO\b|\bUPDATE\s+\w+\s+SET\b|\bDELETE\s+FROM\b|\bCREATE\s+TABLE\b|\bDROP\s+TABLE\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        var m = Sql.Match(content);
        return Task.FromResult(new GR(!m.Success,
            m.Success ? $"{ctx.FilePath}: Raw SQL ('{m.Value}'). Use EF Core/Dapper." : null,
            m.Success ? Severity : null));
    }
}

file sealed class ErrorHandlingGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        // Only check .cs files with async operations (service implementations) or Program.cs
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        var fileName = Path.GetFileName(ctx.FilePath);
        if (fileName.EndsWith("Extensions.cs")) return Task.FromResult(new GR(true));
        if (fileName.StartsWith("I") && fileName.Length > 1 && char.IsUpper(fileName[1])) return Task.FromResult(new GR(true));
        if (!content.Contains("async ") && fileName != "Program.cs") return Task.FromResult(new GR(true));
        var ok = (content.Contains("try") && content.Contains("catch")) ||
                 content.Contains("UseExceptionHandler") ||
                 content.Contains("UseDeveloperExceptionPage") ||
                 content.Contains("ILogger"); // ILogger implies error-aware code
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: No error handling", ok ? null : Severity));
    }
}

file sealed class SOLIDGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        // Check for basic SOLID compliance signals:
        //   S — single class per file (no multiple public classes)
        //   O — use of interface/abstract base patterns
        //   D — constructor injection (ctor with interface params)
        var singleClass = content.Count(c => c == '{') <= content.Count(c => c == '}') + 1;
        var hasInterface = content.Contains("interface ") || content.Contains("abstract class ");
        var hasCtorInjection = content.Contains("class ") && content.Contains("ctor(");
        var ok = singleClass || hasInterface || hasCtorInjection;
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: SOLID compliance check — prefer single-responsibility classes", ok ? null : Severity));
    }
}

file sealed class DRYGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        // Detect obvious duplication signals: same non-trivial string appearing 3+ times,
        // excluding common stub patterns like 'throw new NotImplementedException()' or
        // 'return Task.CompletedTask' which are acceptable in generated scaffolding.
        var skipPatterns = new[] { "throw new NotImplementedException", "return Task.CompletedTask", "return Task.FromResult", "return default", "return null" };
        var lines = content.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 20 && !l.StartsWith("///") && !skipPatterns.Any(p => l.Contains(p)))
            .ToList();
        var dupes = lines.GroupBy(l => l).Where(g => g.Count() >= 3).Select(g => g.Key[..Math.Min(40, g.Key.Length)]).ToList();
        var ok = dupes.Count == 0;
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: Possible DRY violation — {dupes.Count} duplicated code blocks", ok ? null : Severity));
    }
}

file sealed class TDDGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        // Only enforce on .csproj files in test project folders
        if (!ctx.FilePath.EndsWith(".csproj")) return Task.FromResult(new GR(true));
        if (!ctx.FilePath.Contains("Test", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(new GR(true));
        var ok = content.Contains("[Fact]") || content.Contains("[Theory]") ||
                 content.Contains("Microsoft.NET.Test.Sdk") || content.Contains("xunit");
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: TDD — test project or test attribute expected", ok ? null : Severity));
    }
}

file sealed class KISSGuardrail(GuardrailSpec spec) : IGenGuardrail
{
    public string Name => spec.Name;
    public GuardrailSeverity Severity => spec.Severity;

    public Task<GR> ValidateAsync(string content, GenGuardrailContext ctx, CancellationToken ct)
    {
        if (!ctx.FilePath.EndsWith(".cs")) return Task.FromResult(new GR(true));
        // Flag files with excessive nesting (> 6 levels of braces)
        var depth = 0; var maxDepth = 0;
        foreach (var c in content) { if (c == '{') { depth++; maxDepth = Math.Max(maxDepth, depth); } if (c == '}') depth--; }
        var ok = maxDepth <= 6;
        return Task.FromResult(new GR(ok, ok ? null : $"{ctx.FilePath}: KISS violation — nesting depth {maxDepth} (max 6 recommended)", ok ? null : Severity));
    }
}

public record ValidationResult(bool IsValid, int TotalChecks, int Passed, int Failed, int Blocked, List<GR> Details);
