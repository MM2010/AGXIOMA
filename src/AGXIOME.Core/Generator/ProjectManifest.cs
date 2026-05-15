using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Generator;

/// <summary>
/// Declarative specification of a software project.
/// Drives the DifferentialGenerator to produce consistent, governance-compliant code.
/// </summary>
public sealed class ProjectManifest
{
    public string ProjectName { get; }
    public string OutputPath { get; private set; } = "./";
    public LanguageSpec Language { get; private set; } = new("csharp", "net10.0");
    public ArchitectureStyle Architecture { get; private set; } = ArchitectureStyle.ModularMonolith;
    public FileGenerationStrategy FileStrategy { get; private set; } = FileGenerationStrategy.PerModule;
    public AuthConfig Auth { get; private set; } = new();
    public DockerConfig Docker { get; private set; } = new();
    public EfCoreConfig EfCore { get; private set; } = new();
    public ColumnNamingConvention ColumnNaming { get; private set; } = ColumnNamingConvention.SnakeCase;
    public List<ModuleSpec> Modules { get; } = [];
    public List<InfraSpec> Infrastructure { get; } = [];
    public List<GuardrailSpec> Guardrails { get; } = [];
    public List<PackageSpec> Packages { get; } = [];
    public List<AgentSpec> Agents { get; } = [];
    public List<ParadigmSpec> Paradigms { get; } = [];
    public bool IncludeTests { get; private set; } = true;
    public bool IncludeOpenTelemetry { get; private set; } = true;
    public bool IncludeHealthChecks { get; private set; } = true;

    public ProjectManifest(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name is required.", nameof(projectName));
        ProjectName = projectName;
    }

    // ── Core fluent builders ─────────────────────────────────────────────────

    public ProjectManifest WithOutputPath(string path) { OutputPath = path; return this; }
    public ProjectManifest WithLanguage(string lang, string version) { Language = new(lang, version); return this; }
    public ProjectManifest WithArchitecture(ArchitectureStyle style) { Architecture = style; return this; }
    public ProjectManifest FileStructure(FileGenerationStrategy s) { FileStrategy = s; return this; }
    public ProjectManifest ColumnNames(ColumnNamingConvention c) { ColumnNaming = c; return this; }

    // ── Auth ─────────────────────────────────────────────────────────────────

    public ProjectManifest WithAuth(Action<AuthConfig> configure)
    {
        configure(Auth);
        return this;
    }

    /// <summary>Quick-set: enable JWT auth with default config.</summary>
    public ProjectManifest UseJwtAuth(string? issuer = null, string? audience = null)
    {
        Auth.Enabled = true;
        Auth.Scheme = AuthScheme.Jwt;
        if (issuer != null) Auth.Issuer = issuer;
        if (audience != null) Auth.Audience = audience;
        return this;
    }

    // ── Docker ───────────────────────────────────────────────────────────────

    public ProjectManifest WithDocker(Action<DockerConfig> configure)
    {
        configure(Docker);
        return this;
    }

    // ── EF Core ──────────────────────────────────────────────────────────────

    public ProjectManifest WithEfCore(Action<EfCoreConfig> configure)
    {
        configure(EfCore);
        return this;
    }

    /// <summary>Add an EF Core DbContext to a specific module.</summary>
    public ProjectManifest AddDbContext(string contextName, string moduleId,
        string? connectionStringName = null, bool useSeparateConfig = true)
    {
        EfCore.Contexts.Add(new DbContextSpec(contextName, moduleId, connectionStringName, useSeparateConfig));
        return this;
    }

    // ── Modules, infra, packages ──────────────────────────────────────────────

    public ProjectManifest AddParadigm(ParadigmSpec spec) { Paradigms.Add(spec); return this; }

    public ProjectManifest AddParadigms(Action<ParadigmConfig> configure)
    {
        var c = new ParadigmConfig(); configure(c);
        foreach (var s in c.Build()) Paradigms.Add(s);
        return this;
    }

    public ProjectManifest AddModule(string id, string @namespace, string description)
    {
        Modules.Add(new(id, @namespace, description));
        return this;
    }

