// F10 — Security & Audit Tests
// 48 tests — 6 per SA task (SA-01..SA-08)

using AGXIOME.Security.Rbac;
using AGXIOME.Security.Audit;
using AGXIOME.Security.Keys;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.RateLimit;
using AGXIOME.Security.Sanitization;
using AGXIOME.Security.Csp;
using AGXIOME.Security.Secrets;
using System.Text.RegularExpressions;

namespace AGXIOME.Tests;

// ════════════════════════════════════════════════════════════
// SA-01 — RBAC
// ════════════════════════════════════════════════════════════
public sealed class SA01_RbacTests
{
    private static AgxiomePrincipal Admin    => new("admin-1", [AgxiomeRole.Admin],    []);
    private static AgxiomePrincipal Operator => new("op-1",    [AgxiomeRole.Operator], []);
    private static AgxiomePrincipal Viewer   => new("view-1",  [AgxiomeRole.Viewer],   []);
    private static AgxiomePrincipal Service  => new("svc-1",   [AgxiomeRole.Service],  ["agent:demo"]);

    private readonly IRbacPolicy _policy = new DefaultRbacPolicy();

    [Fact] public void Admin_HasAllPermissions()
    {
        Assert.True(_policy.IsAuthorized(Admin, AgxiomePermission.AdminConfig));
        Assert.True(_policy.IsAuthorized(Admin, AgxiomePermission.AgentDelete));
        Assert.True(_policy.IsAuthorized(Admin, AgxiomePermission.AdminUsers));
    }

    [Fact] public void Operator_CannotDeleteAgent()
        => Assert.False(_policy.IsAuthorized(Operator, AgxiomePermission.AgentDelete));

    [Fact] public void Viewer_CanReadButNotWrite()
    {
        Assert.True( _policy.IsAuthorized(Viewer, AgxiomePermission.AgentRead));
        Assert.False(_policy.IsAuthorized(Viewer, AgxiomePermission.AgentCreate));
        Assert.False(_policy.IsAuthorized(Viewer, AgxiomePermission.AgentRun));
    }

    [Fact] public void Service_HasNarrowPermissions()
    {
        Assert.True( _policy.IsAuthorized(Service, AgxiomePermission.AgentRun));
        Assert.False(_policy.IsAuthorized(Service, AgxiomePermission.AgentCreate));
        Assert.False(_policy.IsAuthorized(Service, AgxiomePermission.AdminConfig));
    }

    [Fact] public void HasRole_ReturnsCorrectly()
    {
        Assert.True( _policy.HasRole(Admin,  AgxiomeRole.Admin));
        Assert.False(_policy.HasRole(Viewer, AgxiomeRole.Admin));
    }

    [Fact] public void PolicyBuilder_AddsPermission()
    {
        var custom = new RbacPolicyBuilder()
            .Allow(AgxiomeRole.Viewer, AgxiomePermission.AgentCreate)
            .Build();
        Assert.True(custom.IsAuthorized(Viewer, AgxiomePermission.AgentCreate));
        // Default viewer cannot create; custom policy allows it
    }
}

