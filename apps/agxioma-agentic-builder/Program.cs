using System.Text.Json;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Core.Generator;
using AgxiomaAgenticBuilder;

// ══════════════════════════════════════════════════════════════════════════════
// AGXIOME Agentic Project Builder
//
// This application uses AGXIOME's 3-layer project generator to scaffold,
// agent-generate, sandbox-build-fix, and validate a full incident intelligence
// platform — entirely from a specification manifest.
//
// Usage:
//   $env:DEEPSEEK_API_KEY="sk-..."
//   dotnet run                                         # prompt da file
//   dotnet run -- --prompt "..."                       # prompt inline
//   dotnet run -- --prompt-file prompt.md               # prompt da file
//   dotnet run -- --sandbox                            # + build-fix loop
//   dotnet run -- --serve                              # modalità API
// ══════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAgxiome(options => ConfigureRegisteredBuilderAgents(options));

// ── Parse CLI ────────────────────────────────────────────────────────────────
var outputPath = "../generated/incident-platform";
var useSandbox = args.Contains("--sandbox");
var useServe   = args.Contains("--serve");
var maxAttempts = 5;

for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--output") outputPath = args[i + 1];

// ── Load prompt ──────────────────────────────────────────────────────────────
string? promptText = null;

// 1. --prompt "inline text"
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--prompt") promptText = args[i + 1];

// 2. --prompt-file path
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--prompt-file" && File.Exists(args[i + 1]))
        promptText = await File.ReadAllTextAsync(args[i + 1]);

// 3. Default: use embedded prompt
if (string.IsNullOrWhiteSpace(promptText))
{
    var promptPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "prompt.md");
    if (File.Exists(promptPath))
        promptText = await File.ReadAllTextAsync(promptPath);
}

if (string.IsNullOrWhiteSpace(promptText))
{
    Console.Error.WriteLine("No prompt provided. Use --prompt or --prompt-file.");
    return;
}

var app = builder.Build();
var registeredAgentFactory = app.Services.GetRequiredService<IAgxiomeAgentFactory>();

// ──── BUILD & RUN THE GENERATOR ──────────────────────────────────────────────

Console.WriteLine(" AGXIOME Agentic Project Builder ");
Console.WriteLine("═══════════════════════════════════");
Console.WriteLine($"Output: {Path.GetFullPath(outputPath)}");
Console.WriteLine($"Mode:   {(useSandbox ? "Agentic + Sandbox" : "Prompt → Manifest → Scaffold")}");
Console.WriteLine($"Prompt: {promptText[..Math.Min(80, promptText.Length)]}...");
Console.WriteLine();

var generator = AGXIOME.Core.Generator.AGXIOME.Create()
    .GenerateProject("IncidentPlatform")
    .Prompt(promptText)
    .UseRegisteredAgentFactory(registeredAgentFactory)

    // ── Key: use LLM to generate the manifest from the prompt ───────────
    .UseManifestFromPrompt()

    // ── Architecture hints (overridable by LLM manifest) ────────────────
    .Architecture("modular-monolith")
    .OutputTo(outputPath)

    // ── Agentic code generation ─────────────────────────────────────────
    .UseAgenticCodeGen("deepseek", "deepseek-chat")

    // ── Guardrails ──────────────────────────────────────────────────────
    .Guardrails(g => g
        .RequireUnitTests()
        .RequireStructuredLogging()
        .RequireOpenTelemetry()
        .RequireHealthChecks()
        .RequireErrorHandling()
        .RequireSOLID()
        .RequireDRY()
        .RequireTDD()
        .DenyRawSql())

    // ── Wiki ────────────────────────────────────────────────────────────
    .GenerateWiki();

// ── Sandbox (always on when using agentic code gen) ──────────────────────────
// Use --sandbox to override max attempts (default 5)
generator.CompileUntilGreen(useSandbox ? maxAttempts : 5);

// ──── EXECUTE ────────────────────────────────────────────────────────────────

Console.WriteLine(" Generating project...\n");

var sw = System.Diagnostics.Stopwatch.StartNew();
var result = await generator.GenerateAsync();
sw.Stop();

