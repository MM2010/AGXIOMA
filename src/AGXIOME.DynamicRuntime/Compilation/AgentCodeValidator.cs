using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AGXIOME.DynamicRuntime.Compilation;

/// <summary>Risultato dell'analisi statica di sicurezza.</summary>
public sealed record ValidationResult(
    bool Ok,
    IReadOnlyList<string> Violations
);

/// <summary>
/// Analisi statica Roslyn del codice utente.
/// Blocca namespace vietati (System.IO, System.Reflection, etc.) via SyntaxTree walk.
/// Nessuna compilazione — solo visita dell'AST.
/// </summary>
public static class AgentCodeValidator
{
    private static readonly HashSet<string> BlockedNamespacePrefixes =
    [
        "System.IO",
        "System.Reflection",
        "System.Diagnostics.Process",
        "System.Runtime.InteropServices",
        "System.Security",
        "System.Net.Sockets",
        "Microsoft.Win32",
    ];

    private static readonly HashSet<string> BlockedKeywords =
    [
        "unsafe",
        "DllImport",
        "Marshal",
        "__makeref",
    ];

    public static ValidationResult Validate(string source)
    {
        var violations = new List<string>();
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();

        // 1. Controlla le direttive using
        foreach (var usingDir in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var ns = usingDir.Name?.ToString() ?? string.Empty;
            if (IsBlocked(ns))
                violations.Add($"Blocked namespace in using directive: {ns}");
        }

        // 2. Controlla member access (es. System.IO.File.ReadAllText)
        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var expr = memberAccess.ToString();
            foreach (var blocked in BlockedNamespacePrefixes)
            {
                if (expr.StartsWith(blocked, StringComparison.Ordinal))
                {
                    violations.Add($"Blocked namespace reference: {expr}");
                    break;
                }
            }
        }

        // 3. Controlla keyword bloccate nei token
        foreach (var token in root.DescendantTokens())
        {
            if (BlockedKeywords.Contains(token.ValueText))
                violations.Add($"Blocked keyword: {token.ValueText}");
        }

        // 4. Blocca unsafe context esplicito
        if (root.DescendantNodes().OfType<UnsafeStatementSyntax>().Any())
            violations.Add("Unsafe context not allowed");

        return new ValidationResult(violations.Count == 0, violations);
    }

    private static bool IsBlocked(string ns) =>
        BlockedNamespacePrefixes.Any(b =>
            ns.Equals(b, StringComparison.Ordinal) ||
            ns.StartsWith(b + ".", StringComparison.Ordinal));
}
