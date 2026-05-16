using AGXIOME.Runtime.Middleware;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.Audit;
using AGXIOME.Security.Keys;
using AGXIOME.Security.RateLimit;
using AGXIOME.Security.Rbac;
using AGXIOME.Security.Sanitization;
using AGXIOME.Security.Secrets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AGXIOME.Runtime;

public sealed record AgxiomePermissionMetadata(AgxiomePermission Permission);

public sealed record AgxiomeRateLimitMetadata(RateLimitPolicy Policy);

public static class SecurityAppBootstrapExtensions
{
    private const string DefaultCorsPolicy = "AgxiomeDefault";

    public static WebApplicationBuilder AddAgxiomeSecurityDefaults(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IKeyRotationService>(_ =>
            new InMemoryKeyRotationService(TimeSpan.FromHours(24)));
        builder.Services.TryAddSingleton<IApiKeyStore, InMemoryApiKeyStore>();
        builder.Services.TryAddSingleton<IRbacPolicy, DefaultRbacPolicy>();
        builder.Services.TryAddSingleton<IAuditTrail, InMemoryAuditTrail>();
        builder.Services.TryAddSingleton<AuditLogger>();
        builder.Services.TryAddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        builder.Services.TryAddSingleton<RateLimitPolicyRegistry>();
        builder.Services.TryAddSingleton<IInputSanitizer, InputSanitizer>();
        builder.Services.TryAddSingleton<ISecretsScanner, SecretsScanner>();

        builder.Services.AddCors(opts => opts.AddPolicy(DefaultCorsPolicy, policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? (builder.Environment.IsDevelopment()
                              ? ["http://localhost:3000", "http://localhost:5173"]
                              : []);

            if (origins.Length > 0)
            {
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        }));

        return builder;
    }

    public static async Task<WebApplication> UseAgxiomeSecurityDefaultsAsync(this WebApplication app)
    {
        var bootstrapKey = app.Configuration["AGXIOME_BOOTSTRAP_KEY"]
                           ?? Environment.GetEnvironmentVariable("AGXIOME_BOOTSTRAP_KEY");
        if (!string.IsNullOrWhiteSpace(bootstrapKey))
        {
            var keyStore = app.Services.GetRequiredService<IApiKeyStore>();
            await keyStore.SeedBootstrapKeyAsync(bootstrapKey, msg => app.Logger.LogWarning("{Msg}", msg));
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<ErrorHandlerMiddleware>();
        app.UseCors(DefaultCorsPolicy);
        app.UseMiddleware<AgxiomeAuthMiddleware>();
        app.UseMiddleware<RbacMiddleware>();
        app.UseMiddleware<RateLimiterMiddleware>();
        return app;
    }

    public static TBuilder AllowAnonymousAgxiome<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new AllowAnonymousAttribute()));
        return builder;
    }

    public static TBuilder RequireAgxiomePermission<TBuilder>(this TBuilder builder, AgxiomePermission permission)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new AgxiomePermissionMetadata(permission)));
        return builder;
    }

    public static TBuilder WithAgxiomeRateLimit<TBuilder>(this TBuilder builder, int maxRequests, TimeSpan window, int burstAllowance = 0)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(
            new AgxiomeRateLimitMetadata(new RateLimitPolicy(maxRequests, window, burstAllowance))));
        return builder;
    }
}