    public bool HasModule(string id) => Modules.Any(m =>
        m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public ProjectManifest AddPostgres(string name, string? conn = null)
    { Infrastructure.Add(new InfraSpec(name, InfraType.PostgreSQL, conn)); return this; }

    public ProjectManifest AddRedis(string name, string? conn = null)
    { Infrastructure.Add(new InfraSpec(name, InfraType.Redis, conn)); return this; }

    public ProjectManifest AddElastic(string name, string? conn = null)
    { Infrastructure.Add(new InfraSpec(name, InfraType.Elasticsearch, conn)); return this; }

    public ProjectManifest AddKafka(string name, string? conn = null)
    { Infrastructure.Add(new InfraSpec(name, InfraType.Kafka, conn)); return this; }

    public ProjectManifest AddGuardrail(GuardrailSpec spec)
    {
        // Deduplicate by name — never add the same guardrail twice
        if (!Guardrails.Any(g => g.Name == spec.Name))
            Guardrails.Add(spec);
        return this;
    }

    public ProjectManifest AddPackage(string id, string ver, string? targetModule = null)
    { Packages.Add(new(id, ver, targetModule)); return this; }

    public ProjectManifest AddAgent(string id, string tree, string prompt, string[] keywords)
    { Agents.Add(new(id, tree, prompt, keywords)); return this; }

    public ProjectManifest WithTests() { IncludeTests = true; return this; }
    public ProjectManifest WithOpenTelemetry() { IncludeOpenTelemetry = true; return this; }
    public ProjectManifest WithHealthChecks() { IncludeHealthChecks = true; return this; }
    public ProjectManifest WithoutTests() { IncludeTests = false; return this; }
    public ProjectManifest WithoutOpenTelemetry() { IncludeOpenTelemetry = false; return this; }
    public ProjectManifest WithoutHealthChecks() { IncludeHealthChecks = false; return this; }
}

// ══════════════════════════════════════════════════════════════════════════════
// RECORDS — infra, modules, packages, agents, guardrails
// ══════════════════════════════════════════════════════════════════════════════

public record LanguageSpec(string Name, string Version);
public record ModuleSpec(string Id, string Namespace, string Description);
public record PackageSpec(string Id, string Version, string? TargetModule = null);
public record InfraSpec(string Name, InfraType Type, string? ConnectionString = null);
public record AgentSpec(string Id, string TreeName, string SystemPrompt, string[] Keywords);

public enum InfraType
{
    PostgreSQL, Redis, Elasticsearch, Kafka, RabbitMQ, BlobStorage, CosmosDB
}

public enum ArchitectureStyle
{
    Monolith, ModularMonolith, CleanArchitecture, VerticalSlices, Microservices
}

public enum FileGenerationStrategy
{
    PerModule, SingleProject, PerFeature, Hexagonal
}

public enum ColumnNamingConvention
{
    SnakeCase, CamelCase, PascalCase
}

// ══════════════════════════════════════════════════════════════════════════════
// AUTH CONFIG
// ══════════════════════════════════════════════════════════════════════════════

public enum AuthScheme { None, Jwt, Jwe }

public sealed class AuthConfig
{
    public bool Enabled { get; set; }
    public AuthScheme Scheme { get; set; } = AuthScheme.Jwt;
    public string Issuer { get; set; } = "agxiome";
    public string Audience { get; set; } = "agxiome-api";
    public string? SigningKeyEnvVar { get; set; } = "JWT_SIGNING_KEY";
    public bool RequireHttps { get; set; } = true;
    public int TokenExpiryMinutes { get; set; } = 60;
    public bool EnableRefreshTokens { get; set; }
    public List<string> Policies { get; set; } = [];
    public AuthConfig AddPolicy(string name) { Policies.Add(name); return this; }
}

// ══════════════════════════════════════════════════════════════════════════════
// DOCKER CONFIG
// ══════════════════════════════════════════════════════════════════════════════

public sealed class DockerConfig
{
    public bool Enabled { get; set; } = true;
    public bool ExposePorts { get; set; } = true;
    public bool HealthCheck { get; set; } = true;
    public string? Registry { get; set; }
    public string BaseImageSdk { get; set; } = "mcr.microsoft.com/dotnet/sdk:10.0";
    public string BaseImageRuntime { get; set; } = "mcr.microsoft.com/dotnet/aspnet:10.0";
    public bool MultiStageBuild { get; set; } = true;
    public bool IncludeComposeProfiles { get; set; } = true;
    public List<string> ComposeProfiles { get; set; } = ["dev", "prod"];

