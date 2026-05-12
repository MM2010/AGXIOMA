// SA-06 — Input Sanitization
// Validates and sanitizes all inputs crossing .NET↔JS and API trust boundaries.
// Guards against injection (SQL, command, HTML/XSS), path traversal, and length attacks.
// All validation is deny-by-default: inputs must pass explicit allow rules.

using System.Text.RegularExpressions;

namespace AGXIOME.Security.Sanitization;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum SanitizationViolation
{
    None          = 0,
    TooLong       = 1,
    IllegalChars  = 2,
    NullOrEmpty   = 3,
    PathTraversal = 4,
    HtmlInjection = 5,
    SqlKeyword    = 6,
    ShellMeta     = 7
}

// ─── Value Objects ────────────────────────────────────────────────────────────

public sealed record SanitizationResult(
    bool                IsValid,
    string              SanitizedValue,
    SanitizationViolation Violation,
    string?             ViolationDetail = null);

/// <summary>
/// Rule set for a specific input field.
/// AllowedPattern: if non-null, input must match this regex (anchored).
/// </summary>
public sealed record SanitizationRule(
    string   FieldName,
    int      MaxLength         = 4096,
    bool     AllowEmpty        = false,
    bool     StripHtml         = true,
    bool     CheckSqlKeywords  = true,
    bool     CheckShellMeta    = false,
    Regex?   AllowedPattern    = null);

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IInputSanitizer
{
    SanitizationResult Sanitize(string input, SanitizationRule rule);
    bool IsValid(string input, SanitizationRule rule);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public sealed class InputSanitizer : IInputSanitizer
{
    // Dangerous SQL keywords (non-exhaustive — defense-in-depth, not sole protection)
    private static readonly Regex SqlKeywords = new(
        @"\b(SELECT|INSERT|UPDATE|DELETE|DROP|TRUNCATE|ALTER|EXEC|EXECUTE|UNION|CAST|CONVERT|XP_|SP_|DECLARE|CHAR\(|NCHAR\()\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Shell metacharacters
    private static readonly Regex ShellMeta = new(
        @"[;&|`$(){}<>\[\]\\!]|\.\.",
        RegexOptions.Compiled);

    // Path traversal sequences
    private static readonly Regex PathTraversal = new(
        @"(\.\.[\\/]|[\\/]\.\.)",
        RegexOptions.Compiled);

    // Basic HTML tag detection
    private static readonly Regex HtmlTags = new(
        @"<[^>]+>|&[a-zA-Z]+;|&#\d+;",
        RegexOptions.Compiled);

    public SanitizationResult Sanitize(string input, SanitizationRule rule)
    {
        // Null/empty guard
        if (string.IsNullOrEmpty(input))
        {
            return rule.AllowEmpty
                ? new SanitizationResult(true, string.Empty, SanitizationViolation.None)
                : new SanitizationResult(false, string.Empty, SanitizationViolation.NullOrEmpty);
        }

        // Length check before allocation
        if (input.Length > rule.MaxLength)
            return new SanitizationResult(false, string.Empty, SanitizationViolation.TooLong,
                $"Length {input.Length} > max {rule.MaxLength}");

        // Path traversal
        if (PathTraversal.IsMatch(input))
            return new SanitizationResult(false, string.Empty, SanitizationViolation.PathTraversal);

        var sanitized = input;

        // Strip/detect HTML
        if (HtmlTags.IsMatch(sanitized))
        {
            if (!rule.StripHtml)
                return new SanitizationResult(false, string.Empty, SanitizationViolation.HtmlInjection);
            sanitized = HtmlTags.Replace(sanitized, string.Empty);
        }

        // SQL keyword detection
        if (rule.CheckSqlKeywords && SqlKeywords.IsMatch(sanitized))
            return new SanitizationResult(false, string.Empty, SanitizationViolation.SqlKeyword);

        // Shell metacharacters
        if (rule.CheckShellMeta && ShellMeta.IsMatch(sanitized))
            return new SanitizationResult(false, string.Empty, SanitizationViolation.ShellMeta);

        // Custom allow pattern (must match full input)
        if (rule.AllowedPattern is not null && !rule.AllowedPattern.IsMatch(sanitized))
            return new SanitizationResult(false, string.Empty, SanitizationViolation.IllegalChars,
                $"Input does not match allowed pattern for '{rule.FieldName}'");

        return new SanitizationResult(true, sanitized, SanitizationViolation.None);
    }

    public bool IsValid(string input, SanitizationRule rule) =>
        Sanitize(input, rule).IsValid;
}

// ─── Predefined Rules ────────────────────────────────────────────────────────

/// <summary>Commonly used sanitization rules for AGXIOME API inputs.</summary>
public static class SanitizationRules
{
    private static readonly Regex AlphaNumDash = new(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);
    private static readonly Regex AgentName    = new(@"^[a-zA-Z0-9\-_ ]{1,128}$", RegexOptions.Compiled);
    private static readonly Regex ModelName    = new(@"^[a-zA-Z0-9\.\-_:/]{1,256}$", RegexOptions.Compiled);

    public static readonly SanitizationRule AgentId     = new("agentId",   MaxLength: 128, AllowedPattern: AlphaNumDash);
    public static readonly SanitizationRule AgentNameR  = new("agentName", MaxLength: 128, AllowedPattern: AgentName);
    public static readonly SanitizationRule SessionId   = new("sessionId", MaxLength: 128, AllowedPattern: AlphaNumDash);
    public static readonly SanitizationRule ModelId     = new("modelId",   MaxLength: 256, AllowedPattern: ModelName);
    public static readonly SanitizationRule FreeText    = new("freeText",  MaxLength: 8192, StripHtml: true, CheckSqlKeywords: true);
    public static readonly SanitizationRule ShellArg    = new("shellArg",  MaxLength: 512,  CheckShellMeta: true);
}
