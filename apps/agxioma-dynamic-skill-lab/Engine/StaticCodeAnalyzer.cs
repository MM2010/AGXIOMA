using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgxiomaDynamicSkillLab.Engine;

/// <summary>
/// Static analysis of generated C# code for security policy violations.
/// Inspired by AGXIOME's AgentCodeValidator but with configurable policy.
/// </summary>
public sealed class StaticCodeAnalyzer
{
    private static readonly HashSet<string> BlockedNamespacePrefixes =
    [
        "System.IO",            // File, Directory, etc.
        "System.Reflection",    // Assembly, MethodInfo — would bypass policy
        "System.Diagnostics.Process", // Process.Start
        "System.Runtime.InteropServices", // DllImport, Marshal
        "System.Security",
        "System.Net.Sockets",
        "Microsoft.Win32",
        "System.Management",
    ];

    private static readonly HashSet<string> BlockedIdentifiers =
    [
        "unsafe",
        "DllImport",
        "Marshal",
        "Process",
        "Pipe",

        //
        // "WebClient",
        // "TcpClient",
        // "Socket",
    ];

    public ValidationReport Analyze(string sourceCode)
    {
        var violations = new List<string>();
        var warnings  = new List<string>();
        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = tree.GetRoot();

        // 1. Check using directives
        foreach (var usingDir in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var ns = usingDir.Name?.ToString() ?? string.Empty;
            if (IsNamespaceBlocked(ns))
                violations.Add($"Blocked namespace: {ns}");
        }

        // 2. Check member access (e.g. System.IO.File.ReadAllText)
        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var expr = memberAccess.ToString();
            foreach (var blocked in BlockedNamespacePrefixes)
            {
                if (expr.StartsWith(blocked, StringComparison.Ordinal))
                {
                    violations.Add($"Blocked API usage: {expr}");
                    break;
                }
            }
        }

        // 3. Check unsafe statements
        if (root.DescendantNodes().OfType<UnsafeStatementSyntax>().Any())
            violations.Add("Unsafe code is not allowed");

        // 4. Check blocked type references (e.g., Process.Start)
        foreach (var ident in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (BlockedIdentifiers.Contains(ident.Identifier.Text))
                violations.Add($"Blocked identifier: {ident.Identifier.Text}");
        }

        // 5. Warnings: large method size, nested loops, etc.
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().ToList();
        foreach (var method in methods)
        {
            var lines = method.ToString().Split('\n').Length;
            if (lines > 60)
                warnings.Add($"Method '{method.Identifier.Text}' is {lines} lines (recommended < 60)");

            var loops = method.DescendantNodes()
                .Count(n => n is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax);
            if (loops > 3)
                warnings.Add($"Method '{method.Identifier.Text}' has {loops} loops (consider simplifying)");
        }

        return new ValidationReport(
            IsPass: violations.Count == 0,
            Violations: violations,
            Warnings: warnings,
            SyntaxNodeCount: root.DescendantNodes().Count()
        );
    }

    private static bool IsNamespaceBlocked(string ns) =>
        BlockedNamespacePrefixes.Any(b =>
            ns.Equals(b, StringComparison.Ordinal) ||
            ns.StartsWith(b + ".", StringComparison.Ordinal));
}

public sealed record ValidationReport(
    bool IsPass,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings,
    int SyntaxNodeCount
);
