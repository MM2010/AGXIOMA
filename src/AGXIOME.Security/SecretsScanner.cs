// SA-08 — Secrets Scanner
// Prevents accidental exposure of credentials, tokens, keys, and connection strings
// in log output, serialized payloads, and structured error responses.
// Uses pattern-based detection + fixed masking ("***REDACTED***").
// Designed to be called at logging boundaries, not as a security gate.

using System.Text.RegularExpressions;

namespace AGXIOME.Security.Secrets;

// ─── Enums ───────────────────────────────────────────────────────────────────

/// <summary>Enumeration type for the AGXIOME framework.</summary>
public enum SecretCategory
{
    ApiKey         = 0,
    JwtToken       = 1,
    ConnectionString = 2,
    PrivateKey     = 3,
    Password       = 4,
    AwsCredential  = 5,
    AzureKey       = 6,
    GitHubToken    = 7,
    GenericSecret  = 8
}

// ─── Value Objects ────────────────────────────────────────────────────────────

public sealed record SecretPattern(
    string          Name,
    SecretCategory  Category,
    Regex           Pattern,
    string          Replacement = "***REDACTED***");

public sealed record ScanResult(
    bool            HasSecrets,
    string          SafeText,
    IReadOnlyList<SecretMatch> Matches);

public sealed record SecretMatch(
    string          PatternName,
    SecretCategory  Category,
    int             StartIndex,
    int             Length);

// ─── Interface ────────────────────────────────────────────────────────────────

public interface ISecretsScanner
{
    ScanResult Scan(string text);
    string Mask(string text);
    bool ContainsSecrets(string text);
}

// ─── Implementation ───────────────────────────────────────────────────────────

public sealed class SecretsScanner : ISecretsScanner
{
    private readonly IReadOnlyList<SecretPattern> _patterns;

    public SecretsScanner() : this(BuiltInPatterns.All) { }

    public SecretsScanner(IReadOnlyList<SecretPattern> patterns) =>
        _patterns = patterns;

    public ScanResult Scan(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new ScanResult(false, text, []);

        var matches  = new List<SecretMatch>();
        var safe     = text;

        // Apply patterns in order; each pass operates on the already-masked string
        // to avoid double-matching the replacement token.
        foreach (var pattern in _patterns)
        {
            var regex   = pattern.Pattern;
            var rawMatches = regex.Matches(text);
            foreach (Match m in rawMatches)
                matches.Add(new SecretMatch(pattern.Name, pattern.Category, m.Index, m.Length));
            safe = regex.Replace(safe, pattern.Replacement);
        }

        return new ScanResult(matches.Count > 0, safe, matches);
    }

    public string Mask(string text) => Scan(text).SafeText;

    public bool ContainsSecrets(string text) => Scan(text).HasSecrets;
}

// ─── Built-In Patterns ────────────────────────────────────────────────────────

public static class BuiltInPatterns
{
    private static RegexOptions O => RegexOptions.Compiled | RegexOptions.IgnoreCase;

    public static readonly IReadOnlyList<SecretPattern> All =
    [
        // JWT tokens (3-part base64url)
        new("JWT", SecretCategory.JwtToken,
            new Regex(@"eyJ[A-Za-z0-9\-_]+\.eyJ[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+", O)),

        // OpenAI API keys
        new("OpenAI_Key", SecretCategory.ApiKey,
            new Regex(@"sk-[A-Za-z0-9]{20,}", O)),

        // AWS credentials
        new("AWS_AccessKey", SecretCategory.AwsCredential,
            new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)),
        new("AWS_SecretKey", SecretCategory.AwsCredential,
            new Regex(@"(?i)aws[_\-]?(secret|access)[_\-]?key[^=]*=[^=\s]{20,}", O)),

        // Azure connection strings / keys
        new("Azure_ConnStr", SecretCategory.AzureKey,
            new Regex(@"DefaultEndpointsProtocol=https;AccountName=[^;]+;AccountKey=[^;]+", O)),
        new("Azure_Key", SecretCategory.AzureKey,
            new Regex(@"(?i)(accountkey|sharedaccesskey)\s*=\s*[A-Za-z0-9+/=]{20,}", O)),

        // GitHub tokens
        new("GitHub_Token", SecretCategory.GitHubToken,
            new Regex(@"gh[pousr]_[A-Za-z0-9]{36,}", O)),

        // Private key blocks (PEM)
        new("PEM_Private", SecretCategory.PrivateKey,
            new Regex(@"-----BEGIN (EC |RSA )?PRIVATE KEY-----[\s\S]*?-----END (EC |RSA )?PRIVATE KEY-----", O)),

        // Generic password in key=value pairs
        new("GenericPassword", SecretCategory.Password,
            new Regex(@"(?i)(password|passwd|pwd|secret|token|apikey|api_key)\s*[=:]\s*[^\s,;""']{6,}", O)),

        // PostgreSQL / Redis connection strings
        new("ConnString_Postgres", SecretCategory.ConnectionString,
            new Regex(@"(?i)(postgres(?:ql)?|redis)://[^\s@]*:[^\s@]+@", O)),
        new("ConnString_SQLServer", SecretCategory.ConnectionString,
            new Regex(@"(?i)(user id|uid|password|pwd)=[^;""]{4,}", O)),
    ];
}
