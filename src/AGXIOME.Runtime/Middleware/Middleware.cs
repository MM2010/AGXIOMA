// RT-02..RT-05 — Middleware: SecurityHeaders, Auth, RBAC, RateLimiter, ErrorHandler
// All middleware is stateless; dependencies are injected via IServiceProvider.

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.Rbac;
using AGXIOME.Security.RateLimit;
using AGXIOME.Security.Audit;

namespace AGXIOME.Runtime.Middleware;

// ─── RT-02 a) Security Headers ────────────────────────────────────────────────

/// <summary>
/// Adds hardened HTTP security headers on every response.
/// Must be the first middleware so headers are always present even on errors.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.Headers["X-Content-Type-Options"]   = "nosniff";
        ctx.Response.Headers["X-Frame-Options"]          = "DENY";
        ctx.Response.Headers["Referrer-Policy"]          = "strict-origin-when-cross-origin";
        ctx.Response.Headers["Permissions-Policy"]       = "camera=(), microphone=(), geolocation=()";
        ctx.Response.Headers["X-XSS-Protection"]        = "0"; // deprecated but harmless; rely on CSP
        ctx.Response.Headers["Content-Security-Policy"]  =
            "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'";
        await next(ctx);
    }
}

// ─── RT-02 b) Auth Middleware ─────────────────────────────────────────────────

/// <summary>
/// Validates API-key header (X-Api-Key). On success attaches AgxiomePrincipal to Items.
/// JWT bearer validation is delegated to ASP.NET Core auth if configured.
/// Public routes (health, hubs handshake) are passed through unauthenticated.
/// </summary>
public sealed class AgxiomeAuthMiddleware(RequestDelegate next, IApiKeyStore keyStore, AuditLogger audit)
{
    private static readonly HashSet<string> PublicPaths =
    [
        "/health", "/hubs/agent", "/hubs/log", "/hubs/admin"
    ];

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Skip public paths
        if (PublicPaths.Any(p => ctx.Request.Path.StartsWithSegments(p)))
        {
            await next(ctx);
            return;
        }

        var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiKey))
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await WriteError(ctx, "auth_required", "Missing X-Api-Key header");
            return;
        }

        var result = await keyStore.ValidateAsync(apiKey, ctx.RequestAborted);
        if (!result.IsValid || result.Record is null)
        {
            await audit.LogAsync(AuditAction.AuthFailed, AuditSeverity.Warning, apiKey[..Math.Min(8, apiKey.Length)],
                "Auth", outcomeDetail: result.FailureReason);
            ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await WriteError(ctx, "invalid_key", "API key is invalid or expired");
            return;
        }

        var principal = new AgxiomePrincipal(
            AuditLogger.Pseudonymize(apiKey),
            result.Record.Roles,
            result.Record.Scopes);
        ctx.Items["AgxiomePrincipal"] = principal;

        await next(ctx);
    }

    private static Task WriteError(HttpContext ctx, string code, string message)
    {
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new { code, message }));
    }
}

// ─── RT-03 — RBAC Middleware ──────────────────────────────────────────────────

/// <summary>
/// Maps HTTP method + route prefix to a required permission and enforces RBAC.
/// Unauthenticated requests (no principal in Items) are passed through — auth middleware handles them.
/// </summary>
public sealed class RbacMiddleware(RequestDelegate next, IRbacPolicy policy)
{
    // Maps (method, route prefix) → required permission
    private static readonly Dictionary<(string Method, string Prefix), AgxiomePermission> RouteMap = new()
    {
        { ("POST",   "/api/agents"),    AgxiomePermission.AgentCreate },
        { ("DELETE", "/api/agents"),    AgxiomePermission.AgentDelete },
        { ("POST",   "/api/runs"),      AgxiomePermission.AgentRun },
        { ("POST",   "/api/knowledge"), AgxiomePermission.KnowledgeIngest },
        { ("DELETE", "/api/knowledge"), AgxiomePermission.KnowledgeDelete },
        { ("GET",    "/api/admin"),     AgxiomePermission.AdminMetrics },
        { ("POST",   "/api/admin"),     AgxiomePermission.AdminConfig },
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.Items["AgxiomePrincipal"] is not AgxiomePrincipal principal)
        {
            await next(ctx);
            return;
        }

        var method = ctx.Request.Method.ToUpperInvariant();
        var path   = ctx.Request.Path.Value ?? string.Empty;

        foreach (var ((m, prefix), perm) in RouteMap)
        {
            if (method == m && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (!policy.IsAuthorized(principal, perm))
                {
                    ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                        new { code = "forbidden", message = $"Permission '{perm}' required" }));
                    return;
                }
                break;
            }
        }

        await next(ctx);
    }
}

// ─── RT-04 — Rate Limiter Middleware ──────────────────────────────────────────

/// <summary>
/// Applies sliding-window rate limiting keyed by (IP, route).
/// Returns 429 Too Many Requests with Retry-After header on violation.
/// Default policy: 60 req/min per IP per route.
/// </summary>
public sealed class RateLimiterMiddleware(RequestDelegate next, IRateLimiter limiter)
{
    private static readonly RateLimitPolicy DefaultPolicy = new(60, TimeSpan.FromMinutes(1));

    public async Task InvokeAsync(HttpContext ctx)
    {
        var ip    = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var route = ctx.Request.Path.Value ?? "/";
        var key   = $"ip:{ip}:{route}";

        var result = limiter.Check(key, DefaultPolicy);
        if (!result.IsAllowed)
        {
            ctx.Response.StatusCode = 429;
            ctx.Response.Headers["Retry-After"] = ((int)result.RetryAfter.TotalSeconds).ToString();
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(
                new { code = "rate_limited", retryAfterSeconds = (int)result.RetryAfter.TotalSeconds }));
            return;
        }

        await next(ctx);
    }
}

// ─── RT-05 — Error Handler Middleware ────────────────────────────────────────

/// <summary>
/// Catches unhandled exceptions and returns structured JSON error responses.
/// Stack traces are never exposed in non-development environments.
/// </summary>
public sealed class ErrorHandlerMiddleware(RequestDelegate next, IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // Normal cancellation — ignore
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;
            ctx.Response.ContentType = "application/json";

            var body = env.IsDevelopment()
                ? new { code = "internal_error", message = ex.Message, detail = ex.StackTrace }
                : (object)new { code = "internal_error", message = "An unexpected error occurred." };

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
        }
    }
}
