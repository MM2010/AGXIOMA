// RT-01 — AgentOS Runtime — Middleware pipeline, DI, config
// Full ASP.NET Core 10 host for AGXIOME realtime + REST gateway.
// Middleware order: SecurityHeaders → Auth → RBAC → RateLimit → ErrorHandler → Controllers/Hubs

using AGXIOME.Runtime;
using AGXIOME.Runtime.Controllers;
using AGXIOME.Runtime.Hubs;
using AGXIOME.Runtime.Workers;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Execution;
using AGXIOME.Core.Tools;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;
using RuntimeBackgroundWorker = AGXIOME.Runtime.Workers.BackgroundRunWorker;

var builder = WebApplication.CreateBuilder(args);

// ── Core Services ────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        var errorCode = context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => AgxiomeErrorCode.TL_InvalidArguments,
            StatusCodes.Status401Unauthorized => AgxiomeErrorCode.SE_AuthenticationFailed,
            StatusCodes.Status403Forbidden => AgxiomeErrorCode.SE_AuthorizationDenied,
            StatusCodes.Status404NotFound => AgxiomeErrorCode.TL_NotFound,
            StatusCodes.Status405MethodNotAllowed => AgxiomeErrorCode.TL_InvalidArguments,
            StatusCodes.Status415UnsupportedMediaType => AgxiomeErrorCode.TL_InvalidArguments,
            _ => AgxiomeErrorCode.InternalError,
        };

        context.ProblemDetails.Title = ErrorCatalog.Get(errorCode).Category;
        context.ProblemDetails.Detail = ErrorCatalog.GetUserMessage(errorCode);
        context.ProblemDetails.Extensions["code"] = ErrorCatalog.GetCode(errorCode);
        context.ProblemDetails.Extensions["numericCode"] = (int)errorCode;
        context.ProblemDetails.Extensions["category"] = ErrorCatalog.Get(errorCode).Category;
    };
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = ApiErrors.InvalidModelState;
});
builder.AddAgxiomeSecurityDefaults();

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(opts => {
    opts.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
builder.Services.AddSingleton<IAgentEventReplayStore, InMemoryAgentEventReplayStore>();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? builder.Configuration["ConnectionStrings:Redis"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var options = ConfigurationOptions.Parse(redisConnectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = Math.Max(options.ConnectTimeout, 5000);
        options.SyncTimeout = Math.Max(options.SyncTimeout, 5000);
        return ConnectionMultiplexer.Connect(options);
    });
    builder.Services.AddSingleton<IAgentEventReplayStore, RedisAgentEventReplayStore>();
}

// ── Background Workers ────────────────────────────────────────────────────────
builder.Services.AddHostedService<RuntimeBackgroundWorker>();
builder.Services.AddHostedService<SessionCleanupJob>();
builder.Services.AddHostedService<TokenRotationJob>();

// ── Execution Pipeline + Tool Infra (optional — configured via appsettings) ──
// Set "Agxiome:ExecutionPipeline:Enabled": true to activate P09/P11 pipelines.
// When enabled, RunController and ToolController use real pipeline dispatch.
// When disabled, they run in stub mode (safe for local dev without a model key).
if (builder.Configuration.GetValue<bool>("Agxiome:ExecutionPipeline:Enabled"))
{
    var providerStr  = builder.Configuration["Agxiome:Provider"] ?? "DeepSeek";
    var model        = builder.Configuration["Agxiome:Model"] ?? "deepseek-chat";
    var apiKey       = builder.Configuration["Agxiome:ApiKey"]
                       ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                       ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    if (Enum.TryParse<LLMProviderType>(providerStr, ignoreCase: true, out var provider))
    {
        var agent = AGXIOME.Core.AgxiomeBuilder
            .Agent("runtime-agent")
            .UseModel(provider, model, apiKey)
            .WithSystemPrompt("You are a helpful AI assistant.")
            .Build();

        builder.Services.AddSingleton(agent);
        builder.Services.AddSingleton<AGXIOME.Shared.Interfaces.IAgxiomeAgent>(_ => agent);
        builder.Services.AddAgxiome().AddExecutionPipeline().AddToolInfra();
    }
}

var app = builder.Build();
await app.UseAgxiomeSecurityDefaultsAsync();

app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;
        var agxiomeException = exception as AgxiomeException
            ?? new AgxiomeException(
                AgxiomeErrorCode.InternalError,
                technicalMessage: "Unhandled runtime exception.",
                userMessage: ErrorCatalog.GetUserMessage(AgxiomeErrorCode.InternalError),
                innerException: exception);

        await ApiErrors.ToResult(agxiomeException, StatusCodes.Status500InternalServerError).ExecuteAsync(context);
    });
});

app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (response.HasStarted)
    {
        return;
    }

    await ApiErrors.FromStatusCode(statusContext.HttpContext).ExecuteAsync(statusContext.HttpContext);
});

app.MapControllers();

// SignalR hubs
app.MapHub<AgentHub>("/hubs/agent");
app.MapHub<LogHub>("/hubs/log");
app.MapHub<AdminHub>("/hubs/admin");

app.MapGet("/health", () => Results.Ok(new { Status = "healthy", Ts = DateTimeOffset.UtcNow }))
    .AllowAnonymousAgxiome();

app.Run();

// Make the implicit Program class accessible for WebApplicationFactory in tests
public partial class Program { }
