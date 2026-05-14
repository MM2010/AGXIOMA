using System.Security.Cryptography;
using System.Text;
using AGXIOME.DynamicRuntime.Isolation;
using AGXIOME.Shared.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AGXIOME.DynamicRuntime.Compilation;

/// <summary>Assembly compilato pronto all'uso.</summary>
public sealed class CompiledAgent
{
    public required string AgentId { get; init; }
    public required string SourceHash { get; init; }
    public required AgentAssemblyContext Context { get; init; }
    public required Interfaces.ISimpleDynamicAgent Instance { get; init; }
}

/// <summary>Risultato della compilazione.</summary>
public sealed record CompileResult(
    bool Success,
    CompiledAgent? Agent,
    IReadOnlyList<string> Errors
);

/// <summary>
/// Compila sorgente C# in assembly isolato tramite Roslyn CSharpCompilation.
/// Scrive cache .dll + .sha256 in binPath per evitare ricompilazioni al riavvio.
/// </summary>
public sealed class AgentCompiler
{
    private static readonly IReadOnlyList<MetadataReference> AllowedReferences = BuildAllowedReferences();

    private static IReadOnlyList<MetadataReference> BuildAllowedReferences()
    {
        // Allowlist: solo assembly esplicitamente consentiti
        var refs = new List<MetadataReference>();
        var allowedAssemblies = new[]
        {
            typeof(object).Assembly,                          // System.Runtime
            typeof(System.Collections.Generic.List<>).Assembly,
            typeof(System.Text.Json.JsonDocument).Assembly,
            typeof(System.Net.Http.HttpClient).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(Interfaces.ISimpleDynamicAgent).Assembly, // AGXIOME.DynamicRuntime (includes AgentServices)
            typeof(ILLMProvider).Assembly, // AGXIOME.Shared
        };

        foreach (var asm in allowedAssemblies)
        {
            if (!string.IsNullOrEmpty(asm.Location))
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
        }

        return refs;
    }

    public async Task<CompileResult> CompileAsync(
        string source,
        string agentId,
        string? binPath = null,
        CancellationToken ct = default)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var assemblyName = $"DynamicAgent_{agentId}_{Guid.NewGuid():N}";

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            AllowedReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
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
            return new CompileResult(false, null, errors);
        }

        ms.Seek(0, SeekOrigin.Begin);
        var hash = ComputeHash(source);

        // Carica in contesto isolato
        var context = new AgentAssemblyContext(agentId);
        var assembly = context.LoadFromStream(ms);

        // Trova il tipo che implementa ISimpleDynamicAgent
        var agentType = assembly.GetTypes()
            .FirstOrDefault(t => t.IsClass && !t.IsAbstract &&
                                 typeof(Interfaces.ISimpleDynamicAgent).IsAssignableFrom(t));

        if (agentType is null)
        {
            context.Unload();
            return new CompileResult(false, null, ["No class implementing ISimpleDynamicAgent found"]);
        }

        var instance = (Interfaces.ISimpleDynamicAgent)Activator.CreateInstance(agentType)!;

        // Cache su disco
        if (binPath is not null)
        {
            Directory.CreateDirectory(binPath);
            ms.Seek(0, SeekOrigin.Begin);
            var dllPath = Path.Combine(binPath, $"{agentId}.dll");
            var hashPath = Path.Combine(binPath, $"{agentId}.sha256");
            await File.WriteAllBytesAsync(dllPath, ms.ToArray(), ct);
            await File.WriteAllTextAsync(hashPath, hash, ct);
        }

        return new CompileResult(true,
            new CompiledAgent { AgentId = agentId, SourceHash = hash, Context = context, Instance = instance },
            []);
    }

    private static string ComputeHash(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexStringLower(bytes);
    }
}
