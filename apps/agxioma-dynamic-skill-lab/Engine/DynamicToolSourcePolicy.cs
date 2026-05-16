using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgxiomaDynamicSkillLab.Engine;

public static class DynamicToolSourcePolicy
{
    private static readonly string[] ForbiddenNamespacePrefixes =
    [
        "System.Diagnostics",
        "System.IO",
        "System.Net",
        "System.Reflection",
        "System.Runtime.InteropServices",
        "System.Security",
        "Microsoft.Win32"
    ];

    private static readonly string[] ForbiddenTypeNames =
    [
        "Activator",
        "AppDomain",
        "Assembly",
        "Directory",
        "DirectoryInfo",
        "DriveInfo",
        "Environment",
        "File",
        "FileInfo",
        "Marshal",
        "Process",
        "Type"
    ];

    public static IReadOnlyList<string> Validate(string sourceCode, string expectedToolId, int maxSourceChars = 32_768)
    {
        var errors = new List<string>();
        var effectiveMaxSourceChars = Math.Max(1024, maxSourceChars);

        if (sourceCode.Length > effectiveMaxSourceChars)
        {
            errors.Add($"Dynamic source exceeds {effectiveMaxSourceChars} characters");
            return errors;
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, new CSharpParseOptions(LanguageVersion.Latest));
        errors.AddRange(syntaxTree.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString()));

        var root = syntaxTree.GetRoot();

        foreach (var usingDirective in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var importedNamespace = usingDirective.Name?.ToString();
            if (importedNamespace is not null && ForbiddenNamespacePrefixes.Any(prefix =>
                    importedNamespace.StartsWith(prefix, StringComparison.Ordinal)))
            {
                errors.Add($"Forbidden namespace import: {importedNamespace}");
            }
        }

        if (!root.DescendantNodes().OfType<BaseTypeSyntax>().Any(baseType =>
                string.Equals(baseType.Type.ToString(), "ISimpleDynamicAgent", StringComparison.Ordinal) ||
                string.Equals(baseType.Type.ToString(), "AGXIOME.DynamicRuntime.Interfaces.ISimpleDynamicAgent", StringComparison.Ordinal)))
        {
            errors.Add("Source must implement ISimpleDynamicAgent.");
        }

        if (root.DescendantNodes().OfType<AttributeSyntax>().Any(attribute =>
                string.Equals(attribute.Name.ToString(), "DllImport", StringComparison.Ordinal) ||
                string.Equals(attribute.Name.ToString(), "DllImportAttribute", StringComparison.Ordinal)))
        {
            errors.Add("P/Invoke is not allowed in dynamic tools");
        }

        if (root.DescendantNodes().Any(node =>
                node.IsKind(SyntaxKind.UnsafeStatement) ||
                node.IsKind(SyntaxKind.PointerType) ||
                node.IsKind(SyntaxKind.FunctionPointerType) ||
                node.IsKind(SyntaxKind.StackAllocArrayCreationExpression)))
        {
            errors.Add("Unsafe code constructs are not allowed in dynamic tools");
        }

        foreach (var identifier in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (ForbiddenTypeNames.Contains(identifier.Identifier.ValueText, StringComparer.Ordinal))
                errors.Add($"Forbidden API usage: {identifier.Identifier.ValueText}");
        }

        var agentIdValues = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(property => string.Equals(property.Identifier.ValueText, "AgentId", StringComparison.Ordinal))
            .Select(TryExtractConstantString)
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (agentIdValues.Count == 0)
        {
            errors.Add("Source must expose AgentId as a constant string.");
        }
        else if (agentIdValues.Count != 1 || !string.Equals(agentIdValues[0], expectedToolId, StringComparison.Ordinal))
        {
            errors.Add("Source AgentId must match the tool id.");
        }

        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string? TryExtractConstantString(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody?.Expression is LiteralExpressionSyntax literalExpression &&
            literalExpression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literalExpression.Token.ValueText;
        }

        var getter = property.AccessorList?.Accessors.FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter?.ExpressionBody?.Expression is LiteralExpressionSyntax getterLiteral &&
            getterLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return getterLiteral.Token.ValueText;
        }

        if (getter?.Body?.Statements.FirstOrDefault() is ReturnStatementSyntax returnStatement &&
            returnStatement.Expression is LiteralExpressionSyntax returnLiteral &&
            returnLiteral.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return returnLiteral.Token.ValueText;
        }

        return null;
    }
}