// ════════════════════════════════════════════════════════════
// SA-02 — Audit Trail
// ════════════════════════════════════════════════════════════
public sealed class SA02_AuditTrailTests
{
    [Fact] public async Task RecordAsync_StoresEntry()
    {
        var trail  = new InMemoryAuditTrail();
        var entry  = new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            AuditAction.AgentRun, AuditSeverity.Info, "abc123", "Agent", "a1", "success");
        await trail.RecordAsync(entry);
        var results = await trail.QueryAsync(new AuditQuery()).ToListAsync();
        Assert.Single(results);
        Assert.Equal("abc123", results[0].SubjectHash);
    }

    [Fact] public async Task QueryAsync_FiltersAction()
    {
        var trail = new InMemoryAuditTrail();
        await trail.RecordAsync(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            AuditAction.AgentRun, AuditSeverity.Info, "h1", "Agent", null, null));
        await trail.RecordAsync(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            AuditAction.AuthLogin, AuditSeverity.Info, "h2", "Auth", null, null));
        var results = await trail.QueryAsync(new AuditQuery(Action: AuditAction.AgentRun)).ToListAsync();
        Assert.Single(results);
        Assert.Equal(AuditAction.AgentRun, results[0].Action);
    }

    [Fact] public async Task QueryAsync_FiltersMinSeverity()
    {
        var trail = new InMemoryAuditTrail();
        await trail.RecordAsync(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            AuditAction.SecurityAlert, AuditSeverity.Critical, "h", "Sec", null, null));
        await trail.RecordAsync(new AuditEntry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            AuditAction.AgentRun, AuditSeverity.Info, "h2", "Agent", null, null));
        var results = await trail.QueryAsync(new AuditQuery(MinSeverity: AuditSeverity.Warning)).ToListAsync();
        Assert.Single(results);
        Assert.Equal(AuditSeverity.Critical, results[0].Severity);
    }

    [Fact] public void Pseudonymize_IsDeterministic()
    {
        var h1 = AuditLogger.Pseudonymize("user@example.com");
        var h2 = AuditLogger.Pseudonymize("user@example.com");
        Assert.Equal(h1, h2);
        Assert.Equal(16, h1.Length);
    }

    [Fact] public void Pseudonymize_DifferentInputsDifferentHashes()
    {
        var h1 = AuditLogger.Pseudonymize("alice");
        var h2 = AuditLogger.Pseudonymize("bob");
        Assert.NotEqual(h1, h2);
    }

    [Fact] public async Task AuditLogger_WritesEntry()
    {
        var trail  = new InMemoryAuditTrail();
        var logger = new AuditLogger(trail);
        await logger.LogAsync(AuditAction.AuthLogin, AuditSeverity.Info, "user-id", "Auth", outcomeDetail: "success");
        var results = await trail.QueryAsync(new AuditQuery()).ToListAsync();
        Assert.Single(results);
        Assert.Equal(AuditAction.AuthLogin, results[0].Action);
    }
}

// ════════════════════════════════════════════════════════════
// SA-03 — Key Rotation
// ════════════════════════════════════════════════════════════
public sealed class SA03_KeyRotationTests
{
    [Fact] public void GetCurrentKey_ReturnsActiveKey()
    {
        using var svc = new InMemoryKeyRotationService();
        var key = svc.GetCurrentKey();
        Assert.NotNull(key);
        Assert.NotEmpty(key.Kid);
        Assert.True(key.IsActive);
    }

    [Fact] public void FindKey_FindsByKid()
    {
        using var svc = new InMemoryKeyRotationService();
        var key   = svc.GetCurrentKey();
        var found = svc.FindKey(key.Kid);
        Assert.NotNull(found);
        Assert.Equal(key.Kid, found!.Kid);
    }

    [Fact] public void FindKey_ReturnsNull_ForUnknownKid()
    {
        using var svc = new InMemoryKeyRotationService();
        Assert.Null(svc.FindKey("k-notexist"));
    }

    [Fact] public void ForceRotate_ProducesNewKey()
    {
        using var svc = new InMemoryKeyRotationService();
        var old = svc.GetCurrentKey();
        var next = svc.ForceRotate();
        Assert.NotEqual(old.Kid, next.Kid);
    }

    [Fact] public void GetPublicKey_ReturnsCopy()
    {
        using var svc = new InMemoryKeyRotationService();
        var key = svc.GetCurrentKey();
        var pub1 = key.GetPublicKey();
        var pub2 = key.GetPublicKey();
        Assert.Equal(pub1, pub2);
        Assert.NotSame(pub1, pub2); // defensive copy
    }

    [Fact] public void AllActiveKeys_ContainsCurrentKey()
    {
        using var svc = new InMemoryKeyRotationService();
        var current = svc.GetCurrentKey();
        Assert.Contains(svc.AllActiveKeys, k => k.Kid == current.Kid);
    }
}

// ════════════════════════════════════════════════════════════
// SA-04 — API Key Auth
// ════════════════════════════════════════════════════════════
public sealed class SA04_ApiKeyAuthTests
{
    private static readonly IReadOnlyList<AgxiomeRole> _roles = [AgxiomeRole.Operator];
    private static readonly IReadOnlyList<string>      _scopes = ["agent:demo"];

    [Fact] public async Task Create_ThenValidate_Succeeds()
    {
        var store = new InMemoryApiKeyStore();
        var (raw, record) = await store.CreateAsync(_roles, _scopes, "owner-1");
        var result = await store.ValidateAsync(raw);
        Assert.True(result.IsValid);
        Assert.NotNull(result.Record);
    }

    [Fact] public async Task ValidateWrongKey_Fails()
    {
        var store = new InMemoryApiKeyStore();
        await store.CreateAsync(_roles, _scopes, "owner-1");
        var result = await store.ValidateAsync("agx_wrongkey");
        Assert.False(result.IsValid);
    }

