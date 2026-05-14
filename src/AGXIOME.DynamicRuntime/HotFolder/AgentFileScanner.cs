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
    /// <summary>
    /// Analizza il file e ritorna i metadati dell'agente.
    /// Ritorna null se il file non contiene un attributo [AgxiomeAgent] valido.
    /// </summary>
    public static async Task<AgentFileMetadata?> ScanAsync(string filePath, CancellationToken ct = default)
    {
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

    private static (string? Id, string? Description) ExtractAttribute(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        // Cerca: [AgxiomeAgent("some-id", "some description")]
        foreach (var attrList in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var name = attrList.Name.ToString();
            if (!name.Equals("AgxiomeAgent", StringComparison.Ordinal) &&
                !name.Equals("AgxiomeAgentAttribute", StringComparison.Ordinal))
                continue;

            var args = attrList.ArgumentList?.Arguments;
            if (args is null || args.Value.Count < 2)
                continue;

            var id = ExtractStringLiteral(args.Value[0].Expression);
            var desc = ExtractStringLiteral(args.Value[1].Expression);
            if (id is not null && desc is not null)
                return (id, desc);
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
}
