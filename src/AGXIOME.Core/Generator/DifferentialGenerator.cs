using System.Diagnostics;
using System.Text.Json;

namespace AGXIOME.Core.Generator;

public sealed class DifferentialGenerator
{
    private readonly ProjectManifest _manifest;
    private readonly string _outputRoot;
    private readonly ModuleDependencyPlanner _moduleDependencyPlanner;
    private readonly PackageReferencePlanner _packageReferencePlanner;

    public DifferentialGenerator(ProjectManifest manifest)
    {
        _manifest   = manifest;
        _outputRoot = Path.GetFullPath(manifest.OutputPath);
        _moduleDependencyPlanner = new ModuleDependencyPlanner(manifest.Modules);
        _moduleDependencyPlanner.ValidateAcyclic();
        _packageReferencePlanner = new PackageReferencePlanner(manifest);
    }

    public async Task<GenerationResult> GenerateAsync(CancellationToken ct = default)
    {
        var existing = AnalyzeExisting();
        var planned  = PlanGeneration(existing);
        var sw       = Stopwatch.StartNew();

        foreach (var action in planned.Where(a => a.Type != FileActionType.Skip))
            await ExecuteActionAsync(action, ct);

        sw.Stop();

        return new GenerationResult(
            ProjectName:   _manifest.ProjectName,
            TotalActions:  planned.Count,
            Created:       planned.Count(a => a.Type == FileActionType.Create),
            Modified:      planned.Count(a => a.Type == FileActionType.Modify),
            Skipped:       planned.Count(a => a.Type == FileActionType.Skip),
            Duration:      sw.Elapsed,
            Actions:       planned
        );
    }

    public ExistingProjectAnalysis AnalyzeExisting()
    {
        var root = _outputRoot;
        if (!Directory.Exists(root))
            return new ExistingProjectAnalysis(Exists: false, []);

        var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Select(f => f.Replace('\\', '/'))
            .Select(f => new
            {
                Relative = Path.GetRelativePath(root, f).Replace('\\', '/'),
                Content  = File.Exists(f) ? File.ReadAllText(f) : null
            })
            .ToList();

        return new ExistingProjectAnalysis(
            Exists:  files.Count > 0,
            Entries: files.Select(f => new FileEntry(f.Relative, f.Content ?? "")).ToList()
        );
    }

