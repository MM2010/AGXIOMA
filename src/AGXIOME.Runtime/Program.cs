// RT-01 — AgentOS Runtime — Middleware pipeline, DI, config
// Full ASP.NET Core 10 host for AGXIOME realtime + REST gateway.
// Middleware order: SecurityHeaders → Auth → RBAC → RateLimit → ErrorHandler → Controllers/Hubs

using AGXIOME.Runtime.Middleware;
using AGXIOME.Runtime.Hubs;
using AGXIOME.Runtime.Workers;
using AGXIOME.Security.Keys;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.Rbac;
using AGXIOME.Security.Audit;
using AGXIOME.Security.RateLimit;
using AGXIOME.Security.Sanitization;
using AGXIOME.Security.Secrets;

var builder = WebApplication.CreateBuilder(args);

// ── Core Services ────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(opts => {
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// ── Security Services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IKeyRotationService>(_ =>
    new InMemoryKeyRotationService(TimeSpan.FromHours(24)));
builder.Services.AddSingleton<IApiKeyStore, InMemoryApiKeyStore>();
builder.Services.AddSingleton<IRbacPolicy, DefaultRbacPolicy>();
builder.Services.AddSingleton<IAuditTrail, InMemoryAuditTrail>();
builder.Services.AddSingleton<AuditLogger>();
builder.Services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
builder.Services.AddSingleton<RateLimitPolicyRegistry>();
builder.Services.AddSingleton<IInputSanitizer, InputSanitizer>();
builder.Services.AddSingleton<ISecretsScanner, SecretsScanner>();

// ── Background Workers ────────────────────────────────────────────────────────
builder.Services.AddHostedService<BackgroundRunWorker>();
builder.Services.AddHostedService<SessionCleanupJob>();
builder.Services.AddHostedService<TokenRotationJob>();

// ── CORS ─────────────────────────────────────────────────────────────────────
// Production: replace origins with explicit allowlist from config.
builder.Services.AddCors(opts => opts.AddPolicy("AgxiomeDefault", policy => {
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? (builder.Environment.IsDevelopment() ? ["http://localhost:3000", "http://localhost:5173"] : []);
    policy.WithOrigins(origins)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials();
}));

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ErrorHandlerMiddleware>();
app.UseCors("AgxiomeDefault");
app.UseMiddleware<AgxiomeAuthMiddleware>();
app.UseMiddleware<RbacMiddleware>();
app.UseMiddleware<RateLimiterMiddleware>();

app.MapControllers();

// SignalR hubs
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<LogHub>("/hubs/log");
app.MapHub<AdminHub>("/hubs/admin");

app.MapGet("/health", () => Results.Ok(new { Status = "healthy", Ts = DateTimeOffset.UtcNow }));

app.Run();

// Make the implicit Program class accessible for WebApplicationFactory in tests
public partial class Program { }
