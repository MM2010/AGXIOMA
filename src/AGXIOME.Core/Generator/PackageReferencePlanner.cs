namespace AGXIOME.Core.Generator;

/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class PackageReferencePlanner
{
    private readonly ProjectManifest _manifest;
    private readonly HashSet<string> _efCoreModuleIds;
    private readonly string _frameworkMajorWildcard;

    public PackageReferencePlanner(ProjectManifest manifest)
    {
        _manifest = manifest;
        _efCoreModuleIds = manifest.EfCore.Contexts
            .Select(context => context.ModuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _frameworkMajorWildcard = ResolveFrameworkMajorWildcard(manifest.Language.Version);
    }

    public IReadOnlyList<PackageSpec> GetPackagesForModule(ModuleSpec module)
    {
        var packages = new Dictionary<string, PlannedPackage>(StringComparer.OrdinalIgnoreCase);
        var role = ResolveRole(module.Id);

        void Add(string id, string version, PackageSource source)
        {
            var normalized = NormalizePackage(new PackageSpec(id, version, module.Id));
            id = normalized.Id;
            version = normalized.Version;

            if (packages.TryGetValue(id, out var existing))
            {
                if (string.Equals(existing.Version, version, StringComparison.OrdinalIgnoreCase))
                    return;

                if (existing.Source == PackageSource.BuiltIn && source == PackageSource.Manifest)
                {
                    packages[id] = new PlannedPackage(version, source);
                    return;
                }

                throw new InvalidOperationException(
                    $"Package version conflict for module '{module.Id}': '{id}' has versions '{existing.Version}' and '{version}'.");
            }

            packages[id] = new PlannedPackage(version, source);
        }

        foreach (var package in GetBuiltInPackages(module, role))
            Add(package.Id, package.Version, PackageSource.BuiltIn);

        foreach (var package in _manifest.Packages.Where(package => ShouldInclude(package, module, role)))
            Add(package.Id, package.Version, PackageSource.Manifest);

        return packages
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new PackageSpec(entry.Key, entry.Value.Version, module.Id))
            .ToList();
    }

    private IEnumerable<PackageSpec> GetBuiltInPackages(ModuleSpec module, ModuleRole role)
    {
        if (role != ModuleRole.Api)
            yield return new PackageSpec("Microsoft.Extensions.DependencyInjection.Abstractions", _frameworkMajorWildcard, module.Id);

        // ILogger<T> is universally needed outside of Api (which gets it from the framework host)
        if (role is ModuleRole.Application or ModuleRole.Domain or ModuleRole.Infrastructure)
            yield return new PackageSpec("Microsoft.Extensions.Logging.Abstractions", _frameworkMajorWildcard, module.Id);
        if (role == ModuleRole.Api)
        {
            yield return new PackageSpec("Swashbuckle.AspNetCore", "7.*", module.Id);

            if (_manifest.IncludeOpenTelemetry)
            {
                yield return new PackageSpec("OpenTelemetry.Extensions.Hosting", "1.*", module.Id);
                yield return new PackageSpec("OpenTelemetry.Instrumentation.AspNetCore", "1.*", module.Id);
                yield return new PackageSpec("OpenTelemetry.Instrumentation.Http", "1.*", module.Id);
                yield return new PackageSpec("OpenTelemetry.Instrumentation.Runtime", "1.*", module.Id);
            }

            if (_manifest.Auth.Enabled)
                yield return new PackageSpec("Microsoft.AspNetCore.Authentication.JwtBearer", _frameworkMajorWildcard, module.Id);
        }

        if (_efCoreModuleIds.Contains(module.Id))
        {
            yield return new PackageSpec("Microsoft.EntityFrameworkCore", _frameworkMajorWildcard, module.Id);

            if (_manifest.Infrastructure.Any(infra => infra.Type == InfraType.PostgreSQL))
                yield return new PackageSpec("Npgsql.EntityFrameworkCore.PostgreSQL", _frameworkMajorWildcard, module.Id);

            if (_manifest.EfCore.ColumnNaming == ColumnNamingConvention.SnakeCase)
                yield return new PackageSpec("EFCore.NamingConventions", _frameworkMajorWildcard, module.Id);
        }
    }

    private bool ShouldInclude(PackageSpec package, ModuleSpec module, ModuleRole role)
    {
        package = NormalizePackage(package);

        if (IsRedundantDirectPackage(package.Id))
            return false;

        if (!string.IsNullOrWhiteSpace(package.TargetModule))
            return package.TargetModule.Equals(module.Id, StringComparison.OrdinalIgnoreCase);

        if (IsTestPackage(package.Id))
            return false;

        return role switch
        {
            ModuleRole.Api => IsApiPackage(package.Id),
            ModuleRole.Application => IsApplicationPackage(package.Id),
            ModuleRole.Infrastructure => IsInfrastructurePackage(package.Id),
            ModuleRole.Agents => IsAgentPackage(package.Id),
            ModuleRole.Toolkits => IsToolkitPackage(package.Id),
            ModuleRole.Documentation => false,
            _ => false
        };
    }

    private static bool IsApiPackage(string packageId) =>
        ContainsAny(packageId,
            "AspNetCore",
            "Swashbuckle",
            "Serilog.AspNetCore",
            "Serilog.Sinks",
            "FluentValidation.AspNetCore",
            "HealthChecks",
            "OpenTelemetry.Extensions.Hosting",
            "OpenTelemetry.Instrumentation.AspNetCore");

    private static bool IsApplicationPackage(string packageId) =>
        ContainsAny(packageId,
            "MediatR",
            "FluentValidation.DependencyInjectionExtensions");

    private static bool IsInfrastructurePackage(string packageId) =>
        ContainsAny(packageId,
            "EntityFrameworkCore",
            "Npgsql",
            "Redis",
            "Elastic",
            "Elasticsearch",
            "pgvector",
            "OpenTelemetry.Instrumentation.EntityFrameworkCore",
            "EFCore.NamingConventions");

    private static bool IsAgentPackage(string packageId) =>
        ContainsAny(packageId,
            "SemanticKernel",
            "OpenAI",
            "DeepSeek",
            "Azure.AI",
            "Ollama");

    private static bool IsToolkitPackage(string packageId) =>
        ContainsAny(packageId,
            "Polly",
            "Refit",
            "RestSharp");

    private static bool IsTestPackage(string packageId) =>
        ContainsAny(packageId,
            "xunit",
            "Moq",
            "FluentAssertions",
            "Microsoft.NET.Test.Sdk");

    private static bool IsRedundantDirectPackage(string packageId) =>
        packageId.Equals("Microsoft.Extensions.Diagnostics.HealthChecks", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("Microsoft.AspNetCore.SignalR", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string input, params string[] tokens) =>
        tokens.Any(token => input.Contains(token, StringComparison.OrdinalIgnoreCase));

    private PackageSpec NormalizePackage(PackageSpec package)
    {
        var id = package.Id;
        var version = package.Version;

        if (id.Equals("AspNetCore.HealthChecks.PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || id.Equals("AspNetCore.HealthChecks.PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            id = "AspNetCore.HealthChecks.NpgSql";
            version = NormalizeWildcardVersion(version, "9.0.0");
        }

        if (id.Equals("OpenTelemetry.Instrumentation.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            version = NormalizeWildcardVersion(version, "1.15.1-beta.1");

        if (ShouldAlignToFrameworkMajor(id))
            version = NormalizeFrameworkPackageVersion(version);

        return package with { Id = id, Version = version };
    }

    private string NormalizeFrameworkPackageVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Contains('*', StringComparison.Ordinal))
            return _frameworkMajorWildcard;

        var digits = new string(version.TakeWhile(c => char.IsDigit(c)).ToArray());
        if (!int.TryParse(digits, out var explicitMajor))
            return version;

        var frameworkMajorDigits = _frameworkMajorWildcard.TrimEnd('.', '*');
        return string.Equals(digits, frameworkMajorDigits, StringComparison.Ordinal)
            ? version
            : _frameworkMajorWildcard;
    }

    private static bool ShouldAlignToFrameworkMajor(string packageId) =>
        packageId.Equals("Microsoft.Extensions.DependencyInjection.Abstractions", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("Microsoft.AspNetCore.Authentication.JwtBearer", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite", StringComparison.OrdinalIgnoreCase)
        || packageId.Equals("EFCore.NamingConventions", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeWildcardVersion(string version, string fallbackVersion)
    {
        if (string.IsNullOrWhiteSpace(version))
            return fallbackVersion;

        return version.Contains('*', StringComparison.Ordinal) ? fallbackVersion : version;
    }

    private static string ResolveFrameworkMajorWildcard(string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return "9.*";

        var digits = new string(targetFramework
            .SkipWhile(c => !char.IsDigit(c))
            .TakeWhile(c => char.IsDigit(c))
            .ToArray());

        return int.TryParse(digits, out var major) && major > 0
            ? $"{major}.*"
            : "9.*";
    }

    private static ModuleRole ResolveRole(string moduleId)
    {
        var normalized = moduleId.Trim().ToLowerInvariant();

        if (normalized is "api" or "webapi" || normalized.EndsWith(".api"))
            return ModuleRole.Api;
        if (normalized is "application" || normalized.EndsWith(".application"))
            return ModuleRole.Application;
        if (normalized is "domain" or "core" || normalized.EndsWith(".domain") || normalized.EndsWith(".core"))
            return ModuleRole.Domain;
        if (normalized is "infrastructure" || normalized.EndsWith(".infrastructure"))
            return ModuleRole.Infrastructure;
        if (normalized is "agents" || normalized.EndsWith(".agents"))
            return ModuleRole.Agents;
        if (normalized is "toolkits" || normalized.EndsWith(".toolkits"))
            return ModuleRole.Toolkits;
        if (normalized is "documentation" || normalized.EndsWith(".documentation") || normalized is "docs")
            return ModuleRole.Documentation;

        return ModuleRole.Other;
    }

    private enum ModuleRole
    {
        Api,
        Application,
        Domain,
        Infrastructure,
        Agents,
        Toolkits,
        Documentation,
        Other
    }

    private enum PackageSource
    {
        BuiltIn,
        Manifest
    }

    private readonly record struct PlannedPackage(string Version, PackageSource Source);
}