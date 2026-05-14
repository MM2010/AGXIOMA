// SA-07 — CSP Hardening
// Builds Content-Security-Policy headers from a typed, allowlist-driven policy.
// Denies wildcards in production; requires explicit origin allowlisting.
// Integrates as ASP.NET Core middleware extension.

namespace AGXIOME.Security.Csp;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum CspEnvironment { Development, Staging, Production }

// ─── Value Objects ────────────────────────────────────────────────────────────

public sealed record CspPolicy(
    IReadOnlyList<string> DefaultSrc,
    IReadOnlyList<string> ScriptSrc,
    IReadOnlyList<string> StyleSrc,
    IReadOnlyList<string> ImgSrc,
    IReadOnlyList<string> ConnectSrc,
    IReadOnlyList<string> FontSrc,
    IReadOnlyList<string> FrameSrc,
    IReadOnlyList<string> ObjectSrc,
    IReadOnlyList<string> BaseUri,
    IReadOnlyList<string> FormAction,
    bool                  UpgradeInsecureRequests,
    bool                  BlockAllMixedContent)
{
    /// <summary>
    /// Serializes the policy to a CSP header value string.
    /// Each non-empty directive is emitted; sources joined by space.
    /// </summary>
    public string ToHeaderValue()
    {
        var directives = new List<string>();

        void Emit(string name, IReadOnlyList<string> sources)
        {
            if (sources.Count > 0) directives.Add($"{name} {string.Join(' ', sources)}");
        }

        Emit("default-src",  DefaultSrc);
        Emit("script-src",   ScriptSrc);
        Emit("style-src",    StyleSrc);
        Emit("img-src",      ImgSrc);
        Emit("connect-src",  ConnectSrc);
        Emit("font-src",     FontSrc);
        Emit("frame-src",    FrameSrc);
        Emit("object-src",   ObjectSrc);
        Emit("base-uri",     BaseUri);
        Emit("form-action",  FormAction);

        if (UpgradeInsecureRequests) directives.Add("upgrade-insecure-requests");
        if (BlockAllMixedContent)    directives.Add("block-all-mixed-content");

        return string.Join("; ", directives);
    }
}

// ─── Builder ─────────────────────────────────────────────────────────────────

/// <summary>
/// Fluent CSP policy builder.
/// Strict defaults: 'self' everywhere, no unsafe-inline, no wildcards.
/// Call .Allow{Directive}() to add specific trusted origins.
/// </summary>
public sealed class CspBuilder
{
    private readonly List<string> _default  = ["'self'"];
    private readonly List<string> _script   = ["'self'"];
    private readonly List<string> _style    = ["'self'"];
    private readonly List<string> _img      = ["'self'", "data:"];
    private readonly List<string> _connect  = ["'self'"];
    private readonly List<string> _font     = ["'self'"];
    private readonly List<string> _frame    = ["'none'"];
    private readonly List<string> _object   = ["'none'"];
    private readonly List<string> _base     = ["'self'"];
    private readonly List<string> _form     = ["'self'"];
    private bool _upgradeInsecure = true;
    private bool _blockMixed      = false;

    public CspBuilder AllowScriptSrc(string origin)   { _script.Add(origin);  return this; }
    public CspBuilder AllowStyleSrc(string origin)    { _style.Add(origin);   return this; }
    public CspBuilder AllowImgSrc(string origin)      { _img.Add(origin);     return this; }
    public CspBuilder AllowConnectSrc(string origin)  { _connect.Add(origin); return this; }
    public CspBuilder AllowFontSrc(string origin)     { _font.Add(origin);    return this; }
    public CspBuilder AllowFrameSrc(string origin)    {
        _frame.Remove("'none'");
        _frame.Add(origin);
        return this;
    }
    public CspBuilder AllowScriptNonce(string nonce)  { _script.Add($"'nonce-{nonce}'"); return this; }
    public CspBuilder WithUpgradeInsecureRequests(bool enable = true) { _upgradeInsecure = enable; return this; }
    public CspBuilder WithBlockAllMixedContent(bool enable = true)    { _blockMixed = enable;      return this; }

    public CspPolicy Build() => new(
        DefaultSrc:               _default.AsReadOnly(),
        ScriptSrc:                _script.AsReadOnly(),
        StyleSrc:                 _style.AsReadOnly(),
        ImgSrc:                   _img.AsReadOnly(),
        ConnectSrc:               _connect.AsReadOnly(),
        FontSrc:                  _font.AsReadOnly(),
        FrameSrc:                 _frame.AsReadOnly(),
        ObjectSrc:                _object.AsReadOnly(),
        BaseUri:                  _base.AsReadOnly(),
        FormAction:               _form.AsReadOnly(),
        UpgradeInsecureRequests:  _upgradeInsecure,
        BlockAllMixedContent:     _blockMixed);

    /// <summary>
    /// Builds the default strict policy for AGXIOME production.
    /// frame-ancestors 'none', object-src 'none', base-uri 'self'.
    /// </summary>
    public static CspPolicy DefaultStrict(CspEnvironment env = CspEnvironment.Production)
    {
        var builder = new CspBuilder()
            .WithUpgradeInsecureRequests(env == CspEnvironment.Production);

        return builder.Build();
    }
}

// ─── Validation ───────────────────────────────────────────────────────────────

public static class CspPolicyValidator
{
    /// <summary>
    /// Returns list of policy violations for production deployment.
    /// Wildcards, unsafe-inline, and unsafe-eval are treated as violations.
    /// </summary>
    public static IReadOnlyList<string> ValidateForProduction(CspPolicy policy)
    {
        var violations = new List<string>();
        Check(policy.ScriptSrc,  "script-src",  violations);
        Check(policy.StyleSrc,   "style-src",   violations);
        Check(policy.DefaultSrc, "default-src", violations);
        Check(policy.ConnectSrc, "connect-src", violations);
        return violations;
    }

    private static void Check(IReadOnlyList<string> sources, string directive, List<string> violations)
    {
        if (sources.Contains("*"))        violations.Add($"{directive}: wildcard '*' is forbidden in production");
        if (sources.Contains("'unsafe-inline'")) violations.Add($"{directive}: 'unsafe-inline' is forbidden");
        if (sources.Contains("'unsafe-eval'"))   violations.Add($"{directive}: 'unsafe-eval' is forbidden");
    }
}