    public DockerConfig WithRegistry(string r) { Registry = r; return this; }
    public DockerConfig WithProfiles(params string[] profiles)
    { ComposeProfiles = [.. profiles]; return this; }
}

// ══════════════════════════════════════════════════════════════════════════════
// EF CORE CONFIG — multi-context, entity config separation, naming
// ══════════════════════════════════════════════════════════════════════════════

public sealed class EfCoreConfig
{
    public bool Enabled { get; set; }
    public List<DbContextSpec> Contexts { get; set; } = [];
    public bool UseSeparateEntityTypeConfigurations { get; set; } = true;
    public ColumnNamingConvention ColumnNaming { get; set; } = ColumnNamingConvention.SnakeCase;
    public bool UseSnakeCase { get; set; } = true;       // shortcut
    public bool UseCamelCase { get; set; }
    public bool UsePascalCase { get; set; }
    public bool EnableSensitiveLogging { get; set; }
    public bool EnableAutoMigrations { get; set; }
    public bool EnableRetryOnFailure { get; set; } = true;

    /// <summary>Helper: set column naming and update UseXxx flags.</summary>
    public EfCoreConfig WithColumnNaming(ColumnNamingConvention c)
    {
        ColumnNaming = c;
        UseSnakeCase = c == ColumnNamingConvention.SnakeCase;
        UseCamelCase = c == ColumnNamingConvention.CamelCase;
        UsePascalCase = c == ColumnNamingConvention.PascalCase;
        return this;
    }
}

public record DbContextSpec(
    string ContextName,
    string ModuleId,
    string? ConnectionStringName = null,
    bool UseSeparateEntityTypeConfigurations = true
);

// ══════════════════════════════════════════════════════════════════════════════
// GUARDRAIL SPEC
// ══════════════════════════════════════════════════════════════════════════════

public class GuardrailSpec
{
    public string Name { get; }
    public GuardrailSeverity Severity { get; set; } = GuardrailSeverity.Block;
    public GuardrailSpec(string name) => Name = name;

    public static GuardrailSpec RequireUnitTests() => new("require-unit-tests");
    public static GuardrailSpec RequireStructuredLogging() => new("require-structured-logging");
    public static GuardrailSpec RequireOpenTelemetry() => new("require-opentelemetry");
    public static GuardrailSpec RequireDependencyInjection() => new("require-di");
    public static GuardrailSpec DenyRawSql() => new("deny-raw-sql") { Severity = GuardrailSeverity.Block };
    public static GuardrailSpec RequireHealthChecks() => new("require-health-checks");
    public static GuardrailSpec RequireErrorHandling() => new("require-error-handling");
    public static GuardrailSpec RequireSOLID() => new("require-solid");
    public static GuardrailSpec RequireDRY() => new("require-dry");
    public static GuardrailSpec RequireKISS() => new("require-kiss");
    public static GuardrailSpec RequireTDD() => new("require-tdd");
}

// ══════════════════════════════════════════════════════════════════════════════
// PARADIGMS
// ══════════════════════════════════════════════════════════════════════════════

public record ParadigmSpec(string Name, string Description);

public static class Paradigms
{
    public static ParadigmSpec SOLID     => new("solid", "Single-responsibility, Open-closed, Liskov, Interface segregation, Dependency inversion");
    public static ParadigmSpec DRY       => new("dry",   "Don't Repeat Yourself");
    public static ParadigmSpec KISS      => new("kiss",  "Keep It Simple, Stupid");
    public static ParadigmSpec TDD       => new("tdd",   "Test-Driven Development");
    public static ParadigmSpec YAGNI     => new("yagni", "You Ain't Gonna Need It");
    public static ParadigmSpec CleanCode => new("clean-code", "Meaningful names, small functions");
    public static ParadigmSpec DDD       => new("ddd",   "Domain-Driven Design");
}

public sealed class ParadigmConfig
{
    private readonly List<ParadigmSpec> _list = [];
    public ParadigmConfig Add(ParadigmSpec s) { _list.Add(s); return this; }
    public ParadigmConfig AddSOLID() => Add(Paradigms.SOLID);
    public ParadigmConfig AddDRY()   => Add(Paradigms.DRY);
    public ParadigmConfig AddKISS()  => Add(Paradigms.KISS);
    public ParadigmConfig AddTDD()   => Add(Paradigms.TDD);
    public ParadigmConfig AddYAGNI() => Add(Paradigms.YAGNI);
    public ParadigmConfig AddCleanCode() => Add(Paradigms.CleanCode);
    public ParadigmConfig AddDDD()   => Add(Paradigms.DDD);
    internal List<ParadigmSpec> Build() => [.. _list];
}
