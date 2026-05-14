using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using AGXIOME.DynamicRuntime.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AgxiomaDynamicSkillLab.Engine;

/// <summary>
/// Compiles dynamically generated C# source code using Roslyn,
/// loads the assembly in an isolated AssemblyLoadContext,
/// and returns an ISimpleDynamicAgent instance.
/// Wraps AGXIOME's AgentCompiler pattern for generic tool code.
/// </summary>
public sealed class RoslynCompiler
{
    private static readonly IReadOnlyList<MetadataReference> _references = BuildReferences();

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        // On .NET 6+ the trusted platform assemblies list contains all runtime DLLs
        // (System.Runtime, System.Collections, netstandard, etc.) that Roslyn needs.
        var trustedPaths = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)
            ?? string.Empty;

        var platformRefs = trustedPaths
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();

        // Always add the AGXIOME interface assembly explicitly (not in platform list)
        var interfaceAssembly = typeof(ISimpleDynamicAgent).Assembly.Location;
        if (!string.IsNullOrEmpty(interfaceAssembly) && File.Exists(interfaceAssembly))
            platformRefs.Add(MetadataReference.CreateFromFile(interfaceAssembly));

        // Add System.Text.Json if not already covered
        var jsonAssemblyPath = typeof(JsonSerializer).Assembly.Location;
        if (!string.IsNullOrEmpty(jsonAssemblyPath) && File.Exists(jsonAssemblyPath))
            platformRefs.Add(MetadataReference.CreateFromFile(jsonAssemblyPath));

        return platformRefs;
    }

    public async Task<CompileOutput> CompileAsync(
        string sourceCode,
        string toolId,
        CancellationToken ct = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
            new CSharpParseOptions(LanguageVersion.Latest),
            cancellationToken: ct);

        var assemblyName = $"DynamicTool_{toolId}_{Guid.NewGuid():N}";

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            _references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms, cancellationToken: ct);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();
            return new CompileOutput(false, null, null, null, errors);
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Load in isolated, collectible ALC
        var alc = new IsolatedAssemblyLoadContext(toolId);
        var assembly = alc.LoadFromStream(ms);

        // Find the ISimpleDynamicAgent implementor
        var toolType = assembly.GetTypes()
            .FirstOrDefault(t => t is { IsClass: true, IsAbstract: false } &&
                                 typeof(ISimpleDynamicAgent).IsAssignableFrom(t));

        if (toolType is null)
        {
            alc.Unload();
            return new CompileOutput(false, null, null, null,
                new List<string> { "No class implementing ISimpleDynamicAgent found" });
        }

        var instance = (ISimpleDynamicAgent)Activator.CreateInstance(toolType)!;

        return new CompileOutput(true, instance, alc, assemblyName, null);
    }
}

public sealed record CompileOutput(
    bool Success,
    ISimpleDynamicAgent? Instance,
    AssemblyLoadContext? Context,
    string? AssemblyName,
    List<string>? Errors
);

/// <summary>
/// Isolated, collectible AssemblyLoadContext for dynamic tools.
/// </summary>
public sealed class IsolatedAssemblyLoadContext : AssemblyLoadContext
{
    public string ToolId { get; }

    public IsolatedAssemblyLoadContext(string toolId)
        : base(name: $"ToolCtx_{toolId}", isCollectible: true)
    {
        ToolId = toolId;
    }

    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