    [Fact] public async Task Revoke_PreventsValidation()
    {
        var store = new InMemoryApiKeyStore();
        var (raw, record) = await store.CreateAsync(_roles, _scopes, "owner-1");
        await store.RevokeAsync(record.KeyId);
        var result = await store.ValidateAsync(raw);
        Assert.False(result.IsValid);
        Assert.Equal("invalid_key", result.FailureReason);
    }

    [Fact] public async Task ListByOwner_ReturnsKeys()
    {
        var store = new InMemoryApiKeyStore();
        var ownerHash = AuditLogger.Pseudonymize("owner-1");
        await store.CreateAsync(_roles, _scopes, "owner-1");
        await store.CreateAsync(_roles, _scopes, "owner-1");
        var keys = await store.ListByOwnerAsync(ownerHash);
        Assert.Equal(2, keys.Count);
    }

    [Fact] public async Task ValidateEmpty_Fails()
    {
        var store = new InMemoryApiKeyStore();
        var result = await store.ValidateAsync(string.Empty);
        Assert.False(result.IsValid);
        Assert.Equal("empty_key", result.FailureReason);
    }

    [Fact] public async Task ExpiredKey_Fails()
    {
        var store = new InMemoryApiKeyStore();
        var (raw, _) = await store.CreateAsync(_roles, _scopes, "owner-1",
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1)); // already expired
        var result = await store.ValidateAsync(raw);
        Assert.False(result.IsValid);
    }
}

// ════════════════════════════════════════════════════════════
// SA-05 — Rate Limiting
// ════════════════════════════════════════════════════════════
public sealed class SA05_RateLimitingTests
{
    private static readonly RateLimitPolicy Policy3 = new(3, TimeSpan.FromMinutes(1));

    [Fact] public void Check_AllowsUpToLimit()
    {
        var limiter = new SlidingWindowRateLimiter();
        for (int i = 0; i < 3; i++)
            Assert.True(limiter.Check("key1", Policy3).IsAllowed);
    }

    [Fact] public void Check_BlocksAboveLimit()
    {
        var limiter = new SlidingWindowRateLimiter();
        for (int i = 0; i < 3; i++) limiter.Check("key2", Policy3);
        var result = limiter.Check("key2", Policy3);
        Assert.False(result.IsAllowed);
        Assert.True(result.RetryAfter > TimeSpan.Zero);
    }

    [Fact] public void Reset_ClearsState()
    {
        var limiter = new SlidingWindowRateLimiter();
        for (int i = 0; i < 3; i++) limiter.Check("key3", Policy3);
        limiter.Reset("key3");
        Assert.True(limiter.Check("key3", Policy3).IsAllowed);
    }

    [Fact] public void BurstAllowance_ExtendsLimit()
    {
        var policy  = new RateLimitPolicy(2, TimeSpan.FromMinutes(1), BurstAllowance: 2);
        var limiter = new SlidingWindowRateLimiter();
        for (int i = 0; i < 4; i++)
            Assert.True(limiter.Check("burst", policy).IsAllowed);
        Assert.False(limiter.Check("burst", policy).IsAllowed);
    }

    [Fact] public void DifferentKeys_IndependentCounters()
    {
        var limiter = new SlidingWindowRateLimiter();
        for (int i = 0; i < 3; i++) limiter.Check("ka", Policy3);
        // key "kb" should still be allowed
        Assert.True(limiter.Check("kb", Policy3).IsAllowed);
    }

    [Fact] public void Remaining_DecreasesWithEachRequest()
    {
        var limiter = new SlidingWindowRateLimiter();
        var r1 = limiter.Check("dec", Policy3);
        var r2 = limiter.Check("dec", Policy3);
        Assert.True(r1.Remaining > r2.Remaining);
    }
}

// ════════════════════════════════════════════════════════════
// SA-06 — Input Sanitization
// ════════════════════════════════════════════════════════════
public sealed class SA06_InputSanitizationTests
{
    private readonly IInputSanitizer _san = new InputSanitizer();

    [Fact] public void ValidInput_PassesAlphanumRule()
    {
        var result = _san.Sanitize("my-agent-01", SanitizationRules.AgentId);
        Assert.True(result.IsValid);
        Assert.Equal("my-agent-01", result.SanitizedValue);
    }

    [Fact] public void SqlInjection_Blocked()
    {
        var result = _san.Sanitize("SELECT * FROM agents", SanitizationRules.FreeText);
        Assert.False(result.IsValid);
        Assert.Equal(SanitizationViolation.SqlKeyword, result.Violation);
    }