Console.WriteLine();
Console.WriteLine("═════════════════════════════════════════════════════");
Console.WriteLine(" GENERATION COMPLETE");
Console.WriteLine("═════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine($"  Project:      {result.ProjectName}");
Console.WriteLine($"  Duration:     {result.Duration.TotalSeconds:F1}s");
Console.WriteLine($"  Success:      {result.Success}");
Console.WriteLine($"  Output:       {result.OutputPath}");
Console.WriteLine();

// Generation stats
Console.WriteLine("── Scaffolding ───────────────────────────────────────");
Console.WriteLine($"  Created:      {result.Generation.Created}");
Console.WriteLine($"  Modified:     {result.Generation.Modified}");
Console.WriteLine($"  Skipped:      {result.Generation.Skipped}");
Console.WriteLine($"  Total files:  {result.Generation.TotalActions}");

PrintCooperativePlan(result.CooperativePlan);

// Guardrail stats
Console.WriteLine();
Console.WriteLine("── Guardrails ────────────────────────────────────────");
Console.WriteLine($"  Passed:       {result.Validation.Passed}/{result.Validation.TotalChecks}");
Console.WriteLine($"  Failed:       {result.Validation.Failed}");
Console.WriteLine($"  Blocked:      {result.Validation.Blocked}");

if (result.Fulfillment is not null)
{
    Console.WriteLine();
    Console.WriteLine("── Prompt Fulfillment ───────────────────────────────");
    Console.WriteLine($"  Passed:       {result.Fulfillment.Passed}/{result.Fulfillment.TotalChecks}");
    Console.WriteLine($"  Failed:       {result.Fulfillment.Failed}");
}

if (result.RuntimeSmoke is not null)
{
    Console.WriteLine();
    Console.WriteLine("── Runtime Smoke ───────────────────────────────────");
    Console.WriteLine($"  Success:      {result.RuntimeSmoke.Success}");
    Console.WriteLine($"  Port:         {result.RuntimeSmoke.Port}");
    Console.WriteLine($"  Checks:       {result.RuntimeSmoke.Checks.Count(c => c.Passed)}/{result.RuntimeSmoke.Checks.Count}");
}

Console.WriteLine();
Console.WriteLine("── Files Generated ──────────────────────────────────");
foreach (var action in result.Generation.Actions.Where(a => a.Type != FileActionType.Skip).Take(25))
    Console.WriteLine($"  [{action.Type,-6}] {action.RelativePath}");
var remainingFiles = result.Generation.Actions.Count(a => a.Type != FileActionType.Skip) - 25;
if (remainingFiles > 0) Console.WriteLine($"  ... and {remainingFiles} more files");

Console.WriteLine();
Console.WriteLine($" Project generated at: {result.OutputPath}");
Console.WriteLine();
Console.WriteLine(" Next steps:");
Console.WriteLine($"   cd {result.OutputPath}");
Console.WriteLine("   dotnet restore");
Console.WriteLine("   dotnet build");
Console.WriteLine("   docker compose up -d");
Console.WriteLine("   dotnet run --project src/Api/Api.csproj");
Console.WriteLine("   open docs/wiki/");

// ── Optional: API mode ───────────────────────────────────────────────────────
if (useServe)
{
    Console.WriteLine("\n Starting API server on port 5020...");
    Console.WriteLine(" POST /generate with { \"prompt\": \"...\" } to trigger generation\n");
    app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0.0-agentic" }));

    app.MapPost("/generate", async (GenRequest req, CancellationToken ct) =>
    {
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        var gen = AGXIOME.Core.Generator.AGXIOME.Create()
            .GenerateProject(req.Name ?? "IncidentPlatform")
            .Prompt(req.Prompt ?? "")
            .UseRegisteredAgentFactory(app.Services.GetRequiredService<IAgxiomeAgentFactory>())
            .UseManifestFromPrompt()
            .UseAgenticCodeGen()
            .UseSandbox(3)
            .GenerateWiki()
            .OutputTo(outputPath);

        var res = await gen.GenerateAsync(ct);
        sw2.Stop();

        return Results.Ok(new
        {
            res.Success,
            DurationSeconds = sw2.Elapsed.TotalSeconds,
            res.Generation.Created,
            res.Generation.Modified,
            res.Validation.Passed,
            res.Validation.Failed,
            res.Validation.Blocked,
            FulfillmentPassed = res.Fulfillment?.Passed,
            FulfillmentTotal = res.Fulfillment?.TotalChecks,
            FulfillmentFailed = res.Fulfillment?.Failed,
            RuntimeSmokeSuccess = res.RuntimeSmoke?.Success,
            RuntimeSmokeChecks = res.RuntimeSmoke?.Checks.Count,
            CooperativePlan = res.CooperativePlan?.Steps.Select(step => new
            {
                step.Id,
                step.ModuleId,
                Owner = step.Owner.ToString(),
                step.TargetPath,
                step.Goal,
                step.Notes
            }),
            res.OutputPath
        });
    });

    await app.RunAsync();
}

// Sandbox stats
if (result.Sandbox is not null)
{
    Console.WriteLine();
    Console.WriteLine("── Sandbox Build-Fix ───────────────────────────────");
    Console.WriteLine($"  Build OK:     {result.Sandbox.Success}");
    Console.WriteLine($"  Attempts:     {result.Sandbox.Attempts}");
    foreach (var attempt in result.Sandbox.AttemptsLog)
    {
        var status = attempt.Success ? "OK" : $"FAIL ({attempt.Errors.Count} errors)";
        Console.WriteLine($"  Attempt {attempt.AttemptNumber}: {status} in {attempt.Duration.TotalSeconds:F1}s");
        foreach (var err in attempt.Errors.Take(5))
            Console.WriteLine($"    {err.Code}: {err.Message}");
        if (attempt.Errors.Count > 5)
            Console.WriteLine($"    ... and {attempt.Errors.Count - 5} more errors");
    }
}

PrintCooperativePlan(result.CooperativePlan);

// Guardrail stats
Console.WriteLine();
Console.WriteLine("── Guardrails ────────────────────────────────────────");
Console.WriteLine($"  Passed:       {result.Validation.Passed}/{result.Validation.TotalChecks}");
Console.WriteLine($"  Failed:       {result.Validation.Failed}");
Console.WriteLine($"  Blocked:      {result.Validation.Blocked}");

if (result.Fulfillment is not null)
{
    Console.WriteLine();
    Console.WriteLine("── Prompt Fulfillment ───────────────────────────────");
    Console.WriteLine($"  Passed:       {result.Fulfillment.Passed}/{result.Fulfillment.TotalChecks}");
    Console.WriteLine($"  Failed:       {result.Fulfillment.Failed}");
    foreach (var check in result.Fulfillment.Checks.Where(c => !c.Passed).Take(10))
        Console.WriteLine($"    {check.Id}: {check.Evidence}");
}

if (result.RuntimeSmoke is not null)
{
    Console.WriteLine();
    Console.WriteLine("── Runtime Smoke ───────────────────────────────────");
    Console.WriteLine($"  Success:      {result.RuntimeSmoke.Success}");
    Console.WriteLine($"  Port:         {result.RuntimeSmoke.Port}");
    foreach (var check in result.RuntimeSmoke.Checks)
        Console.WriteLine($"    {check.Id}: {(check.Passed ? "OK" : "FAIL")} - {check.Evidence}");
}

Console.WriteLine();
Console.WriteLine("── Files Generated ──────────────────────────────────");
foreach (var action in result.Generation.Actions.Where(a => a.Type != FileActionType.Skip).Take(20))
    Console.WriteLine($"  [{action.Type,-6}] {action.RelativePath}");
var remaining = result.Generation.Actions.Count(a => a.Type != FileActionType.Skip) - 20;
if (remaining > 0) Console.WriteLine($"  ... and {remaining} more files");

Console.WriteLine();
Console.WriteLine($" Project generated at: {result.OutputPath}");
Console.WriteLine();
Console.WriteLine(" Next steps:");
Console.WriteLine($"   cd {result.OutputPath}");
Console.WriteLine("   dotnet restore");
Console.WriteLine("   dotnet build");
Console.WriteLine("   docker compose up -d");
Console.WriteLine("   dotnet run --project src/Api/Api.csproj");
Console.WriteLine("   open docs/wiki/");

// ── Optional: expose as API endpoints ────────────────────────────────────────
// If you want to run this as a service instead of CLI:

if (args.Contains("--serve"))
{
    Console.WriteLine();
    Console.WriteLine(" Starting API server on port 5020...");
    Console.WriteLine(" POST /generate with JSON body to trigger generation");
    Console.WriteLine();

    app.MapGet("/health", () => Results.Ok(new { status = "ok", version = "1.0.0-agentic" }));

    app.MapPost("/generate", async (GenRequest req, CancellationToken ct) =>
    {
        var sw2 = System.Diagnostics.Stopwatch.StartNew();

        var gen = AGXIOME.Core.Generator.AGXIOME.Create()
            .GenerateProject(req.Name ?? "IncidentPlatform")
            .Prompt(req.Prompt ?? "")
            .UseRegisteredAgentFactory(app.Services.GetRequiredService<IAgxiomeAgentFactory>())
            .Architecture(req.Architecture ?? "modular-monolith")
            .UseJwtAuth()
            .WithEfCore(ef => ef.WithColumnNaming(ColumnNamingConvention.SnakeCase))
            .UseAgenticCodeGen()
            .UseSandbox(3)
            .GenerateWiki()
            .OutputTo(req.OutputPath ?? outputPath);

        var res = await gen.GenerateAsync(ct);
        sw2.Stop();

        return Results.Ok(new
        {
            res.Success,
            DurationSeconds = sw2.Elapsed.TotalSeconds,
            res.Generation.Created,
            res.Generation.Modified,
            res.Validation.Passed,
            res.Validation.Failed,
            res.Validation.Blocked,
            CooperativePlan = res.CooperativePlan?.Steps.Select(step => new
            {
                step.Id,
                step.ModuleId,
                Owner = step.Owner.ToString(),
                step.TargetPath,
                step.Goal,
                step.Notes
            }),
            res.OutputPath
        });
    });

    await app.RunAsync();
}

static void ConfigureRegisteredBuilderAgents(AgxiomeDIOptions options)
{
    var apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
    const string model = "deepseek-chat";

    options.UseDeepSeek(apiKey, model)
        .AddAgent("manifest-architect", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are a software architect. Given a natural-language project description, you produce a structured ProjectManifest. Return only valid JSON matching the manifest schema.")
            .WithAutoLoops(1))
        .AddAgent("implementation-planner", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are a senior software delivery planner for a .NET 10+ delivery team. Build an execution plan that coordinates specialists across API, application/domain, infrastructure, frontend, and agents. Only use the candidate steps provided, keep the workflow end-to-end complete, minimize cross-layer leakage, and return strict JSON."))
        .AddAgent("api-specialist", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are an API specialist for ASP.NET Core minimal APIs. Own only the HTTP edge: endpoints, request/response contracts, validation flow, status codes, route naming, and API logging. Do not move business logic into the API layer. Delegate workflow semantics to application services and preserve the scaffolded startup style."))
        .AddAgent("backend-specialist", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are a backend specialist for the application and core layers. Own use-case orchestration, domain behavior, invariants, status transitions, and service coordination. Do not implement HTTP concerns, persistence wiring, or external integrations here. Strictly follow the module contract and keep layer boundaries coherent."))
        .AddAgent("infrastructure-specialist", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are an infrastructure specialist. Own persistence, DbContext usage, repository/integration wiring, and runtime seed data. Do not redefine business rules or HTTP contracts. Keep implementations consistent with the scaffold, packages, and application-layer expectations."))
        .AddAgent("frontend-specialist", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are a frontend/client specialist for .NET-centric projects. Own UI-facing adapters, client workflow composition, and presentation-friendly DTO usage only when a frontend/client module is present. Do not invent external frameworks or backend behavior. Keep generated code buildable and aligned with the API surface."))
        .AddAgent("agents-specialist", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are an AI agent systems specialist. Own agent definitions, orchestration helpers, routing metadata, and cooperative execution support. Do not drift into generic business services or HTTP endpoint logic. Keep the implementation aligned with the requested workflow and module contract."))
        .AddAgent("code-generator", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are an expert C# .NET developer used as a fallback specialist. Generate production-quality, self-contained implementation code while respecting the target layer boundary described in the task. Ensure the code composes with the deterministic scaffold."))
        .AddAgent("repair-agent", agent => agent
            .UseDeepSeek(apiKey, model)
            .WithSystemPrompt(
                "You are a C# build repair specialist. Fix compiler errors precisely without changing logic."));
}

static void PrintCooperativePlan(CooperativePlanResult? plan)
{
    if (plan?.Steps is null || plan.Steps.Count == 0)
        return;

    Console.WriteLine();
    Console.WriteLine("── Cooperative Plan ─────────────────────────────────");
    foreach (var step in plan.Steps.Take(12))
        Console.WriteLine($"  [{step.Owner,-14}] {step.ModuleId,-16} {step.Id} -> {step.TargetPath} | {step.Goal}");

    var remaining = plan.Steps.Count - 12;
    if (remaining > 0)
        Console.WriteLine($"  ... and {remaining} more planned step(s)");
}

