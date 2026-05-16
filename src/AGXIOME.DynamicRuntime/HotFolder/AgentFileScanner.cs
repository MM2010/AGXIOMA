using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AGXIOME.DynamicRuntime.HotFolder;

/// <summary>Metadati estratti da un file .cs senza compilazione.</summary>
public sealed record AgentFileMetadata(
    string AgentId,
    string Description,
    string FilePath,
    string SourceHash
);

/// <summary>
/// Legge un file .cs con Roslyn SyntaxTree.ParseText() (parse-only, zero compilazione)
/// ed estrae i metadati dall'attributo [AgxiomeAgent("id","description")].
/// </summary>
public static class AgentFileScanner
{
    private const long MaxSourceFileSizeBytes = 256 * 1024;
    private static readonly SemaphoreSlim ScanGate = new(Math.Max(1, Environment.ProcessorCount), Math.Max(1, Environment.ProcessorCount));
    private static readonly HashSet<string> AllowedAttributeNames =
    [
        "AgxiomeAgent",
        "AgxiomeAgentAttribute",
        "AGXIOME.DynamicRuntime.Attributes.AgxiomeAgent",
        "AGXIOME.DynamicRuntime.Attributes.AgxiomeAgentAttribute"
    ];

    /// <summary>
    /// Analizza il file e ritorna i metadati dell'agente.
    /// Ritorna null se il file non contiene un attributo [AgxiomeAgent] valido.
    /// </summary>
    public static async Task<AgentFileMetadata?> ScanAsync(string filePath, string? allowedRoot = null, CancellationToken ct = default)
    {
        await ScanGate.WaitAsync(ct);
        try
        {
        if (!string.Equals(Path.GetExtension(filePath), ".cs", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!string.IsNullOrWhiteSpace(allowedRoot))
        {
            var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(allowedRoot));
            var normalizedPath = Path.GetFullPath(filePath);
            if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return null;
        }

        FileInfo info;
        try
        {
            info = new FileInfo(filePath);
            if (!info.Exists || info.Length > MaxSourceFileSizeBytes)
                return null;

            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || ContainsReparsePoint(info.Directory))
                return null;
        }
        catch (IOException)
        {
            return null;
        }

        string source;
        try
        {
            source = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        }
        catch (IOException)
        {
            return null;
        }

        var (agentId, description) = ExtractAttribute(source);
        if (agentId is null || description is null)
            return null;

        var hash = ComputeHash(source);
        return new AgentFileMetadata(agentId, description, filePath, hash);
        }
        finally
        {
            ScanGate.Release();
        }
    }

    private static (string? Id, string? Description) ExtractAttribute(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        // Cerca: [AgxiomeAgent("some-id", "some description")]
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            foreach (var attrList in classDecl.AttributeLists)
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (!AllowedAttributeNames.Contains(name))
                    continue;

                var args = attr.ArgumentList?.Arguments;
                if (args is null || args.Value.Count < 2)
                    continue;

                var id = ExtractStringLiteral(args.Value[0].Expression);
                var desc = ExtractStringLiteral(args.Value[1].Expression);
                if (id is not null && desc is not null)
                    return (id, desc);
            }
        }

        return (null, null);
    }

    private static string? ExtractStringLiteral(ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax lit &&
            lit.IsKind(SyntaxKind.StringLiteralExpression))
            return lit.Token.ValueText;
        return null;
    }

    public static string ComputeHash(string source)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexStringLower(bytes);
    }

    private static bool ContainsReparsePoint(DirectoryInfo? directory)
    {
        while (directory is not null)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            directory = directory.Parent;
        }

        return false;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