    [Fact] public void HtmlStripped_WhenStripHtmlEnabled()
    {
        var rule   = new SanitizationRule("test", MaxLength: 100, StripHtml: true, CheckSqlKeywords: false);
        var result = _san.Sanitize("Hello <script>alert(1)</script>", rule);
        Assert.True(result.IsValid);
        Assert.DoesNotContain("<script>", result.SanitizedValue);
    }

    [Fact] public void PathTraversal_Blocked()
    {
        var result = _san.Sanitize("../../etc/passwd", SanitizationRules.AgentId);
        Assert.False(result.IsValid);
        Assert.Equal(SanitizationViolation.PathTraversal, result.Violation);
    }

    [Fact] public void TooLong_Blocked()
    {
        var input  = new string('a', 200);
        var result = _san.Sanitize(input, SanitizationRules.AgentId);
        Assert.False(result.IsValid);
        Assert.Equal(SanitizationViolation.TooLong, result.Violation);
    }

    [Fact] public void Empty_BlockedByDefault()
    {
        var result = _san.Sanitize(string.Empty, SanitizationRules.AgentId);
        Assert.False(result.IsValid);
        Assert.Equal(SanitizationViolation.NullOrEmpty, result.Violation);
    }
}

// ════════════════════════════════════════════════════════════
// SA-07 — CSP Hardening
// ════════════════════════════════════════════════════════════
public sealed class SA07_CspHardeningTests
{
    [Fact] public void DefaultStrict_BuildsNonEmptyHeader()
    {
        var policy = CspBuilder.DefaultStrict();
        var header = policy.ToHeaderValue();
        Assert.NotEmpty(header);
        Assert.Contains("default-src", header);
    }

    [Fact] public void DefaultStrict_HasSelfOrigin()
    {
        var policy = CspBuilder.DefaultStrict();
        var header = policy.ToHeaderValue();
        Assert.Contains("'self'", header);
    }

    [Fact] public void Builder_AddScriptSrc()
    {
        var policy = new CspBuilder()
            .AllowScriptSrc("https://cdn.example.com")
            .Build();
        Assert.Contains("https://cdn.example.com", policy.ToHeaderValue());
    }

    [Fact] public void Validator_FlagsWildcard()
    {
        var policy = new CspBuilder().AllowScriptSrc("*").Build();
        var violations = CspPolicyValidator.ValidateForProduction(policy);
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("wildcard"));
    }

    [Fact] public void Validator_FlagsUnsafeInline()
    {
        var policy = new CspBuilder().AllowScriptSrc("'unsafe-inline'").Build();
        var violations = CspPolicyValidator.ValidateForProduction(policy);
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("unsafe-inline"));
    }

    [Fact] public void DefaultStrict_NoViolationsInProduction()
    {
        var policy     = CspBuilder.DefaultStrict(CspEnvironment.Production);
        var violations = CspPolicyValidator.ValidateForProduction(policy);
        Assert.Empty(violations);
    }
}

// ════════════════════════════════════════════════════════════
// SA-08 — Secrets Scanner
// ════════════════════════════════════════════════════════════
public sealed class SA08_SecretsScannerTests
{
    private readonly ISecretsScanner _scanner = new SecretsScanner();

    [Fact] public void DetectsJwtToken()
    {
        const string jwt = "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ1c2VyIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";
        Assert.True(_scanner.ContainsSecrets(jwt));
    }

    [Fact] public void DetectsOpenAiKey()
    {
        Assert.True(_scanner.ContainsSecrets("sk-abcdefghijklmnopqrstuvwxyz1234567890"));
    }

    [Fact] public void DetectsAwsAccessKey()
    {
        Assert.True(_scanner.ContainsSecrets("AKIAIOSFODNN7EXAMPLE"));
    }

    [Fact] public void Mask_ReplacesSecret()
    {
        var masked = _scanner.Mask("token=sk-abcdefghijklmnopqrstuvwxyz1234567890 rest");
        Assert.DoesNotContain("sk-abc", masked);
        Assert.Contains("***REDACTED***", masked);
    }

    [Fact] public void SafeText_PassesThrough()
    {
        const string safe = "Hello, world! Agent run completed.";
        var result = _scanner.Scan(safe);
        Assert.False(result.HasSecrets);
        Assert.Equal(safe, result.SafeText);
    }

    [Fact] public void DetectsPasswordKeyValue()
    {
        Assert.True(_scanner.ContainsSecrets("password=supersecret123"));
    }
}

// helper
file static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source) list.Add(item);
        return list;
    }
}