    public List<FileAction> PlanGeneration(ExistingProjectAnalysis existing)
    {
        var actions  = new List<FileAction>();
        var existingPaths = new HashSet<string>(existing.Entries.Select(e => e.RelativePath));
        var pn = _manifest.ProjectName;
        var m = _manifest;
        var primaryEntity = DerivePrimaryEntity(_manifest.ProjectName);

        // Solution file
        var slnPath = $"{pn}.slnx";
        actions.Add(existingPaths.Contains(slnPath)
            ? new FileAction(slnPath, FileActionType.Skip, "Solution exists")
            : GenSln(slnPath));

        // Directory.Build.props
        const string bp = "Directory.Build.props";
        actions.Add(existingPaths.Contains(bp)
            ? new FileAction(bp, FileActionType.Skip, "Build props exists")
            : GenBuildProps(bp));

        // ── Modules ───────────────────────────────────────────────────────────
        foreach (var mod in m.Modules)
        {
            var pp = $"src/{mod.Id}/{mod.Id}.csproj";
            actions.Add(existingPaths.Contains(pp)
                ? new FileAction(pp, FileActionType.Skip, "Project exists")
                : GenProjectFile(pp, mod));

            var progPath = $"src/{mod.Id}/Program.cs";
            if (mod.Id.Equals("Api", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(progPath))
                actions.Add(GenProgramCs(progPath, mod));

            var endpointPath = $"src/{mod.Id}/Endpoints/{primaryEntity}Endpoints.cs";
            if (mod.Id.Equals("Api", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(endpointPath))
                actions.Add(GenEndpointScaffold(endpointPath, mod, primaryEntity));

            var entityPath = $"src/{mod.Id}/Entities/{primaryEntity}.cs";
            if (mod.Id.Equals("Core", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(entityPath))
                actions.Add(GenCoreEntity(entityPath, mod, primaryEntity));

            var serviceInterfacePath = $"src/{mod.Id}/Interfaces/I{primaryEntity}Service.cs";
            if (mod.Id.Equals("Application", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(serviceInterfacePath))
                actions.Add(GenApplicationServiceInterface(serviceInterfacePath, mod, primaryEntity));

            var servicePath = $"src/{mod.Id}/Services/{primaryEntity}Service.cs";
            if (mod.Id.Equals("Application", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(servicePath))
                actions.Add(GenApplicationService(servicePath, mod, primaryEntity));

            var mediatrPath = $"src/{mod.Id}/Handlers/Create{primaryEntity}Handler.cs";
            if (mod.Id.Equals("Application", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(mediatrPath))
                actions.Add(GenMediatRHandler(mediatrPath, mod, primaryEntity));

            var technicianPath = $"src/{mod.Id}/Entities/Technician.cs";
            if (mod.Id.Equals("Core", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(technicianPath))
                actions.Add(GenTechnicianEntity(technicianPath, mod));

            var seederPath = $"src/{mod.Id}/InfrastructureSeeder.cs";
            if (mod.Id.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase) && !existingPaths.Contains(seederPath))
                actions.Add(GenInfrastructureSeeder(seederPath, mod));

            var diPath = $"src/{mod.Id}/DependencyInjection/{mod.Id}ServiceExtensions.cs";
            if (!existingPaths.Contains(diPath))
                actions.Add(GenDiExt(diPath, mod));

            if (m.IncludeTests)
            {
                var tp = $"tests/{mod.Id}.Tests/{mod.Id}.Tests.csproj";
                if (!existingPaths.Contains(tp))
                    actions.Add(GenTestProj(tp, mod));
            }
        }

        // ── Infrastructure services ───────────────────────────────────────────
        foreach (var inf in m.Infrastructure)
        {
            var cp = $"src/{pn}.Infrastructure/{inf.Name}.cs";
            if (!existingPaths.Contains(cp))
                actions.Add(GenInfraCfg(cp, inf));
        }

        // ── EF Core DbContexts + Entity Configs ───────────────────────────────
        if (m.EfCore.Enabled)
        {
            foreach (var ctx in m.EfCore.Contexts)
            {
                var ctxPath = $"src/{ctx.ModuleId}/Persistence/{ctx.ContextName}.cs";
                if (!existingPaths.Contains(ctxPath))
                    actions.Add(GenDbContext(ctxPath, ctx));

                if (ctx.UseSeparateEntityTypeConfigurations)
                {
                    var cfgDir = $"src/{ctx.ModuleId}/EntityConfigurations";
                    var cfgPath = $"{cfgDir}/{ctx.ContextName}EntityConfig.cs";
                    if (!existingPaths.Contains(cfgPath))
                        actions.Add(GenEntityTypeConfig(cfgPath, ctx));
                }
            }
        }

        // ── Auth (JWT/JWE) ────────────────────────────────────────────────────
        if (m.Auth.Enabled)
        {
            var authPath = $"src/{pn}.Api/Auth/JwtSetup.cs";
            if (!existingPaths.Contains(authPath))
                actions.Add(GenAuthSetup(authPath));
        }

        // ── FluentValidation ──────────────────────────────────────────────────
        if (m.Packages.Any(p => p.Id.Contains("FluentValidation")))
        {
            var valPath = $"src/{pn}.Api/Validation/ValidationPipeline.cs";
            if (!existingPaths.Contains(valPath))
                actions.Add(GenValidationPipeline(valPath));
        }

        // ── Serilog ───────────────────────────────────────────────────────────
        if (m.Packages.Any(p => p.Id.Contains("Serilog")))
        {
            var seriPath = $"src/{pn}.Api/Diagnostics/SerilogConfig.cs";
            if (!existingPaths.Contains(seriPath))
                actions.Add(GenSerilogSetup(seriPath));
        }

        // ── appsettings.json ──────────────────────────────────────────────────
        var asp = $"src/{pn}.Api/appsettings.json";
        if (!existingPaths.Contains(asp))
            actions.Add(GenAppSettings(asp));

        // ── Docker ────────────────────────────────────────────────────────────
        if (m.Docker.Enabled)
        {
            if (!existingPaths.Contains("Dockerfile"))
                actions.Add(GenDockerfile("Dockerfile"));

            if (m.Docker.IncludeComposeProfiles && m.Infrastructure.Count > 0
                && !existingPaths.Contains("docker-compose.yml"))
                actions.Add(GenCompose("docker-compose.yml"));
        }

        // ── K8s ───────────────────────────────────────────────────────────────
        if (m.Infrastructure.Any(i => i.Type is InfraType.PostgreSQL or InfraType.Redis or InfraType.Elasticsearch))
        {
            const string kp = "deploy/k8s/deployment.yaml";
            if (!existingPaths.Contains(kp))
                actions.Add(GenK8s(kp));
        }

        // ── OpenTelemetry ─────────────────────────────────────────────────────
        if (m.IncludeOpenTelemetry)
        {
            var op = $"src/{pn}.Api/Diagnostics/Telemetry.cs";
            if (!existingPaths.Contains(op))
                actions.Add(GenOtel(op));
        }

        // ── Serilog config ────────────────────────────────────────────────────
        if (m.Packages.Any(p => p.Id.Contains("Serilog")))
        {
            var seriProgPath = $"src/{pn}.Api/Program.cs";
            // Program.cs already handled above — skip
        }

        // ── ADR docs ──────────────────────────────────────────────────────────
        var adrDir = "docs/adr";
        var adrPath = $"{adrDir}/0001-record-architecture-decisions.md";
        if (!existingPaths.Contains(adrPath))
            actions.Add(GenAdr(adrPath));

        // ── README ────────────────────────────────────────────────────────────
        const string readmePath = "README.md";
        if (!existingPaths.Contains(readmePath))
            actions.Add(GenReadme(readmePath));

        return actions;
    }

    private static string DerivePrimaryEntity(string projectName)
    {
        var simple = projectName
            .Replace("Platform", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Management", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(simple))
            return "Entity";

        var pascalParts = System.Text.RegularExpressions.Regex.Split(
            simple, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");
        var meaningful = pascalParts.FirstOrDefault(p => p.Length > 2);
        if (meaningful is not null)
            return char.ToUpperInvariant(meaningful[0]) + meaningful[1..].ToLowerInvariant();

        return "Entity";
    }

    private async Task ExecuteActionAsync(FileAction action, CancellationToken ct)
    {
        var fullPath = Path.Combine(_outputRoot, action.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, action.Content, ct);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // FILE GENERATORS
    // ──────────────────────────────────────────────────────────────────────────

    private FileAction GenSln(string path)
    {
        var pn = _manifest.ProjectName;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(""); // start with newline (slnx)
        sb.AppendLine("<Solution>");
        foreach (var m in _manifest.Modules)
            sb.AppendLine($@"  <Project Path=""src\{m.Id}\{m.Id}.csproj"" />");
        if (_manifest.IncludeTests)
            foreach (var m in _manifest.Modules)
                sb.AppendLine($@"  <Project Path=""tests\{m.Id}.Tests\{m.Id}.Tests.csproj"" />");
        sb.AppendLine("</Solution>");
        return new FileAction(path, FileActionType.Create, "Solution file (slnx)", sb.ToString());
    }

    private FileAction GenBuildProps(string path)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<Project>");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine($"    <TargetFramework>{_manifest.Language.Version}</TargetFramework>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("</Project>");
        return new FileAction(path, FileActionType.Create, "Directory.Build.props", sb.ToString());
    }

    private FileAction GenProjectFile(string path, ModuleSpec mod)
    {
        var isApi = mod.Id.EndsWith("Api", StringComparison.OrdinalIgnoreCase);
        var sdk = isApi ? "Microsoft.NET.Sdk.Web" : "Microsoft.NET.Sdk";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<Project Sdk=\"{sdk}\">");
        sb.AppendLine();
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine($"    <TargetFramework>{_manifest.Language.Version}</TargetFramework>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine($"    <RootNamespace>{mod.Namespace}</RootNamespace>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine();
        sb.AppendLine("  <ItemGroup>");

        foreach (var package in _packageReferencePlanner.GetPackagesForModule(mod))
            sb.AppendLine($"    <PackageReference Include=\"{package.Id}\" Version=\"{package.Version}\" />");

        foreach (var dependency in _moduleDependencyPlanner.GetDirectDependencies(mod))
            sb.AppendLine($"    <ProjectReference Include=\"..\\{dependency.Id}\\{dependency.Id}.csproj\" />");

        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine();
        sb.AppendLine("</Project>");
        return new FileAction(path, FileActionType.Create, $"Project: {mod.Id}", sb.ToString());
    }

    private FileAction GenProgramCs(string path, ModuleSpec mod)
    {
        var pn = _manifest.ProjectName;
        var primaryEntity = DerivePrimaryEntity(_manifest.ProjectName);
        var hasPG  = _manifest.Infrastructure.Any(i => i.Type == InfraType.PostgreSQL);
        var hasRds = _manifest.Infrastructure.Any(i => i.Type == InfraType.Redis);
        var hasO   = _manifest.IncludeOpenTelemetry;
        var hasA   = _manifest.Agents.Count > 0;
        var hasSerilog = _manifest.Packages.Any(p => p.Id.Contains("Serilog"));
        var hasEfInMemory = _manifest.Packages.Any(p => p.Id.Contains("EntityFrameworkCore.InMemory", StringComparison.OrdinalIgnoreCase));
        var hasAuth = _manifest.Auth.Enabled;
        var svc = hasSerilog ? "CreateSerilogWebApplicationBuilder" : "WebApplication.CreateBuilder";
        var applicationModule = _manifest.Modules.FirstOrDefault(m => m.Id.Equals("Application", StringComparison.OrdinalIgnoreCase));
        var infrastructureModule = _manifest.Modules.FirstOrDefault(m => m.Id.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase));

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"using {mod.Namespace};");
        sb.AppendLine($"using {mod.Namespace}.Endpoints;");
        if (applicationModule is not null) sb.AppendLine($"using {applicationModule.Namespace}.DependencyInjection;");
        if (infrastructureModule is not null) sb.AppendLine($"using {infrastructureModule.Namespace}.DependencyInjection;");
        if (infrastructureModule is not null && _manifest.EfCore.Enabled) sb.AppendLine($"using {infrastructureModule.Namespace};");
        if (hasSerilog) sb.AppendLine("using Serilog;");
        if (hasO) { sb.AppendLine("using OpenTelemetry.Metrics;"); sb.AppendLine("using OpenTelemetry.Trace;"); sb.AppendLine("using OpenTelemetry.Resources;"); }
        if (hasPG) sb.AppendLine("using Npgsql;");
        if (hasRds) sb.AppendLine("using StackExchange.Redis;");
        if (_manifest.EfCore.Enabled) sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();

        if (hasSerilog)
        {
            sb.AppendLine("Log.Logger = new LoggerConfiguration()");
            sb.AppendLine("    .WriteTo.Console()");
            sb.AppendLine("    .CreateBootstrapLogger();");
            sb.AppendLine();
            sb.AppendLine("try {");
            sb.AppendLine($"    var builder = WebApplication.CreateBuilder(args);");
            sb.AppendLine("    builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));");
        }
        else
        {
            sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        }
        sb.AppendLine();

        // DB infra
        if (hasPG)
        {
            sb.AppendLine("// PostgreSQL");
            var pgName = _manifest.Infrastructure.First(i => i.Type == InfraType.PostgreSQL).Name;
            sb.AppendLine($"var ds = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString(\"{pgName}\")).Build();");
            sb.AppendLine("builder.Services.AddSingleton(ds);");
            sb.AppendLine();
        }
        if (hasRds)
        {
            var rdName = _manifest.Infrastructure.First(i => i.Type == InfraType.Redis).Name;
            sb.AppendLine($"// Redis");
            sb.AppendLine($"var redis = ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString(\"{rdName}\"));");
            sb.AppendLine("builder.Services.AddSingleton<IConnectionMultiplexer>(redis);");
            sb.AppendLine();
        }

        // EF Core
        if (_manifest.EfCore.Enabled)
        {
            foreach (var ctx in _manifest.EfCore.Contexts)
            {
                var connName = ctx.ConnectionStringName ?? _manifest.Infrastructure.FirstOrDefault(i => i.Type == InfraType.PostgreSQL)?.Name ?? "DefaultConnection";
                var ns = _manifest.Modules.FirstOrDefault(m2 => m2.Id == ctx.ModuleId)?.Namespace ?? pn;
                sb.AppendLine($"builder.Services.AddDbContext<{ctx.ContextName}>(o =>");
                if (hasPG)
                    sb.AppendLine($"    o.UseNpgsql(builder.Configuration.GetConnectionString(\"{connName}\")));");
                else if (hasEfInMemory || !hasPG)
                    sb.AppendLine($"    o.UseInMemoryDatabase(\"{pn}\"));");
                else
                    sb.AppendLine($"    o.UseInMemoryDatabase(\"{pn}\"));");
                sb.AppendLine();
            }
        }

        // Auth
        if (hasAuth)
        {
            sb.AppendLine("// JWT Authentication");
            sb.AppendLine("builder.Services.AddAuthentication()");
            sb.AppendLine("    .AddJwtBearer(o =>");
            sb.AppendLine("    {");
            sb.AppendLine($"        o.Authority = builder.Configuration[\"Auth:Issuer\"] ?? \"{_manifest.Auth.Issuer}\";");
            sb.AppendLine($"        o.Audience = builder.Configuration[\"Auth:Audience\"] ?? \"{_manifest.Auth.Audience}\";");
            if (hasSerilog) sb.AppendLine("        o.Events = new JwtBearerEvents { OnAuthenticationFailed = ctx => { Log.Warning(ctx.Exception, \"JWT auth failed\"); return Task.CompletedTask; } };");
            sb.AppendLine("    });");
            sb.AppendLine("builder.Services.AddAuthorization();");
            sb.AppendLine();
        }

        // Swagger
        sb.AppendLine("// Swagger / OpenAPI");
        sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
        sb.AppendLine("builder.Services.AddSwaggerGen();");
        sb.AppendLine();

        // Structured logging (always on)
        sb.AppendLine("// Structured logging");
        sb.AppendLine("builder.Logging.AddConsole();");
        sb.AppendLine();

        // FluentValidation
        if (_manifest.Packages.Any(p => p.Id.Contains("FluentValidation")))
        {
            sb.AppendLine("// FluentValidation");
            sb.AppendLine("builder.Services.AddValidatorsFromAssemblyContaining<Program>();");
            sb.AppendLine();
        }

        // Health checks (always on)
        sb.AppendLine("// Health checks");
        sb.AppendLine("builder.Services.AddHealthChecks()");
        if (_manifest.IncludeHealthChecks)
        {
            if (hasPG) sb.AppendLine("    .AddNpgSql(builder.Configuration.GetConnectionString(\"" + _manifest.Infrastructure.First(i => i.Type == InfraType.PostgreSQL).Name + "\"))");
            if (hasRds) sb.AppendLine("    .AddRedis(builder.Configuration.GetConnectionString(\"" + _manifest.Infrastructure.First(i => i.Type == InfraType.Redis).Name + "\"))");
        }
        sb.AppendLine("    ;");
        sb.AppendLine();

        // OpenTelemetry
        if (hasO)
        {
            sb.AppendLine("// OpenTelemetry");
            sb.AppendLine("builder.Services.AddOpenTelemetry()");
            sb.AppendLine($"    .ConfigureResource(r => r.AddService(\"{pn}\"))");
            sb.AppendLine("    .WithTracing(t => t");
            sb.AppendLine("        .AddAspNetCoreInstrumentation()");
            sb.AppendLine("        .AddHttpClientInstrumentation()" + (hasPG ? "" : ""));
            if (hasPG) sb.AppendLine("        .AddNpgsql()");
            sb.AppendLine("    )");
            sb.AppendLine("    .WithMetrics(m => m");
            sb.AppendLine("        .AddAspNetCoreInstrumentation()");
            sb.AppendLine("        .AddRuntimeInstrumentation());");
            sb.AppendLine();
        }

        // Agents
        if (hasA)
        {
            sb.AppendLine("// AGXIOME Agents");
            sb.AppendLine("var provider = builder.Configuration[\"Agent:Provider\"] ?? \"deepseek\";");
            sb.AppendLine("var model = builder.Configuration[\"Agent:Model\"];");
            sb.AppendLine("builder.Services.AddAgxiome()");
            sb.AppendLine("    .UseProvider(provider, model)");
            var agentGroups = _manifest.Agents.GroupBy(a => a.TreeName).ToList();
            for (var index = 0; index < agentGroups.Count; index++)
            {
                var g = agentGroups[index];
                var terminator = index == agentGroups.Count - 1 ? ";" : string.Empty;
                sb.AppendLine($"    .AddForestSwarm(\"{g.Key}\", forest =>");
                sb.AppendLine("    {");
                foreach (var a in g)
                {
                    var pr = a.SystemPrompt.Replace("\"", "\\\"");
                    var kw = string.Join(", ", a.Keywords.Select(k => $"\"{k}\""));
                    sb.AppendLine($"        forest.AddTree(\"{a.TreeName}\")");
                    sb.AppendLine($"            .AddAgent(\"{a.Id}\", \"{pr}\")");
                    sb.AppendLine($"            .WithKeywords({kw});");
                }
                sb.AppendLine($"    }}){terminator}");
            }
        }

        // ProblemDetails + Exception handling (required for .NET 10+)
        sb.AppendLine("builder.Services.AddProblemDetails();");
        if (applicationModule is not null) sb.AppendLine("builder.Services.AddApplication();");
        if (infrastructureModule is not null) sb.AppendLine("builder.Services.AddInfrastructure();");
        sb.AppendLine();
        // Middleware pipeline
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        if (_manifest.EfCore.Enabled)
        {
            sb.AppendLine();
            sb.AppendLine("using (var scope = app.Services.CreateScope())");
            sb.AppendLine("{");
            sb.AppendLine("    var seeder = scope.ServiceProvider.GetService<InfrastructureSeeder>();");
            sb.AppendLine("    if (seeder is not null)");
            sb.AppendLine("        await seeder.SeedAsync();");
            sb.AppendLine("}");
        }
        sb.AppendLine();
        sb.AppendLine("app.Logger.LogInformation(\"{Application} starting up\", \"" + pn + "\");");
        sb.AppendLine("app.UseExceptionHandler(\"/error\");");
        sb.AppendLine("app.MapHealthChecks(\"/healthz\");");
        sb.AppendLine($"app.Map{primaryEntity}Endpoints();");
        sb.AppendLine("app.UseSwagger();");
        sb.AppendLine("app.UseSwaggerUI();");
        if (hasAuth)
        {
            sb.AppendLine("app.UseAuthentication();");
            sb.AppendLine("app.UseAuthorization();");
        }
        sb.AppendLine("app.MapGet(\"/health\", () => Results.Ok(new { status = \"ok\", service = \"" + pn + "\" }));");

        if (hasA)
        {
            sb.AppendLine("app.MapPost(\"/api/incident\", async (ForestSwarm router, IncidentRequest req) =>");
            sb.AppendLine("{");
            sb.AppendLine("    var result = await router.RunAsync(req.Description);");
            sb.AppendLine("    return Results.Ok(result);");
            sb.AppendLine("});");
        }

        sb.AppendLine("app.Run();");

        if (hasSerilog)
        {
            sb.AppendLine("}");
            sb.AppendLine("catch (Exception ex) { Log.Fatal(ex, \"Application terminated unexpectedly\"); }");
            sb.AppendLine("finally { Log.CloseAndFlush(); }");
        }

        // Request models
        sb.AppendLine();
        sb.AppendLine("public record IncidentRequest(string Description, string? Source = null);");

        return new FileAction(path, FileActionType.Create, "Program.cs", sb.ToString());
    }

    private FileAction GenDbContext(string path, DbContextSpec ctx)
    {
        var ns = _manifest.Modules.FirstOrDefault(m => m.Id == ctx.ModuleId)?.Namespace ?? _manifest.ProjectName;
        var naming = _manifest.EfCore.ColumnNaming;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        var coreNs = _manifest.Modules.FirstOrDefault(m => m.Id.Equals("Core", StringComparison.OrdinalIgnoreCase))?.Namespace ?? _manifest.ProjectName;
        sb.AppendLine($"using {coreNs}.Entities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {ctx.ContextName} : DbContext");
        sb.AppendLine("{");
        sb.AppendLine($"    public {ctx.ContextName}(DbContextOptions<{ctx.ContextName}> options) : base(options) {{ }}");
        sb.AppendLine();
        sb.AppendLine($"    public DbSet<Incident> Incidents => Set<Incident>();");
        sb.AppendLine($"    public DbSet<Technician> Technicians => Set<Technician>();");
        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        if (ctx.UseSeparateEntityTypeConfigurations)
        {
            sb.AppendLine($"        modelBuilder.ApplyConfigurationsFromAssembly(typeof({ctx.ContextName}).Assembly);");
        }
        if (naming == ColumnNamingConvention.SnakeCase)
        {
            sb.AppendLine();
            sb.AppendLine("        // Snake-case naming convention for all entities");
            sb.AppendLine("        foreach (var entity in modelBuilder.Model.GetEntityTypes())");
            sb.AppendLine("        {");
            sb.AppendLine("            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));");
            sb.AppendLine("            foreach (var prop in entity.GetProperties())");
            sb.AppendLine("                prop.SetColumnName(ToSnakeCase(prop.Name));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static string ToSnakeCase(string s)");
            sb.AppendLine("    {");
            sb.AppendLine("        return string.Concat(s.Select((c, i) =>");
            sb.AppendLine("            i > 0 && char.IsUpper(c) ? \"_\" + char.ToLowerInvariant(c).ToString()");
            sb.AppendLine("            : char.ToLowerInvariant(c).ToString()));");
            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, $"DbContext: {ctx.ContextName}", sb.ToString());
    }

    private FileAction GenEntityTypeConfig(string path, DbContextSpec ctx)
    {
        var ns = _manifest.Modules.FirstOrDefault(m => m.Id == ctx.ModuleId)?.Namespace ?? _manifest.ProjectName;
        return new FileAction(path, FileActionType.Create, $"Entity config: {ctx.ContextName}",
            "using Microsoft.EntityFrameworkCore;\n" +
            "using Microsoft.EntityFrameworkCore.Metadata.Builders;\n\n" +
            $"namespace {ns}.EntityConfigurations;\n\n" +
            $"public sealed class {ctx.ContextName}EntityConfig : IEntityTypeConfiguration<YourEntity>\n" +
            "{\n" +
            "    public void Configure(EntityTypeBuilder<YourEntity> builder)\n" +
            "    {\n" +
            "        builder.ToTable(\"your_entities\");\n" +
            "        builder.HasKey(e => e.Id);\n" +
            "        // Add property configurations here\n" +
            "    }\n" +
            "}\n");
    }

    private FileAction GenAuthSetup(string path)
    {
        var pn = _manifest.ProjectName;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using Microsoft.AspNetCore.Authentication.JwtBearer;");
        sb.AppendLine("using Microsoft.IdentityModel.Tokens;");
        sb.AppendLine();
        sb.AppendLine($"namespace {pn}.Api.Auth;");
        sb.AppendLine();
        sb.AppendLine("public static class JwtSetup");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)");
        sb.AppendLine("    {");
        sb.AppendLine("        var issuer = config[\"Auth:Issuer\"] ?? \"" + _manifest.Auth.Issuer + "\";");
        sb.AppendLine("        var audience = config[\"Auth:Audience\"] ?? \"" + _manifest.Auth.Audience + "\";");
        sb.AppendLine("        var signingKey = config[\"Auth:SigningKey\"] ?? Environment.GetEnvironmentVariable(\"" + _manifest.Auth.SigningKeyEnvVar + "\") ?? \"dev-signing-key-at-least-32-chars!!\";");
        sb.AppendLine();
        sb.AppendLine("        services.AddAuthentication(o =>");
        sb.AppendLine("        {");
        sb.AppendLine("            o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;");
        sb.AppendLine("            o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;");
        sb.AppendLine("        })");
        sb.AppendLine("        .AddJwtBearer(o =>");
        sb.AppendLine("        {");
        sb.AppendLine("            o.TokenValidationParameters = new TokenValidationParameters");
        sb.AppendLine("            {");
        sb.AppendLine("                ValidateIssuer = true,");
        sb.AppendLine("                ValidIssuer = issuer,");
        sb.AppendLine("                ValidateAudience = true,");
        sb.AppendLine("                ValidAudience = audience,");
        sb.AppendLine("                ValidateIssuerSigningKey = true,");
        sb.AppendLine("                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),");
        sb.AppendLine("                ValidateLifetime = true,");
        sb.AppendLine("                ClockSkew = TimeSpan.Zero");
        sb.AppendLine("            };");
        sb.AppendLine("        });");
        sb.AppendLine();
        sb.AppendLine("        services.AddAuthorization();");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, "JWT auth setup", sb.ToString());
    }

    private FileAction GenDockerfile(string path)
    {
        var sdk = _manifest.Docker.BaseImageSdk;
        var run = _manifest.Docker.BaseImageRuntime;
        var pn = _manifest.ProjectName;
        return new FileAction(path, FileActionType.Create, "Dockerfile",
            $"FROM {sdk} AS build\nWORKDIR /app\nCOPY . .\nRUN dotnet restore\n" +
            $"RUN dotnet publish src/{pn}.Api/{pn}.Api.csproj -c Release -o out\n\n" +
            $"FROM {run} AS runtime\nWORKDIR /app\n" +
            "COPY --from=build /app/out .\nEXPOSE 8080\n" +
            "ENV ASPNETCORE_ENVIRONMENT=Production\n" +
            $"ENTRYPOINT [\"dotnet\", \"{pn}.Api.dll\"]\n");
    }

    private FileAction GenCompose(string path)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("version: '3.8'");
        sb.AppendLine("services:");
        foreach (var inf in _manifest.Infrastructure)
        {
            var img = inf.Type switch
            {
                InfraType.PostgreSQL => "postgres:16-alpine",
                InfraType.Redis => "redis:7-alpine",
                InfraType.Elasticsearch => "elasticsearch:8.12.0",
                InfraType.Kafka => "confluentinc/cp-kafka:7.6.0",
                _ => "alpine:latest"
            };
            sb.AppendLine($"  {inf.Name}:");
            sb.AppendLine($"    image: {img}");
            sb.AppendLine("    restart: unless-stopped");
            if (inf.Type == InfraType.PostgreSQL)
            {
                sb.AppendLine("    environment:");
                sb.AppendLine("      POSTGRES_USER: agxiome");
                sb.AppendLine("      POSTGRES_PASSWORD: agxiome_dev");
                sb.AppendLine("      POSTGRES_DB: incident_platform");
                sb.AppendLine("    ports:");
                sb.AppendLine("      - \"5432:5432\"");
            }
        }
        // Add API service
        sb.AppendLine($"  {_manifest.ProjectName.ToLowerInvariant()}-api:");
        sb.AppendLine("    build: .");
        sb.AppendLine("    ports:");
        sb.AppendLine("      - \"5000:8080\"");
        sb.AppendLine("    depends_on:");
        foreach (var inf in _manifest.Infrastructure)
            sb.AppendLine($"      - {inf.Name}");
        sb.AppendLine("    environment:");
        sb.AppendLine("      ASPNETCORE_ENVIRONMENT: Development");
        return new FileAction(path, FileActionType.Create, "docker-compose.yml", sb.ToString());
    }

    private FileAction GenK8s(string path)
    {
        var n = _manifest.ProjectName.ToLowerInvariant();
        return new FileAction(path, FileActionType.Create, "K8s deployment",
            $"apiVersion: apps/v1\nkind: Deployment\nmetadata:\n  name: {n}\nspec:\n  replicas: 3\n" +
            "  selector:\n    matchLabels:\n      app: " + n + "\n  template:\n    metadata:\n      labels:\n        app: " + n + "\n" +
            "    spec:\n      containers:\n      - name: api\n        image: " + n + ":latest\n        ports:\n        - containerPort: 8080\n" +
            "        env:\n        - name: ASPNETCORE_ENVIRONMENT\n          value: Production\n" +
            "        livenessProbe:\n          httpGet:\n            path: /healthz\n            port: 8080\n" +
            "        readinessProbe:\n          httpGet:\n            path: /healthz\n            port: 8080\n---\n" +
            "apiVersion: v1\nkind: Service\nmetadata:\n  name: " + n + "\nspec:\n  selector:\n    app: " + n + "\n  ports:\n  - port: 80\n    targetPort: 8080\n---\n" +
            "apiVersion: autoscaling/v2\nkind: HorizontalPodAutoscaler\nmetadata:\n  name: " + n + "\nspec:\n  scaleTargetRef:\n    apiVersion: apps/v1\n    kind: Deployment\n    name: " + n + "\n  minReplicas: 2\n  maxReplicas: 10\n  metrics:\n  - type: Resource\n    resource:\n      name: cpu\n      target:\n        type: Utilization\n        averageUtilization: 70\n");
    }

    private FileAction GenOtel(string path)
    {
        var pn = _manifest.ProjectName;
        return new FileAction(path, FileActionType.Create, "OpenTelemetry setup",
            "using OpenTelemetry;\nusing OpenTelemetry.Metrics;\nusing OpenTelemetry.Tracing;\nusing OpenTelemetry.Resources;\n\n" +
            $"namespace {pn}.Api.Diagnostics;\n\npublic static class Telemetry\n{{\n" +
            $"    public static readonly ActivitySource ActivitySource = new(\"{pn}\", \"1.0.0\");\n\n" +
            "    public static OpenTelemetryBuilder ConfigureTelemetry(this OpenTelemetryBuilder builder)\n" +
            "        => builder\n" +
            $"            .ConfigureResource(r => r.AddService(\"{pn}\"))\n" +
            "            .WithTracing(t => t\n                .AddAspNetCoreInstrumentation()\n                .AddHttpClientInstrumentation())\n" +
            "            .WithMetrics(m => m\n                .AddAspNetCoreInstrumentation()\n                .AddRuntimeInstrumentation());\n}}\n");
    }

    private FileAction GenAdr(string path)
    {
        var pn = _manifest.ProjectName;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# ADR 0001: Architecture Decision Records for {pn}");
        sb.AppendLine();
        sb.AppendLine("- **Status:** proposed");
        sb.AppendLine($"- **Date:** {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"- **Architecture:** {_manifest.Architecture}");
        sb.AppendLine($"- **Language:** {_manifest.Language.Name} {_manifest.Language.Version}");
        sb.AppendLine();
        sb.AppendLine("## Context");
        sb.AppendLine();
        sb.AppendLine("This project was scaffolded by AGXIOME Project Generator. This document records key architectural decisions.");
        sb.AppendLine();
        sb.AppendLine("## Decision");
        sb.AppendLine();
        sb.AppendLine($"- Use {_manifest.Architecture} architecture for clear separation of concerns.");
        if (_manifest.EfCore.Enabled) sb.AppendLine("- Use EF Core with separate IEntityTypeConfiguration<T> per entity.");
        if (_manifest.Auth.Enabled) sb.AppendLine($"- Use {_manifest.Auth.Scheme} authentication with role-based authorization.");
        if (_manifest.IncludeOpenTelemetry) sb.AppendLine("- Use OpenTelemetry for observability (traces + metrics).");
        sb.AppendLine();
        sb.AppendLine("## Consequences");
        sb.AppendLine();
        sb.AppendLine("- Clear modular boundaries make the system testable and maintainable.");
        sb.AppendLine("- Standardized patterns reduce cognitive load for new contributors.");
        return new FileAction(path, FileActionType.Create, "ADR 0001", sb.ToString());
    }

    private FileAction GenReadme(string path)
    {
        var pn = _manifest.ProjectName;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {pn}");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine($"This project follows the **{_manifest.Architecture}** architecture pattern.");
        sb.AppendLine();
        sb.AppendLine("## Prerequisites");
        sb.AppendLine();
        sb.AppendLine($"- .NET {_manifest.Language.Version} SDK");
        sb.AppendLine("- Docker Desktop (optional, for infrastructure)");
        sb.AppendLine("- DeepSeek API key (for agent features)");
        sb.AppendLine();
        sb.AppendLine("## Quick Start");
        sb.AppendLine();
        sb.AppendLine("`ash");
        sb.AppendLine("dotnet restore");
        sb.AppendLine("dotnet build");
        sb.AppendLine("dotnet run --project src/Api/Api.csproj");
        sb.AppendLine("`");
        sb.AppendLine();
        sb.AppendLine("## Infrastructure");
        sb.AppendLine();
        sb.AppendLine("`ash");
        sb.AppendLine("docker compose up -d");
        sb.AppendLine("`");
        sb.AppendLine();
        sb.AppendLine("## Project Structure");
        sb.AppendLine();
        sb.AppendLine("`");
        foreach (var m in _manifest.Modules)
            sb.AppendLine($"src/{m.Id}/     — {m.Description}");
        sb.AppendLine("tests/           — Unit and integration tests");
        sb.AppendLine("docs/            — Architecture and ADR documentation");
        sb.AppendLine("deploy/          — Kubernetes manifests");
        sb.AppendLine("`");
        sb.AppendLine();
        sb.AppendLine("## Guardrails");
        foreach (var g in _manifest.Guardrails)
            sb.AppendLine($"- {g.Name} ({g.Severity})");
        return new FileAction(path, FileActionType.Create, "README.md", sb.ToString());
    }

    private FileAction GenDiExt(string path, ModuleSpec mod)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        if (mod.Id.Equals("Application", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("using System.Reflection;");
        sb.AppendLine();
        sb.AppendLine($"namespace {mod.Namespace}.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"public static class {mod.Id}ServiceExtensions");
        sb.AppendLine("{");
        sb.AppendLine($"    public static IServiceCollection Add{mod.Id}(this IServiceCollection services)");
        sb.AppendLine("    {");
        if (mod.Id.Equals("Application", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("        var assembly = Assembly.GetExecutingAssembly();");
            sb.AppendLine("        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));");
            sb.AppendLine();
            sb.AppendLine("        foreach (var implementation in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith(\"Service\", StringComparison.Ordinal)))");
            sb.AppendLine("        {");
            sb.AppendLine("            var serviceInterface = implementation.GetInterfaces().FirstOrDefault(i => i.Name == $\"I{implementation.Name}\");");
            sb.AppendLine("            if (serviceInterface is not null)");
            sb.AppendLine("                services.AddScoped(serviceInterface, implementation);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
        else if (mod.Id.Equals("Infrastructure", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("        services.AddScoped<InfrastructureSeeder>();");
            sb.AppendLine();
        }
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, $"DI: {mod.Id}", sb.ToString());
    }

    private FileAction GenEndpointScaffold(string path, ModuleSpec mod, string primaryEntity)
    {
        var appNs = _manifest.Modules.FirstOrDefault(m => m.Id.Equals("Application", StringComparison.OrdinalIgnoreCase))?.Namespace;
        var serviceType = $"I{primaryEntity}Service";
        var requestType = $"{primaryEntity}CreateRequest";
        var responseType = $"{primaryEntity}Response";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using Microsoft.AspNetCore.Builder;");
        sb.AppendLine("using Microsoft.AspNetCore.Http;");
        sb.AppendLine("using Microsoft.AspNetCore.Routing;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        if (appNs is not null)
            sb.AppendLine($"using {appNs}.Interfaces;");
        sb.AppendLine();
        sb.AppendLine($"namespace {mod.Namespace}.Endpoints;");
        sb.AppendLine();
        sb.AppendLine($"public static class {primaryEntity}Endpoints");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void Map{primaryEntity}Endpoints(this IEndpointRouteBuilder app)");
        sb.AppendLine("    {");
        if (appNs is not null)
        {
            sb.AppendLine($"        app.MapGet(\"/{primaryEntity.ToLowerInvariant()}s\", async ({serviceType} service, CancellationToken cancellationToken) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var items = await service.ListAsync(cancellationToken);");
            sb.AppendLine("            return Results.Ok(items);");
            sb.AppendLine("        })");
            sb.AppendLine($"        .WithName(\"List{primaryEntity}s\");");
            sb.AppendLine();
            sb.AppendLine($"        app.MapGet(\"/{primaryEntity.ToLowerInvariant()}s/{{id:guid}}\", async (Guid id, {serviceType} service, CancellationToken cancellationToken) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var item = await service.GetByIdAsync(id, cancellationToken);");
            sb.AppendLine("            return item is null ? Results.NotFound() : Results.Ok(item);");
            sb.AppendLine("        })");
            sb.AppendLine($"        .WithName(\"Get{primaryEntity}ById\");");
            sb.AppendLine();
            sb.AppendLine($"        app.MapPost(\"/{primaryEntity.ToLowerInvariant()}s\", async ({requestType} request, {serviceType} service, ILogger<Program> logger, CancellationToken cancellationToken) =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            logger.LogInformation(\"Creating {primaryEntity} via scaffolded endpoint\");");
            sb.AppendLine("            var created = await service.CreateAsync(request, cancellationToken);");
            sb.AppendLine($"            return Results.Created(\"/{primaryEntity.ToLowerInvariant()}s/\" + created.Id, created);");
            sb.AppendLine("        })");
            sb.AppendLine($"        .WithName(\"Create{primaryEntity}\");");
            sb.AppendLine();
            sb.AppendLine($"        app.MapPost(\"/{primaryEntity.ToLowerInvariant()}s/{{id:guid}}/assign/{{technicianId:guid}}\", async (Guid id, Guid technicianId, {serviceType} service, CancellationToken cancellationToken) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var assigned = await service.AssignAsync(id, technicianId, cancellationToken);");
            sb.AppendLine("            return assigned is null ? Results.NotFound() : Results.Ok(assigned);");
            sb.AppendLine("        })");
            sb.AppendLine($"        .WithName(\"Assign{primaryEntity}\");");
            sb.AppendLine();
            sb.AppendLine($"        app.MapPut(\"/{primaryEntity.ToLowerInvariant()}s/{{id:guid}}/status/{{status}}\", async (Guid id, string status, {serviceType} service, CancellationToken cancellationToken) =>");
            sb.AppendLine("        {");
            sb.AppendLine("            var updated = await service.UpdateStatusAsync(id, status, cancellationToken);");
            sb.AppendLine("            return updated is null ? Results.NotFound() : Results.Ok(updated);");
            sb.AppendLine("        })");
            sb.AppendLine($"        .WithName(\"Update{primaryEntity}Status\");");
        }
        else
        {
            sb.AppendLine($"        app.MapGet(\"/{primaryEntity.ToLowerInvariant()}s\", () => Results.Ok(Array.Empty<object>()));");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new FileAction(path, FileActionType.Create, $"Endpoints: {primaryEntity}", sb.ToString());
    }

    private FileAction GenCoreEntity(string path, ModuleSpec mod, string primaryEntity)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace " + mod.Namespace + ".Entities;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {primaryEntity}");
        sb.AppendLine("{");
        sb.AppendLine("    public Guid Id { get; set; } = Guid.NewGuid();");
        sb.AppendLine("    public string Title { get; set; } = string.Empty;");
        sb.AppendLine("    public string Description { get; set; } = string.Empty;");
        sb.AppendLine("    public string Status { get; set; } = \"Open\";");
        sb.AppendLine("    public string Severity { get; set; } = \"Medium\";");
        sb.AppendLine("    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;");
        sb.AppendLine("    public Guid? TechnicianId { get; set; }");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, $"Entity: {primaryEntity}", sb.ToString());
    }

    private FileAction GenApplicationServiceInterface(string path, ModuleSpec mod, string primaryEntity)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace " + mod.Namespace + ".Interfaces;");
        sb.AppendLine();
        sb.AppendLine($"public interface I{primaryEntity}Service");
        sb.AppendLine("{");
        sb.AppendLine($"    Task<{primaryEntity}Response> CreateAsync({primaryEntity}CreateRequest request, CancellationToken cancellationToken = default);");
        sb.AppendLine($"    Task<{primaryEntity}Response?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);");
        sb.AppendLine($"    Task<IReadOnlyList<{primaryEntity}Response>> ListAsync(CancellationToken cancellationToken = default);");
        sb.AppendLine($"    Task<{primaryEntity}Response?> AssignAsync(Guid incidentId, Guid technicianId, CancellationToken cancellationToken = default);");
        sb.AppendLine($"    Task<{primaryEntity}Response?> UpdateStatusAsync(Guid incidentId, string status, CancellationToken cancellationToken = default);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"public sealed record {primaryEntity}CreateRequest(string Title, string Description);");
        sb.AppendLine($"public sealed record {primaryEntity}Response(Guid Id, string Title, string Description, string Status, string Severity);");
        return new FileAction(path, FileActionType.Create, $"Interface: I{primaryEntity}Service", sb.ToString());
    }

    private FileAction GenApplicationService(string path, ModuleSpec mod, string primaryEntity)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using " + mod.Namespace + ".Interfaces;");
        sb.AppendLine();
        sb.AppendLine("namespace " + mod.Namespace + ".Services;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class {primaryEntity}Service(ILogger<{primaryEntity}Service> logger) : I{primaryEntity}Service");
        sb.AppendLine("{");
        sb.AppendLine($"    public Task<{primaryEntity}Response> CreateAsync({primaryEntity}CreateRequest request, CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine($"        logger.LogInformation(\"Creating {primaryEntity}\");");
        sb.AppendLine($"        return Task.FromResult(new {primaryEntity}Response(Guid.NewGuid(), request.Title, request.Description, \"Open\", \"Medium\"));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    public Task<{primaryEntity}Response?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)");
        sb.AppendLine("        => Task.FromResult<" + primaryEntity + "Response?>(null);");
        sb.AppendLine();
        sb.AppendLine($"    public Task<IReadOnlyList<{primaryEntity}Response>> ListAsync(CancellationToken cancellationToken = default)");
        sb.AppendLine("        => Task.FromResult<IReadOnlyList<" + primaryEntity + "Response>>(Array.Empty<" + primaryEntity + "Response>());");
        sb.AppendLine();
        sb.AppendLine($"    public Task<{primaryEntity}Response?> AssignAsync(Guid incidentId, Guid technicianId, CancellationToken cancellationToken = default)");
        sb.AppendLine("        => Task.FromResult<" + primaryEntity + "Response?>(new " + primaryEntity + "Response(incidentId, \"Assigned incident\", \"Assigned to technician\", \"Assigned\", \"Medium\"));");
        sb.AppendLine();
        sb.AppendLine($"    public Task<{primaryEntity}Response?> UpdateStatusAsync(Guid incidentId, string status, CancellationToken cancellationToken = default)");
        sb.AppendLine("        => Task.FromResult<" + primaryEntity + "Response?>(new " + primaryEntity + "Response(incidentId, \"Updated incident\", \"Status updated\", status, \"Medium\"));");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, $"Service: {primaryEntity}Service", sb.ToString());
    }

    private FileAction GenTechnicianEntity(string path, ModuleSpec mod)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace " + mod.Namespace + ".Entities;");
        sb.AppendLine();
        sb.AppendLine("public sealed class Technician");
        sb.AppendLine("{");
        sb.AppendLine("    public Guid Id { get; set; } = Guid.NewGuid();");
        sb.AppendLine("    public string Name { get; set; } = string.Empty;");
        sb.AppendLine("    public string Specialization { get; set; } = string.Empty;");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, "Entity: Technician", sb.ToString());
    }

    private FileAction GenMediatRHandler(string path, ModuleSpec mod, string primaryEntity)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using MediatR;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine("using " + mod.Namespace + ".Interfaces;");
        sb.AppendLine();
        sb.AppendLine("namespace " + mod.Namespace + ".Handlers;");
        sb.AppendLine();
        sb.AppendLine($"public sealed record Create{primaryEntity}Command(string Title, string Description) : IRequest<{primaryEntity}Response>;");
        sb.AppendLine();
        sb.AppendLine($"public sealed class Create{primaryEntity}Handler(I{primaryEntity}Service service, ILogger<Create{primaryEntity}Handler> logger) : IRequestHandler<Create{primaryEntity}Command, {primaryEntity}Response>");
        sb.AppendLine("{");
        sb.AppendLine($"    public Task<{primaryEntity}Response> Handle(Create{primaryEntity}Command request, CancellationToken cancellationToken)");
        sb.AppendLine("    {");
        sb.AppendLine($"        logger.LogInformation(\"Handling Create{primaryEntity}Command\");");
        sb.AppendLine($"        return service.CreateAsync(new {primaryEntity}CreateRequest(request.Title, request.Description), cancellationToken);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, $"MediatR: Create{primaryEntity}Handler", sb.ToString());
    }

    private FileAction GenInfrastructureSeeder(string path, ModuleSpec mod)
    {
        var coreNs = _manifest.Modules.FirstOrDefault(m => m.Id.Equals("Core", StringComparison.OrdinalIgnoreCase))?.Namespace ?? _manifest.ProjectName;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine("using Microsoft.Extensions.Logging;");
        sb.AppendLine($"using {coreNs}.Entities;");
        sb.AppendLine();
        sb.AppendLine("namespace " + mod.Namespace + ";");
        sb.AppendLine();
        sb.AppendLine("public sealed class InfrastructureSeeder(AppDbContext dbContext, ILogger<InfrastructureSeeder> logger)");
        sb.AppendLine("{");
        sb.AppendLine("    public async Task SeedAsync(CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            if (await dbContext.Technicians.AnyAsync(cancellationToken))");
        sb.AppendLine("                return;");
        sb.AppendLine();
        sb.AppendLine("            logger.LogInformation(\"Seeding default technicians\");");
        sb.AppendLine("            dbContext.Technicians.AddRange(");
        sb.AppendLine("                new Technician { Name = \"Alice\", Specialization = \"Networking\" },");
        sb.AppendLine("                new Technician { Name = \"Bob\", Specialization = \"Field Support\" });");
        sb.AppendLine("            await dbContext.SaveChangesAsync(cancellationToken);");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (Exception ex)");
        sb.AppendLine("        {");
        sb.AppendLine("            logger.LogError(ex, \"Failed to seed infrastructure data\");");
        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return new FileAction(path, FileActionType.Create, "Infrastructure Seeder", sb.ToString());
    }

    private FileAction GenTestProj(string path, ModuleSpec mod)
    {
        return new FileAction(path, FileActionType.Create, $"Tests: {mod.Id}",
            $"<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>{_manifest.Language.Version}</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n    <RootNamespace>{mod.Namespace}.Tests</RootNamespace>\n    <IsPackable>false</IsPackable>\n    <IsTestProject>true</IsTestProject>\n  </PropertyGroup>\n\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.*\" />\n    <PackageReference Include=\"xunit\" Version=\"2.*\" />\n    <PackageReference Include=\"xunit.runner.visualstudio\" Version=\"2.*\" />\n    <PackageReference Include=\"Moq\" Version=\"4.*\" />\n    <ProjectReference Include=\"..\\..\\src\\{mod.Id}\\{mod.Id}.csproj\" />\n  </ItemGroup>\n\n</Project>\n");
    }

    private FileAction GenInfraCfg(string path, InfraSpec inf)
    {
        string s;
        if (inf.Type == InfraType.PostgreSQL)
        {
            s = "using Npgsql;\n\n" +
                $"namespace {_manifest.ProjectName}.Infrastructure;\n\n" +
                $"public sealed class {inf.Name}Connection : IAsyncDisposable\n{{\n" +
                "    private readonly NpgsqlDataSource _ds;\n" +
                "    public NpgsqlConnection Connection => _ds.CreateConnection();\n\n" +
                $"    public {inf.Name}Connection(NpgsqlDataSource ds) => _ds = ds;\n\n" +
                "    public async ValueTask DisposeAsync() => await _ds.DisposeAsync();\n}}\n";
        }
        else if (inf.Type == InfraType.Redis)
        {
            s = "using StackExchange.Redis;\n\n" +
                $"namespace {_manifest.ProjectName}.Infrastructure;\n\n" +
                $"public sealed class {inf.Name}Cache\n{{\n" +
                "    private readonly IConnectionMultiplexer _redis;\n" +
                "    public IDatabase Db => _redis.GetDatabase();\n\n" +
                $"    public {inf.Name}Cache(IConnectionMultiplexer redis) => _redis = redis;\n\n" +
                "    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)\n" +
                "    {\n" +
                "        var cached = await Db.StringGetAsync(key);\n" +
                "        if (cached.HasValue) return System.Text.Json.JsonSerializer.Deserialize<T>(cached!);\n" +
                "        var value = await factory();\n" +
                "        await Db.StringSetAsync(key, System.Text.Json.JsonSerializer.Serialize(value), ttl ?? TimeSpan.FromMinutes(5));\n" +
                "        return value;\n    }\n}\n";
        }
        else
            s = $"// Infrastructure config for {inf.Name} ({inf.Type})\n";
        return new FileAction(path, FileActionType.Create, $"Infra: {inf.Name}", s);
    }

    private FileAction GenAppSettings(string path)
    {
        var conns = new Dictionary<string, string?>();
        foreach (var inf in _manifest.Infrastructure)
        {
            var key = inf.Type switch
            {
                InfraType.PostgreSQL => inf.Name,
                InfraType.Redis => $"{inf.Name}:ConnectionString",
                InfraType.Elasticsearch => $"{inf.Name}:Endpoint",
                _ => inf.Name
            };
            conns[key] = inf.ConnectionString ?? $"{inf.Name.ToLowerInvariant()}_connection";
        }

        var serilogSection = _manifest.Packages.Any(p => p.Id.Contains("Serilog"))
            ? new Dictionary<string, object>
            {
                ["MinimumLevel"] = new Dictionary<string, object>
                {
                    ["Default"] = "Information",
                    ["Override"] = new Dictionary<string, string> { ["Microsoft"] = "Warning", ["Microsoft.AspNetCore"] = "Warning" }
                },
                ["WriteTo"] = new[] {
                    new { Name = "Console", Args = new { outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}" } }
                }
            }
            : null;

        var cfg = new Dictionary<string, object?>
        {
            ["Serilog"] = serilogSection,
            ["AllowedHosts"] = "*",
            ["Urls"] = "http://localhost:5000",
            ["ConnectionStrings"] = conns,
            ["Auth"] = _manifest.Auth.Enabled ? new Dictionary<string, string?>
            {
                ["Issuer"] = _manifest.Auth.Issuer,
                ["Audience"] = _manifest.Auth.Audience,
                ["TokenExpiryMinutes"] = _manifest.Auth.TokenExpiryMinutes.ToString()
            } : null,
            ["Agent"] = _manifest.Agents.Count > 0 ? new Dictionary<string, string?>
            {
                ["Provider"] = "deepseek",
                ["Model"] = "deepseek-chat"
            } : null
        };

        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        return new FileAction(path, FileActionType.Create, "appsettings.json", json);
    }

    private FileAction GenValidationPipeline(string path)
    {
        return new FileAction(path, FileActionType.Create, "FluentValidation pipeline",
            "using FluentValidation;\n" +
            "using FluentValidation.Results;\n\n" +
            $"namespace {_manifest.ProjectName}.Api.Validation;\n\n" +
            "public sealed class ValidationPipeline<T> : IEndpointFilter\n" +
            "{\n" +
            "    private readonly IValidator<T> _validator;\n" +
            "    public ValidationPipeline(IValidator<T> validator) => _validator = validator;\n\n" +
            "    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)\n" +
            "    {\n" +
            "        var arg = ctx.Arguments.OfType<T>().FirstOrDefault();\n" +
            "        if (arg is null) return await next(ctx);\n\n" +
            "        var result = await _validator.ValidateAsync(arg);\n" +
            "        if (!result.IsValid) return Results.ValidationProblem(result.ToDictionary());\n" +
            "        return await next(ctx);\n" +
            "    }\n" +
            "}\n");
    }

    private FileAction GenSerilogSetup(string path)
    {
        return new FileAction(path, FileActionType.Create, "Serilog config",
            "using Serilog;\n" +
            "using Serilog.Events;\n\n" +
            $"namespace {_manifest.ProjectName}.Api.Diagnostics;\n\n" +
            "public static class SerilogConfig\n" +
            "{\n" +
            "    public static IHostBuilder UseSerilogDiagnostics(this IHostBuilder host) =>\n" +
            "        host.UseSerilog((ctx, cfg) => cfg\n" +
            "            .ReadFrom.Configuration(ctx.Configuration)\n" +
            "            .Enrich.WithMachineName()\n" +
            "            .Enrich.WithThreadId()\n" +
            "            .WriteTo.Console(outputTemplate: \"[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}\"));\n" +
            "}\n");
    }
}

// ── Result types (must be in same file) ──────────────────────────────────────

public record ExistingProjectAnalysis(bool Exists, List<FileEntry> Entries);
public record FileEntry(string RelativePath, string Content);
public record FileAction(string RelativePath, FileActionType Type, string Description, string? Content = null);
public enum FileActionType { Create, Modify, Skip }
public record GenerationResult(string ProjectName, int TotalActions, int Created, int Modified, int Skipped, TimeSpan Duration, List<FileAction> Actions);

// ══════════════════════════════════════════════════════════════════════════════
// GENERATORS THAT MUST BE INSIDE THE CLASS (referenced by PlanGeneration)
// These were refactored into the main class body above — keep them here
// only as a safety net for the build. The real implementations are in the class.
// ══════════════════════════════════════════════════════════════════════════════

