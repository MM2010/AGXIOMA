using System.Diagnostics;
using AGXIOME.Core;
using AGXIOME.Core.DependencyInjection;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Generator;

/// <summary>
/// Entry point for the AGXIOME Project Generator.
/// 
/// Usage:
/// <code>
/// var result = await AGXIOME.Create()
///     .GenerateProject("incident-platform")
///     .Prompt("Build a distributed incident management platform...")
///     .Architecture("modular-monolith")
///     .Guardrails(g => {
///         g.RequireUnitTests();
///         g.RequireStructuredLogging();
///         g.RequireOpenTelemetry();
///         g.DenyRawSql();
///     })
///     .GenerateAsync();
/// </code>
/// </summary>
public static class AGXIOME
{
    public static ProjectGenerator Create() => new();
}

/// <summary>
/// Fluent API for specification-driven project generation.
/// </summary>
public sealed class ProjectGenerator
{
    private string? _projectName;
    private string? _prompt;
    private string _architecture = "modular-monolith";
    private string? _fileStrategy;
    private bool _useSandbox;
    private int _sandboxMaxAttempts = 5;
    private bool _generateWiki;
    private bool _useAgenticCodeGen;
    private bool _generateManifestFromPrompt;
    private string? _providerName;
    private string? _modelName;
    private readonly List<Func<GuardrailSpec, IGenGuardrail>> _customGuardrails = [];
    private readonly List<Func<ProjectManifest, Task>> _manifestCustomizers = [];
    private string _outputPath = "./";
    private string? _manifestBuildDetail;
    private string? _agenticGenerationDetail;
    private string? _sandboxDetail;
    private IAgxiomeAgentFactory? _registeredAgentFactory;

    internal ProjectGenerator() { }

    // ── Specification phase ──────────────────────────────────────────────────

    /// <summary>Name of the project to generate.</summary>
    public ProjectGenerator GenerateProject(string name)
    {
        _projectName = name;
        return this;
    }

    /// <summary>Natural language prompt describing the project.</summary>
    /// <remarks>The prompt is stored for future LLM-based template selection.</remarks>
    public ProjectGenerator Prompt(string prompt)
    {
        _prompt = prompt;
        return this;
    }

    /// <summary>Set the project architecture style.</summary>
    public ProjectGenerator Architecture(string style)
    {
        _architecture = style;
        return this;
    }

    /// <summary>Set the output path for generated files.</summary>
    public ProjectGenerator OutputTo(string path)
    {
        _outputPath = path;
        return this;
    }

    /// <summary>File generation structure strategy.</summary>
    public ProjectGenerator FileStructure(string strategy)
    {
        _fileStrategy = strategy;
        return this;
    }

    public ProjectGenerator FileStructure(FileGenerationStrategy strategy)
    {
        _fileStrategy = strategy.ToString();
        return this;
    }

    /// <summary>Add development paradigms (SOLID, DRY, KISS, TDD, etc.).</summary>
    public ProjectGenerator AddParadigms(Action<ParadigmConfig> configure)
    {
        _manifestCustomizers.Add(manifest =>
        {
            manifest.AddParadigms(configure);
            return Task.CompletedTask;
        });
        return this;
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    public ProjectGenerator UseJwtAuth(string? issuer = null, string? audience = null)
    {
        _manifestCustomizers.Add(m => { m.UseJwtAuth(issuer, audience); return Task.CompletedTask; });
        return this;
    }

    public ProjectGenerator WithAuth(Action<AuthConfig> configure)
    {
        _manifestCustomizers.Add(m => { m.WithAuth(configure); return Task.CompletedTask; });
        return this;
    }

    // ── Docker ───────────────────────────────────────────────────────────────

    public ProjectGenerator WithDocker(Action<DockerConfig> configure)
    {
        _manifestCustomizers.Add(m => { m.WithDocker(configure); return Task.CompletedTask; });
        return this;
    }

    // ── EF Core ──────────────────────────────────────────────────────────────

    public ProjectGenerator WithEfCore(Action<EfCoreConfig> configure)
    {
        _manifestCustomizers.Add(m => { m.WithEfCore(configure); return Task.CompletedTask; });
        return this;
    }

    public ProjectGenerator AddDbContext(string ctx, string mod, string? connStrName = null)
    {
        _manifestCustomizers.Add(m => { m.AddDbContext(ctx, mod, connStrName); return Task.CompletedTask; });
        return this;
    }

    /// <summary>Set column naming convention for all DB entities.</summary>
    public ProjectGenerator ColumnNames(ColumnNamingConvention c)
    {
        _manifestCustomizers.Add(m => { m.ColumnNames(c); return Task.CompletedTask; });
        return this;
    }

    // ── Sandbox / Agentic Generation ─────────────────────────────────────────

    /// <summary>Enable sandbox build-fix loop: compiles, fixes errors, retries.</summary>
    public ProjectGenerator UseSandbox(int maxAttempts = 5)
    {
        _useSandbox = true;
        _sandboxMaxAttempts = maxAttempts;
        return this;
    }

    /// <summary>Use LLM agents to generate implementation code (not just static scaffolding).</summary>
    public ProjectGenerator UseAgenticCodeGen(string? provider = null, string? model = null)
    {
        _useAgenticCodeGen = true;
        _providerName = provider;
        _modelName = model;
        return this;
    }

    /// <summary>Use host-registered named agents when the application wires them via AddAgxiome().</summary>
    public ProjectGenerator UseRegisteredAgentFactory(IAgxiomeAgentFactory agentFactory)
    {
        _registeredAgentFactory = agentFactory;
        return this;
    }

    /// <summary>Generate the full ProjectManifest from the prompt using an LLM architect agent.</summary>
    /// <remarks>When enabled, the prompt text is sent to an LLM that returns a structured JSON manifest.
    /// This replaces the need for manual .AddModule/.AddPostgres/.WithEfCore etc. calls.</remarks>
    public ProjectGenerator UseManifestFromPrompt()
    {
        _generateManifestFromPrompt = true;
        return this;
    }

    /// <summary>Generate wiki/Obsidian documentation after build.</summary>
    public ProjectGenerator GenerateWiki()
    {
        _generateWiki = true;
        return this;
    }

    public ProjectGenerator CompileUntilGreen(int maxAttempts)
    {
        _useSandbox = true;
        _sandboxMaxAttempts = maxAttempts;
        return this;
    }

    /// <summary>Add guardrail requirements via fluent configuration.</summary>
    public ProjectGenerator Guardrails(Action<GuardrailConfig> configure)
    {
        var config = new GuardrailConfig();
        configure(config);
        _manifestCustomizers.Add(manifest =>
        {
            foreach (var spec in config.Build())
                manifest.AddGuardrail(spec);
            return Task.CompletedTask;
        });
        return this;
    }

    /// <summary>Register a custom guardrail factory.</summary>
    public ProjectGenerator WithGuardrail(Func<GuardrailSpec, IGenGuardrail> factory)
    {
        _customGuardrails.Add(factory);
        return this;
    }

    /// <summary>Customize the manifest before generation.</summary>
    public ProjectGenerator WithManifestCustomizer(Func<ProjectManifest, Task> customizer)
    {
        _manifestCustomizers.Add(customizer);
        return this;
    }

    // ── Generation phase ─────────────────────────────────────────────────────

    /// <summary>Execute the full generation pipeline.</summary>
    public async Task<ProjectGeneratorResult> GenerateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_projectName))
            throw new InvalidOperationException("Project name is required. Call .GenerateProject(name) first.");

        var sw = Stopwatch.StartNew();
        var events = new List<GenEvent>();

        // 1. Build manifest
        events.Add(new GenEvent("Building manifest", "start"));
        var manifest = BuildManifest();
        EnsurePromptRequirements(manifest);
        events.Add(new GenEvent("Building manifest", "done", _manifestBuildDetail));

        // 2. Generate delta
        events.Add(new GenEvent("Generating delta", "start"));
        var generator = new DifferentialGenerator(manifest);
        var generation = await generator.GenerateAsync(ct);
        events.Add(new GenEvent("Generating delta", "done",
            $"{generation.Created} created, {generation.Modified} modified, {generation.Skipped} skipped"));

        // 3. Agentic code generation (Layer 2) — if enabled
        SandboxResult? sandboxResult = null;
        CooperativePlanResult? cooperativePlan = null;
        IReadOnlyList<CodeArtifact> agenticArtifacts = [];
        if (_useAgenticCodeGen)
        {
            events.Add(new GenEvent("Agentic code generation", "start"));
            var agentResult = await RunAgenticGenerationAsync(manifest, generation, ct);
            events.Add(new GenEvent("Agentic code generation", "done",
                _agenticGenerationDetail ?? $"{agentResult.Artifacts.Count} artifacts generated"));
            generation = agentResult.Generation;
            cooperativePlan = agentResult.Plan;
            agenticArtifacts = agentResult.Artifacts;

            if (cooperativePlan is not null)
                await WriteCooperativePlanReportAsync(cooperativePlan, agenticArtifacts, null, null, null, null, ct);

            // 4. Sandbox build-fix loop (Layer 3)
            if (_useSandbox)
            {
                events.Add(new GenEvent("Sandbox build-fix loop", "start"));
                sandboxResult = await RunSandboxAsync(manifest, ct);
                events.Add(new GenEvent("Sandbox build-fix loop", "done",
                    _sandboxDetail ?? (sandboxResult.Success
                        ? $"Build OK after {sandboxResult.Attempts} attempt(s)"
                        : $"Build FAILED after {sandboxResult.Attempts} attempt(s)")));
            }
        }

        // 5. Validate guardrails
        events.Add(new GenEvent("Validating guardrails", "start"));
        var validator = new GuardrailValidator(manifest);
        foreach (var factory in _customGuardrails)
            validator.WithGuardrail(factory);
        var validation = await validator.ValidateAsync(generation, ct);
        events.Add(new GenEvent("Validating guardrails", "done",
            $"{validation.Passed}/{validation.TotalChecks} passed, {validation.Failed} failed, {validation.Blocked} blocked"));

        // 6. Validate prompt fulfillment
        PromptFulfillmentResult? fulfillment = null;
        if (!string.IsNullOrWhiteSpace(_prompt))
        {
            events.Add(new GenEvent("Validating prompt fulfillment", "start"));
            fulfillment = await new PromptFulfillmentValidator(
                _prompt,
                manifest,
                Path.GetFullPath(_outputPath))
                .ValidateAsync(ct);
            events.Add(new GenEvent(
                "Validating prompt fulfillment",
                "done",
                $"{fulfillment.Passed}/{fulfillment.TotalChecks} checks passed, {fulfillment.Failed} failed"));
        }

        // 7. Runtime smoke test
        RuntimeSmokeResult? runtimeSmoke = null;
        if (manifest.HasModule("Api"))
        {
            events.Add(new GenEvent("Runtime smoke test", "start"));
            runtimeSmoke = await new RuntimeSmokeTester(
                Path.GetFullPath(_outputPath),
                _prompt ?? string.Empty)
                .ExecuteAsync(ct);
            events.Add(new GenEvent(
                "Runtime smoke test",
                runtimeSmoke.Success ? "done" : "failed",
                $"{runtimeSmoke.Checks.Count(c => c.Passed)}/{runtimeSmoke.Checks.Count} checks passed"));
        }

            // 8. Generate wiki (if enabled)
            if (_generateWiki)
            {
                events.Add(new GenEvent("Generating wiki", "start"));
                await GenerateWikiAsync(manifest, cooperativePlan, agenticArtifacts, validation, fulfillment, runtimeSmoke, sandboxResult, ct);
                events.Add(new GenEvent("Generating wiki", "done"));
            }

        sw.Stop();

        return new ProjectGeneratorResult(
            ProjectName:   _projectName!,
            Success:       validation.IsValid && (sandboxResult?.Success ?? true) && (fulfillment?.IsSatisfied ?? true) && (runtimeSmoke?.Success ?? true),
            Duration:      sw.Elapsed,
            Generation:    generation,
            Validation:    validation,
            Fulfillment:   fulfillment,
            RuntimeSmoke:  runtimeSmoke,
            Sandbox:       sandboxResult,
            CooperativePlan: cooperativePlan,
            Events:        events,
            OutputPath:    Path.GetFullPath(_outputPath)
        );
    }

    /// <summary>Run agentic code generation: creates GenSteps from scaffolded structure, calls LLM for each.</summary>
    private async Task<AgenticGenerationResult> RunAgenticGenerationAsync(
        ProjectManifest manifest, GenerationResult scaffold, CancellationToken ct)
    {
        var apiKey = ResolveApiKey();
        var resolved = AgxiomeProviderResolver.Resolve(_providerName ?? "deepseek", null);
        var provider = resolved.Provider;
        var model = _modelName ?? resolved.DefaultModel;

        var plannerAgent = ResolveNamedAgent("implementation-planner", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are a senior software delivery planner for a .NET 10+ delivery team. " +
                    "Build an execution plan that coordinates specialists across API, application/domain, infrastructure, frontend, and agents. " +
                    "Only use the candidate steps provided, keep the workflow end-to-end complete, minimize cross-layer leakage, and return strict JSON.");
        });

        var apiAgent = ResolveNamedAgent("api-specialist", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are an API specialist for ASP.NET Core minimal APIs. Own only the HTTP edge: endpoints, request/response contracts, validation flow, status codes, route naming, and API logging. " +
                    "Do not move business logic into the API layer. Delegate workflow semantics to application services and preserve the scaffolded startup style.");
        });

        var backendAgent = ResolveNamedAgent("backend-specialist", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are a backend specialist for the application and core layers. Own use-case orchestration, domain behavior, invariants, status transitions, and service coordination. " +
                    "Do not implement HTTP concerns, persistence wiring, or external integrations here. Strictly follow the MODULE CONTRACT: never reference namespaces, types, or packages not listed there.");
        });

        var infrastructureAgent = ResolveNamedAgent("infrastructure-specialist", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are an infrastructure specialist. Own persistence, DbContext usage, repository/integration wiring, and runtime seed data. " +
                    "Do not redefine business rules or HTTP contracts. Keep implementations consistent with the scaffold, packages, and application-layer expectations.");
        });

        var frontendAgent = ResolveNamedAgent("frontend-specialist", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are a frontend/client specialist for .NET-centric projects. Own UI-facing adapters, client workflow composition, and presentation-friendly DTO usage only when a frontend/client module is present. " +
                    "Do not invent external frameworks or backend behavior. Keep generated code buildable and aligned with the API surface.");
        });

        var agentsAgent = ResolveNamedAgent("agents-specialist", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are an AI agent systems specialist. Own agent definitions, orchestration helpers, routing metadata, and cooperative execution support. " +
                    "Do not drift into generic business services or HTTP endpoint logic. Keep the implementation aligned with the requested workflow and module contract.");
        });

        var generalAgent = ResolveNamedAgent("code-generator", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are an expert C# .NET developer used as a fallback specialist. Generate production-quality, self-contained implementation code while respecting the target layer boundary described in the task. " +
                    "Strictly follow the MODULE CONTRACT: never reference namespaces, types, or packages not listed there.");
        });

        var artifacts = new List<CodeArtifact>();
        var completedSteps = new List<string>();

        // Derive the primary entity name from the deterministic scaffold to avoid planning against conflicting file names.
        var primaryEntity = DerivePrimaryEntity(scaffold, manifest);

        // Build planners for contracts
        var packagePlanner = new PackageReferencePlanner(manifest);
        var depPlanner     = new ModuleDependencyPlanner(manifest.Modules);
        var allNamespaces  = manifest.Modules.Select(m => m.Namespace).ToList();

        // Dynamically generate steps for every module declared in the manifest
        var candidateSteps = new List<(GenStep Step, ModuleContract Contract)>();
        foreach (var mod in manifest.Modules)
        {
            var packageIds = packagePlanner
                .GetPackagesForModule(mod)
                .Select(p => p.Id)
                .ToList();

            var deps = depPlanner.GetDirectDependencies(mod);
            var refNamespaces = deps.Select(d => d.Namespace).ToList();

            var efContextNames = manifest.EfCore.Contexts
                .Where(c => c.ModuleId.Equals(mod.Id, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.ContextName)
                .ToList();

            var contract = new ModuleContract(
                ModuleId:                  mod.Id,
                ModuleNamespace:           mod.Namespace,
                TargetFramework:           manifest.Language.Version,
                AvailablePackages:         packageIds,
                ReferencedModuleNamespaces: refNamespaces,
                EfContextNames:            efContextNames,
                AllModuleNamespaces:       allNamespaces
            );

            foreach (var step in BuildStepsForModule(mod, contract, manifest, scaffold, primaryEntity))
                candidateSteps.Add((step, contract));
        }

        var steps = await CreateCollaborativePlanAsync(manifest, candidateSteps, plannerAgent.Agent, ct);

        var agents = new Dictionary<AgentSpecialization, ResolvedAgent>
        {
            [AgentSpecialization.Api] = apiAgent,
            [AgentSpecialization.Backend] = backendAgent,
            [AgentSpecialization.Infrastructure] = infrastructureAgent,
            [AgentSpecialization.Frontend] = frontendAgent,
            [AgentSpecialization.Agents] = agentsAgent,
            [AgentSpecialization.General] = generalAgent
        };

        var cooperativePlan = new CooperativePlanResult(
            steps.Select(s => new CooperativePlanStep(
                s.Step.Id,
                s.Step.TargetModule,
                s.Step.TargetPath,
                s.Step.Owner,
                s.Step.PlanGoal,
                s.Step.CollaborationNotes)).ToList());

        foreach (var (step, contract) in steps)
        {
            try
            {
                var owner = agents.TryGetValue(step.Owner, out var selectedAgent)
                    ? selectedAgent
                    : generalAgent;
                var writer = new AgenticCodeWriter(owner.Agent, manifest);
                var executionContext = new CooperativeExecutionContext(
                    CompletedSteps: completedSteps.ToArray(),
                    CompletedArtifacts: artifacts.Select(a => $"{a.FilePath} ({a.StepId})").ToArray());
                var artifact = await writer.GenerateArtifactAsync(step, contract, executionContext, ct);
                artifacts.Add(artifact);
                completedSteps.Add($"{step.Id} [{step.Owner}] -> {step.TargetPath}");

                var fullPath = Path.Combine(Path.GetFullPath(_outputPath), artifact.FilePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, artifact.Code, ct);
            }
            catch (Exception ex)
            {
                _agenticGenerationDetail = $"Agentic generation skipped after {artifacts.Count} artifact(s): provider unavailable ({ex.Message.Split('\n')[0]}).";
                DisposeResolvedAgents(plannerAgent, apiAgent, backendAgent, infrastructureAgent, frontendAgent, agentsAgent, generalAgent);
                return new AgenticGenerationResult(artifacts, scaffold, cooperativePlan);
            }
        }

        DisposeResolvedAgents(plannerAgent, apiAgent, backendAgent, infrastructureAgent, frontendAgent, agentsAgent, generalAgent);
        _agenticGenerationDetail = $"Cooperative team completed {artifacts.Count} artifact(s) across {steps.Count} planned step(s).";

        return new AgenticGenerationResult(artifacts, scaffold, cooperativePlan);
    }

    private async Task<List<(GenStep Step, ModuleContract Contract)>> CreateCollaborativePlanAsync(
        ProjectManifest manifest,
        List<(GenStep Step, ModuleContract Contract)> candidateSteps,
        IAgxiomeAgent plannerAgent,
        CancellationToken ct)
    {
        if (candidateSteps.Count == 0)
            return candidateSteps;

        var candidateIndex = candidateSteps.ToDictionary(s => s.Step.Id, StringComparer.OrdinalIgnoreCase);
                var prompt =
                        "Build a cooperative implementation plan for this generated project.\n\n" +
                        $"PROJECT: {manifest.ProjectName}\n" +
                        $"PROMPT: {_prompt}\n\n" +
                        "CANDIDATE STEPS:\n" +
                        string.Join("\n", candidateSteps.Select(s =>
                                $"- id={s.Step.Id}; module={s.Step.TargetModule}; path={s.Step.TargetPath}; owner={s.Step.Owner}; goal={s.Step.PlanGoal}")) +
                        "\n\nReturn STRICT JSON with this exact shape:\n" +
                        "{\n" +
                        "  \"steps\": [\n" +
                        "    { \"id\": \"step-id\", \"owner\": \"Api|Backend|Frontend|Infrastructure|Agents|General\", \"goal\": \"short goal\", \"notes\": \"handoff notes\" }\n" +
                        "  ]\n" +
                        "}\n\n" +
                        "RULES:\n" +
                        "- Use only step ids from the candidate list.\n" +
                        "- Keep all steps needed for end-to-end completion.\n" +
                        "- Prefer Api + Backend + Infrastructure coordination when relevant.\n" +
                        "- Include Frontend only when a frontend/client module exists.\n" +
                        "- Include Agents when the manifest contains agent modules or explicit agent requirements.\n";

        try
        {
            var response = await plannerAgent.RunAsync(new AgentRequest(prompt), ct);
            if (response.Status != AgentStatus.Completed)
                return candidateSteps;

            using var doc = System.Text.Json.JsonDocument.Parse(response.Content);
            if (!doc.RootElement.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return candidateSteps;

            var planned = new List<(GenStep Step, ModuleContract Contract)>();
            foreach (var item in stepsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idElement))
                    continue;

                var stepId = idElement.GetString();
                if (string.IsNullOrWhiteSpace(stepId) || !candidateIndex.TryGetValue(stepId, out var candidate))
                    continue;

                var owner = ParseOwner(item.TryGetProperty("owner", out var ownerElement) ? ownerElement.GetString() : null, candidate.Step.Owner);
                var goal = item.TryGetProperty("goal", out var goalElement) && !string.IsNullOrWhiteSpace(goalElement.GetString())
                    ? goalElement.GetString()!
                    : candidate.Step.PlanGoal;
                var notes = item.TryGetProperty("notes", out var notesElement) && !string.IsNullOrWhiteSpace(notesElement.GetString())
                    ? notesElement.GetString()!
                    : candidate.Step.CollaborationNotes;

                planned.Add((candidate.Step with { Owner = owner, PlanGoal = goal, CollaborationNotes = notes }, candidate.Contract));
            }

            if (planned.Count > 0)
            {
                foreach (var candidate in candidateSteps)
                {
                    if (!planned.Any(p => p.Step.Id.Equals(candidate.Step.Id, StringComparison.OrdinalIgnoreCase)))
                        planned.Add(candidate);
                }

                return planned;
            }
        }
        catch
        {
            // Fall back to deterministic plan order.
        }

        return candidateSteps;
    }

    private static AgentSpecialization ParseOwner(string? owner, AgentSpecialization fallback)
    {
        return Enum.TryParse<AgentSpecialization>(owner, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    /// <summary>Run sandbox build-fix loop.</summary>
    private async Task<SandboxResult> RunSandboxAsync(ProjectManifest manifest, CancellationToken ct)
    {
        var apiKey = ResolveApiKey();
        var resolved = AgxiomeProviderResolver.Resolve(_providerName ?? "deepseek", null);
        var provider = resolved.Provider;
        var model = _modelName ?? resolved.DefaultModel;

        var repairAgent = ResolveNamedAgent("repair-agent", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt("You are a C# build repair specialist. Fix compiler errors precisely without changing logic.");
        });

        var sandbox = new SandboxBuildFixLoop(
            repairAgent.Agent,
            Path.GetFullPath(_outputPath),
            GetCooperativePlanPath(),
            _sandboxMaxAttempts);

        var result = await sandbox.ExecuteAsync(ct);
        DisposeResolvedAgents(repairAgent);
        _sandboxDetail = result.Message;
        return result;
    }

    private string? ResolveApiKey()
    {
        return Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
               ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    // ── Agentic step helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Derive a PascalCase entity name from the project name.
    /// "incident-platform" → "Incident"; "IncidentManagementPlatform" → "Incident".
    /// Falls back to "Entity" when nothing useful is found.
    /// </summary>
    private static string DerivePrimaryEntity(GenerationResult scaffold, ProjectManifest manifest)
    {
        var scaffoldEntityPath = scaffold.Actions
            .Where(a => a.Type != FileActionType.Skip)
            .Select(a => a.RelativePath)
            .FirstOrDefault(path =>
                path.StartsWith("src/Core/Entities/", StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                && !Path.GetFileName(path).Equals("Technician.cs", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(scaffoldEntityPath))
            return Path.GetFileNameWithoutExtension(scaffoldEntityPath);

        return DerivePrimaryEntity(manifest);
    }

    private static string DerivePrimaryEntity(ProjectManifest manifest)
    {
        // Prefer the first EF context name stripped of "DbContext"/"Context"
        var firstCtx = manifest.EfCore.Contexts.FirstOrDefault();
        if (firstCtx is not null)
        {
            var name = firstCtx.ContextName
                .Replace("DbContext", "", StringComparison.OrdinalIgnoreCase)
                .Replace("Context", "", StringComparison.OrdinalIgnoreCase)
                .Trim();
            if (name.Length > 0) return name;
        }

        var projectName = manifest.ProjectName;

        // Split on delimiters first (kebab-case, snake_case, dot, space)
        var parts = System.Text.RegularExpressions.Regex.Split(
            projectName, @"[\-_\.\s]+");
        if (parts.Length > 1)
        {
            var first = parts.FirstOrDefault(p => p.Length > 2);
            if (first is not null)
                return char.ToUpperInvariant(first[0]) + first[1..].ToLowerInvariant();
        }

        // PascalCase split: "IncidentManagementPlatform" → ["Incident", "Management", "Platform"]
        var pascalParts = System.Text.RegularExpressions.Regex.Split(
            projectName, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])");
        var meaningful = pascalParts.FirstOrDefault(p => p.Length > 2);
        if (meaningful is not null)
            return char.ToUpperInvariant(meaningful[0]) + meaningful[1..].ToLowerInvariant();

        return "Entity";
    }

    /// <summary>
    /// Build agentic generation steps for one module based on its architectural role.
    /// Steps are minimal — one file per concern — to reduce cross-file dependency risk.
    /// </summary>
    private static IEnumerable<GenStep> BuildStepsForModule(
        ModuleSpec mod,
        ModuleContract contract,
        ProjectManifest manifest,
        GenerationResult scaffold,
        string primaryEntity)
    {
        var normalized = mod.Id.Trim().ToLowerInvariant();

        // ── API layer ──────────────────────────────────────────────────────────
        if (normalized is "api" or "webapi" || normalized.EndsWith(".api"))
        {
            var endpointPath = ResolveScaffoldPath(scaffold, mod.Id, $"Endpoints/{primaryEntity}Endpoints.cs",
                path => path.Contains("/Endpoints/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("Endpoints.cs", StringComparison.OrdinalIgnoreCase));
            yield return new GenStep(
                $"api-{primaryEntity.ToLowerInvariant()}-workflow",
                mod.Id,
                endpointPath,
                $"Refine the HTTP workflow for {primaryEntity}: list, get by id, create, assign, and status update endpoints with consistent request handling, result mapping, and logging.",
                Owner: AgentSpecialization.Api,
                PlanGoal: $"Complete the public API flow for {primaryEntity}.",
                CollaborationNotes: "Coordinate route contracts with the backend service and preserve the existing minimal API entrypoints.");
            yield break;
        }

        // ── Application / Service layer ───────────────────────────────────────
        if (normalized is "application" || normalized.EndsWith(".application"))
        {
            var servicePath = ResolveScaffoldPath(scaffold, mod.Id, $"Services/{primaryEntity}Service.cs",
                path => path.Contains("/Services/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("Service.cs", StringComparison.OrdinalIgnoreCase));
            var handlerPath = ResolveScaffoldPath(scaffold, mod.Id, $"Handlers/Create{primaryEntity}Handler.cs",
                path => path.Contains("/Handlers/", StringComparison.OrdinalIgnoreCase) && path.EndsWith("Handler.cs", StringComparison.OrdinalIgnoreCase));
            yield return new GenStep(
                $"application-{primaryEntity.ToLowerInvariant()}-service",
                mod.Id,
                servicePath,
                $"Implement the application service for {primaryEntity} so the end-to-end workflow is coherent across create, list, assignment, and status changes.",
                Owner: AgentSpecialization.Backend,
                PlanGoal: $"Implement the main application workflow for {primaryEntity}.",
                CollaborationNotes: "Keep service contracts aligned with Api endpoints and Infrastructure seeding/data conventions.");
            yield return new GenStep(
                $"application-{primaryEntity.ToLowerInvariant()}-handler",
                mod.Id,
                handlerPath,
                $"Implement the MediatR command handler for {primaryEntity} creation with explicit logging and service coordination.",
                Owner: AgentSpecialization.Backend,
                PlanGoal: $"Keep command handling aligned with the main backend workflow.",
                CollaborationNotes: "Assume the service implementation is the source of business behavior and keep handler logic thin.");
            yield break;
        }

        // ── Domain layer ──────────────────────────────────────────────────────
        if (normalized is "domain" or "core" || normalized.EndsWith(".domain") || normalized.EndsWith(".core"))
        {
            var entityPath = ResolveScaffoldPath(scaffold, mod.Id, $"Entities/{primaryEntity}.cs",
                path => path.Contains("/Entities/", StringComparison.OrdinalIgnoreCase)
                    && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    && !path.EndsWith("Technician.cs", StringComparison.OrdinalIgnoreCase));
            yield return new GenStep(
                $"core-{primaryEntity.ToLowerInvariant()}-entity",
                mod.Id,
                entityPath,
                $"Refine the {primaryEntity} entity shape so it clearly supports the requested workflow, status lifecycle, and assignment semantics without breaking existing contracts.",
                Owner: AgentSpecialization.Backend,
                PlanGoal: $"Keep the core entity aligned with the planned workflow.",
                CollaborationNotes: "Preserve compatibility with the existing application DTOs and Infrastructure DbContext.");
            yield break;
        }

        // ── Infrastructure layer ───────────────────────────────────────────────
        if (normalized is "infrastructure" || normalized.EndsWith(".infrastructure"))
        {
            if (manifest.EfCore.Enabled)
            {
                var seederPath = ResolveScaffoldPath(scaffold, mod.Id, "InfrastructureSeeder.cs",
                    path => path.EndsWith("InfrastructureSeeder.cs", StringComparison.OrdinalIgnoreCase));
                yield return new GenStep(
                    $"infrastructure-{primaryEntity.ToLowerInvariant()}-seeder",
                    mod.Id,
                    seederPath,
                    $"Implement infrastructure seeding that prepares realistic data needed to exercise the {primaryEntity} workflow at runtime.",
                    Owner: AgentSpecialization.Infrastructure,
                    PlanGoal: $"Seed infrastructure data required by the end-to-end flow.",
                    CollaborationNotes: "Keep seed data consistent with Application service expectations and DbContext shape.");
            }
            yield break;
        }

        if (normalized.Contains("frontend") || normalized.Contains("ui") || normalized.Contains("client") || normalized.EndsWith(".web"))
        {
            yield return new GenStep(
                $"frontend-{primaryEntity.ToLowerInvariant()}-workflow",
                mod.Id,
                $"src/{mod.Id}/Generated/{primaryEntity}WorkflowClient.cs",
                $"Implement a thin client-side workflow adapter for {primaryEntity} that can call the generated API and represent the main user flow coherently.",
                Owner: AgentSpecialization.Frontend,
                PlanGoal: $"Represent the end-user flow on the frontend/client side.",
                CollaborationNotes: "Match the route shapes emitted by the Api specialist and do not invent external frameworks.");
            yield break;
        }

        // ── Agents layer ───────────────────────────────────────────────────────
        if (normalized is "agents" || normalized.EndsWith(".agents"))
        {
            foreach (var agent in manifest.Agents)
            {
                var className = ToPascalCase(agent.Id) + "Agent";
                yield return new GenStep($"agent-{agent.Id}", mod.Id,
                    $"src/{mod.Id}/{className}.cs",
                    $"Generate a simple {className} POCO class with: " +
                    $"string Id = \"{agent.Id}\", string SystemPrompt = \"{agent.SystemPrompt[..Math.Min(80, agent.SystemPrompt.Length)]}...\", " +
                    $"string[] Keywords = [{string.Join(", ", agent.Keywords.Take(3).Select(k => $"\"{k}\""))}]. " +
                    $"No external dependencies beyond System.",
                    Owner: AgentSpecialization.Agents,
                    PlanGoal: $"Implement cooperative agent definition {agent.Id}.",
                    CollaborationNotes: "Keep agent metadata aligned with the planner and the other specialists' workflow.");
            }
            yield break;
        }

        // ── All other roles: skip agentic generation ───────────────────────────
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var parts = System.Text.RegularExpressions.Regex.Split(s.Trim(), @"[\-_\.\s]+");
        return string.Concat(parts.Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static string ResolveScaffoldPath(GenerationResult scaffold, string moduleId, string fallbackSuffix, Func<string, bool> predicate)
    {
        var modulePrefix = $"src/{moduleId}/";
        var scaffoldPath = scaffold.Actions
            .Where(a => a.Type != FileActionType.Skip)
            .Select(a => a.RelativePath)
            .FirstOrDefault(path => path.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase) && predicate(path));

        return scaffoldPath ?? $"src/{moduleId}/{fallbackSuffix}";
    }

    private ResolvedAgent ResolveNamedAgent(string agentName, Action<AgxiomeBuilder.AgentBuilder> configure)
    {
        if (_registeredAgentFactory is not null)
        {
            try
            {
                return new ResolvedAgent(_registeredAgentFactory.GetAgent(agentName), false);
            }
            catch (InvalidOperationException)
            {
                // Fall back to local construction if the host app did not register this specific agent.
            }
        }

        var builder = AgxiomeBuilder.Agent(agentName);
        configure(builder);
        return new ResolvedAgent(builder.Build(), true);
    }

    private static void DisposeResolvedAgents(params ResolvedAgent[] agents)
    {
        foreach (var agent in agents)
        {
            if (agent.Owned)
                agent.Agent.Dispose();
        }
    }

    private async Task GenerateWikiAsync(
        ProjectManifest manifest,
        CooperativePlanResult? cooperativePlan,
        IReadOnlyList<CodeArtifact> artifacts,
        ValidationResult validation,
        PromptFulfillmentResult? fulfillment,
        RuntimeSmokeResult? runtimeSmoke,
        SandboxResult? sandbox,
        CancellationToken ct)
    {
        var output = Path.GetFullPath(_outputPath);
        var wikiDir = Path.Combine(output, "docs", "wiki");

        // Architecture overview
        var archMd = $"# {manifest.ProjectName} Architecture\n\n" +
            $"## Overview\n\n" +
            $"- **Architecture:** {manifest.Architecture}\n" +
            $"- **Language:** {manifest.Language.Name} {manifest.Language.Version}\n" +
            $"- **Auth:** {(manifest.Auth.Enabled ? manifest.Auth.Scheme.ToString() : "None")}\n" +
            $"- **EF Core:** {(manifest.EfCore.Enabled ? "Yes" : "No")}\n" +
            $"- **OpenTelemetry:** {(manifest.IncludeOpenTelemetry ? "Yes" : "No")}\n\n" +
            $"## Modules\n\n";
        foreach (var m in manifest.Modules)
            archMd += $"- **{m.Id}** — {m.Description}\n";

        Directory.CreateDirectory(wikiDir);
        await File.WriteAllTextAsync(Path.Combine(wikiDir, "architecture.md"), archMd, ct);

        // ADR index
        var adrMd = "# Architecture Decision Records\n\n" +
            "| ADR | Title | Status |\n|-----|-------|--------|\n" +
            "| 0001 | Initial architecture decisions | Proposed |\n";
        await File.WriteAllTextAsync(Path.Combine(wikiDir, "adr-index.md"), adrMd, ct);

        // Paradigm reference
        if (manifest.Paradigms.Count > 0)
        {
            var parMd = "# Development Paradigms\n\n";
            foreach (var p in manifest.Paradigms)
                parMd += $"## {p.Name}\n{p.Description}\n\n";
            await File.WriteAllTextAsync(Path.Combine(wikiDir, "paradigms.md"), parMd, ct);
        }

        if (cooperativePlan is not null)
            await WriteCooperativePlanReportAsync(cooperativePlan, artifacts, validation, fulfillment, runtimeSmoke, sandbox, ct);
    }

    private string GetCooperativePlanPath()
        => Path.Combine(Path.GetFullPath(_outputPath), "docs", "wiki", "cooperative-plan.md");

    private async Task WriteCooperativePlanReportAsync(
        CooperativePlanResult cooperativePlan,
        IReadOnlyList<CodeArtifact> artifacts,
        ValidationResult? validation,
        PromptFulfillmentResult? fulfillment,
        RuntimeSmokeResult? runtimeSmoke,
        SandboxResult? sandbox,
        CancellationToken ct)
    {
        var wikiDir = Path.GetDirectoryName(GetCooperativePlanPath())!;
        Directory.CreateDirectory(wikiDir);

        var lifecycleMd = $"# Cooperative Plan\n\n" +
            $"## Lifecycle Summary\n\n" +
            $"- Planning steps: {cooperativePlan.Steps.Count}\n" +
            $"- Agentic artifacts: {artifacts.Count}\n" +
            $"- Guardrails: {(validation is null ? "not executed" : $"{validation.Passed}/{validation.TotalChecks} passed")}\n" +
            $"- Prompt fulfillment: {(fulfillment is null ? "not executed" : $"{fulfillment.Passed}/{fulfillment.TotalChecks} passed")}\n" +
            $"- Runtime smoke: {(runtimeSmoke is null ? "not executed" : runtimeSmoke.Success ? "passed" : "failed")}\n" +
            $"- Sandbox build-fix: {(sandbox is null ? "not executed" : sandbox.Success ? $"passed after {sandbox.Attempts} attempt(s)" : $"failed after {sandbox.Attempts} attempt(s)")}\n\n" +
            "## Planned Steps\n\n";

        foreach (var step in cooperativePlan.Steps)
        {
            lifecycleMd +=
                $"### {step.Id}\n\n" +
                $"- Owner: {step.Owner}\n" +
                $"- Module: {step.ModuleId}\n" +
                $"- Target: `{step.TargetPath}`\n" +
                $"- Goal: {step.Goal}\n" +
                $"- Notes: {step.Notes}\n\n";
        }

        if (artifacts.Count > 0)
        {
            lifecycleMd += "## Generated Artifacts\n\n";
            foreach (var artifact in artifacts)
                lifecycleMd += $"- `{artifact.FilePath}` from step `{artifact.StepId}`\n";
            lifecycleMd += "\n";
        }

        if (runtimeSmoke is not null)
        {
            lifecycleMd += "## Runtime Smoke Checks\n\n";
            foreach (var check in runtimeSmoke.Checks)
                lifecycleMd += $"- {check.Id}: {(check.Passed ? "OK" : "FAIL")} - {check.Evidence}\n";
            lifecycleMd += "\n";
        }

        await File.WriteAllTextAsync(GetCooperativePlanPath(), lifecycleMd, ct);
    }

    private ProjectManifest BuildManifest()
    {
        // ── If prompt-based manifest generation is enabled, delegate to LLM ──
        if (_generateManifestFromPrompt && !string.IsNullOrWhiteSpace(_prompt))
        {
            try
            {
                var promptManifest = GenerateManifestFromPromptAsync(_prompt).GetAwaiter().GetResult();
                _manifestBuildDetail = "LLM manifest generated successfully.";
                // Apply code-configured customizers (guardrails, etc.) on top of the LLM-generated manifest
                foreach (var customizer in _manifestCustomizers)
                    customizer(promptManifest).GetAwaiter().GetResult();
                return promptManifest;
            }
            catch (Exception ex)
            {
                var fallbackManifest = BuildHeuristicManifestFromPrompt(_prompt);
                _manifestBuildDetail = $"LLM manifest failed, using heuristic fallback: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
                foreach (var customizer in _manifestCustomizers)
                    customizer(fallbackManifest).GetAwaiter().GetResult();
                return fallbackManifest;
            }
        }

        var architecture = (_architecture?.ToLowerInvariant()) switch
        {
            "modular-monolith" or null => ArchitectureStyle.ModularMonolith,
            "monolith" or "single"     => ArchitectureStyle.Monolith,
            "clean-architecture"       => ArchitectureStyle.CleanArchitecture,
            "vertical-slices"          => ArchitectureStyle.VerticalSlices,
            "microservices"            => ArchitectureStyle.Microservices,
            _                          => ArchitectureStyle.ModularMonolith
        };

        var manifest = new ProjectManifest(_projectName!)
            .WithOutputPath(_outputPath)
            .WithArchitecture(architecture);

        var fStrategy = (_fileStrategy?.ToLowerInvariant()) switch
        {
            "single-project" or "single"    => FileGenerationStrategy.SingleProject,
            "per-feature" or "vertical"     => FileGenerationStrategy.PerFeature,
            "hexagonal" or "ports-adapters" => FileGenerationStrategy.Hexagonal,
            _                                => FileGenerationStrategy.PerModule
        };
        manifest.FileStructure(fStrategy);

        if (architecture == ArchitectureStyle.ModularMonolith || architecture == ArchitectureStyle.Monolith)
        {
            manifest
                .AddModule("Api",        $"{_projectName}.Api",        "ASP.NET Core Web API entry point")
                .AddModule("Core",       $"{_projectName}.Core",       "Domain logic and business rules")
                .AddModule("Infrastructure", $"{_projectName}.Infrastructure", "Data access and external services");
        }

        foreach (var customizer in _manifestCustomizers)
            customizer(manifest).GetAwaiter().GetResult();

        _manifestBuildDetail = "Deterministic manifest built from fluent configuration.";

        return manifest;
    }

    private void EnsurePromptRequirements(ProjectManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(_prompt))
            return;

        var text = _prompt.ToLowerInvariant();
        var guardrailNames = manifest.Guardrails
            .Select(g => g.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        EnsureModule(manifest, "Core", $"{manifest.ProjectName}.Core", "Domain entities and interfaces");
        EnsureModule(manifest, "Application", $"{manifest.ProjectName}.Application", "Application services and handlers");
        EnsureModule(manifest, "Infrastructure", $"{manifest.ProjectName}.Infrastructure", "Persistence and integrations");
        EnsureModule(manifest, "Api", $"{manifest.ProjectName}.Api", "HTTP API entry point");

        if (guardrailNames.Contains("require-unit-tests") || guardrailNames.Contains("require-tdd"))
            manifest.WithTests();

        if (guardrailNames.Contains("require-opentelemetry"))
            manifest.WithOpenTelemetry();

        if (guardrailNames.Contains("require-health-checks"))
            manifest.WithHealthChecks();

        if (Mentions(text, "entity framework", "ef core", "in-memory", "in memory"))
        {
            var efPackageVersion = ResolveFrameworkMajorWildcard(manifest.Language.Version);
            manifest.WithEfCore(cfg =>
            {
                cfg.Enabled = true;
                cfg.UseSeparateEntityTypeConfigurations = false;
                cfg.WithColumnNaming(ColumnNamingConvention.SnakeCase);
            });

            if (!manifest.EfCore.Contexts.Any(c => c.ContextName.Equals("AppDbContext", StringComparison.OrdinalIgnoreCase)))
                manifest.AddDbContext("AppDbContext", "Infrastructure", "DefaultConnection", useSeparateConfig: false);

            EnsurePackage(manifest, "Microsoft.EntityFrameworkCore", efPackageVersion, "Infrastructure");
            EnsurePackage(manifest, "Microsoft.EntityFrameworkCore.InMemory", efPackageVersion, "Infrastructure");
        }

        if (Mentions(text, "mediatr", "cqrs", "command/query"))
            EnsurePackage(manifest, "MediatR", "12.*", "Application");
    }

    private static void EnsureModule(ProjectManifest manifest, string id, string rootNamespace, string description)
    {
        if (!manifest.Modules.Any(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            manifest.AddModule(id, rootNamespace, description);
    }

    private static void EnsurePackage(ProjectManifest manifest, string packageId, string version, string targetModule)
    {
        manifest.Packages.RemoveAll(p =>
            p.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(p.TargetModule, targetModule, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(p.TargetModule)));

        manifest.AddPackage(packageId, version, targetModule);
    }

    private static string ResolveFrameworkMajorWildcard(string targetFramework)
    {
        if (string.IsNullOrWhiteSpace(targetFramework))
            return "10.*";

        var match = System.Text.RegularExpressions.Regex.Match(targetFramework, @"net(?<major>\d+)");
        var major = match.Success ? match.Groups["major"].Value : "10";

        return $"{major}.*";
    }

    private static bool Mentions(string prompt, params string[] patterns) => patterns.Any(prompt.Contains);

    private ProjectManifest BuildHeuristicManifestFromPrompt(string prompt)
    {
        var text = prompt.ToLowerInvariant();
        var architecture = text.Contains("clean architecture")
            ? ArchitectureStyle.CleanArchitecture
            : text.Contains("vertical slice")
                ? ArchitectureStyle.VerticalSlices
                : text.Contains("microservice")
                    ? ArchitectureStyle.Microservices
                    : ArchitectureStyle.ModularMonolith;

        var manifest = new ProjectManifest(_projectName!)
            .WithOutputPath(_outputPath)
            .WithArchitecture(architecture);

        manifest.FileStructure(FileGenerationStrategy.PerModule);

        manifest.Modules.Clear();
        manifest
            .AddModule("Api", $"{_projectName}.Api", "ASP.NET Core Web API entry point")
            .AddModule("Core", $"{_projectName}.Core", "Domain entities and interfaces")
            .AddModule("Infrastructure", $"{_projectName}.Infrastructure", "Data access and external integrations");

        if (architecture == ArchitectureStyle.CleanArchitecture
            || text.Contains("application layer")
            || text.Contains("mediatr")
            || text.Contains("cqrs")
            || text.Contains("command/query"))
        {
            manifest.AddModule("Application", $"{_projectName}.Application", "Application services, DTOs, commands and queries");
        }

        if (text.Contains("entity framework") || text.Contains("ef core") || text.Contains("dbcontext"))
        {
            manifest.WithEfCore(cfg =>
            {
                cfg.Enabled = true;
                cfg.UseSeparateEntityTypeConfigurations = true;
                cfg.WithColumnNaming(ColumnNamingConvention.SnakeCase);
            });

            manifest.AddDbContext("AppDbContext", "Infrastructure", "DefaultConnection");
        }

        if (text.Contains("in-memory") || text.Contains("in memory"))
        {
            manifest.AddPackage("Microsoft.EntityFrameworkCore.InMemory", "8.*", "Infrastructure");
        }

        if (text.Contains("mediatr") || text.Contains("cqrs") || text.Contains("command/query"))
        {
            manifest.AddPackage("MediatR", "12.*", "Application");
        }

        if (text.Contains("solid")) manifest.AddParadigm(Paradigms.SOLID);
        if (text.Contains("dry")) manifest.AddParadigm(Paradigms.DRY);
        if (text.Contains("kiss")) manifest.AddParadigm(Paradigms.KISS);
        if (text.Contains("tdd") || text.Contains("test-driven")) manifest.AddParadigm(Paradigms.TDD);

        if (text.Contains("jwt") || text.Contains("auth") || text.Contains("authentication"))
            manifest.UseJwtAuth();

        if (text.Contains("agxioma") || text.Contains("agent") || text.Contains("swarm"))
        {
            manifest.AddAgent(
                "router",
                "routing",
                "Route incoming tasks to the correct AGXIOMA capability and return structured results.",
                ["route", "agent", "task", "workflow", "support"]);
        }

        return manifest;
    }

    /// <summary>Use LLM to generate a full ProjectManifest from the prompt.</summary>
    private async Task<ProjectManifest> GenerateManifestFromPromptAsync(string prompt)
    {
        var apiKey = ResolveApiKey();
        var resolved = AgxiomeProviderResolver.Resolve(_providerName ?? "deepseek", null);
        var provider = resolved.Provider;
        var model = _modelName ?? resolved.DefaultModel;

        var manifestAgent = ResolveNamedAgent("manifest-architect", agent =>
        {
            agent.UseModel(provider, model, apiKey)
                .WithSystemPrompt(
                    "You are a software architect. Given a natural-language project description, " +
                    "you produce a structured ProjectManifest. Return ONLY valid JSON matching the manifest schema, " +
                    "no markdown, no explanation.")
                .WithAutoLoops(upperBound: 1);
        });

        var schema = """
{
  "projectName": "string",
  "architecture": "ModularMonolith|CleanArchitecture|VerticalSlices|Microservices",
  "language": { "name": "csharp", "version": "net10.0" },
  "fileStrategy": "PerModule|SingleProject|PerFeature|Hexagonal",
  "modules": [{ "id": "Api", "namespace": "Project.Api", "description": "..." }],
  "infrastructure": [{ "name": "IncidentDb", "type": "PostgreSQL", "connectionString": "..." }],
  "auth": { "enabled": true, "scheme": "Jwt", "issuer": "...", "audience": "..." },
  "efCore": { "enabled": true, "useSeparateEntityTypeConfigurations": true, "columnNaming": "SnakeCase" },
  "includeOpenTelemetry": true,
  "includeHealthChecks": true,
  "includeTests": true,
  "packages": [{ "id": "Serilog.AspNetCore", "version": "9.*" }],
  "agents": [{ "id": "router", "treeName": "routing", "systemPrompt": "...", "keywords": ["..."] }],
  "paradigms": ["SOLID", "DRY", "KISS", "TDD"],
  "guardrails": ["require-unit-tests", "require-structured-logging"]
}
""";

        var response = await manifestAgent.Agent.RunAsync(
            new AgentRequest(
                $"Generate a ProjectManifest JSON for this project:\n\n{prompt}\n\n" +
                $"Use this JSON schema:\n{schema}\n\n" +
                "Return ONLY valid JSON. No markdown, no explanation, no code fences."),
            CancellationToken.None);

        DisposeResolvedAgents(manifestAgent);

        // Parse JSON response into ProjectManifest
        var json = response.Content.Trim();
        // Strip markdown code fences if present
        if (json.StartsWith("```")) json = string.Join("\n", json.Split('\n').Skip(1).TakeWhile(l => !l.StartsWith("```"))).Trim();
        if (json.StartsWith("```json")) json = json[7..].Trim();
        if (json.EndsWith("```")) json = json[..^3].Trim();

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var name = root.TryGetProperty("projectName", out var pn) ? pn.GetString() ?? _projectName! : _projectName!;

            var manifest = new ProjectManifest(name)
                .WithOutputPath(_outputPath);

            // Architecture
            if (root.TryGetProperty("architecture", out var arch))
            {
                manifest.WithArchitecture(arch.GetString() switch
                {
                    "CleanArchitecture" => ArchitectureStyle.CleanArchitecture,
                    "VerticalSlices" => ArchitectureStyle.VerticalSlices,
                    "Microservices" => ArchitectureStyle.Microservices,
                    _ => ArchitectureStyle.ModularMonolith
                });
            }

            // File strategy
            if (root.TryGetProperty("fileStrategy", out var fs))
            {
                manifest.FileStructure(fs.GetString() switch
                {
                    "SingleProject" => FileGenerationStrategy.SingleProject,
                    "PerFeature" => FileGenerationStrategy.PerFeature,
                    "Hexagonal" => FileGenerationStrategy.Hexagonal,
                    _ => FileGenerationStrategy.PerModule
                });
            }

            // Modules
            if (root.TryGetProperty("modules", out var mods))
            {
                manifest.Modules.Clear();
                foreach (var m in mods.EnumerateArray())
                {
                    manifest.AddModule(
                        m.GetProperty("id").GetString()!,
                        m.GetProperty("namespace").GetString()!,
                        m.GetProperty("description").GetString()!);
                }
            }

            // Infrastructure
            if (root.TryGetProperty("infrastructure", out var infras))
            {
                foreach (var inf in infras.EnumerateArray())
                {
                    var t = inf.GetProperty("type").GetString();
                    var nm = inf.GetProperty("name").GetString()!;
                    var cs = inf.TryGetProperty("connectionString", out var csEl) ? csEl.GetString() : null;
                    switch (t)
                    {
                        case "PostgreSQL": manifest.AddPostgres(nm, cs); break;
                        case "Redis": manifest.AddRedis(nm, cs); break;
                        case "Elasticsearch": manifest.AddElastic(nm, cs); break;
                    }
                }
            }

            // Auth
            if (root.TryGetProperty("auth", out var auth) && auth.GetProperty("enabled").GetBoolean())
            {
                manifest.UseJwtAuth(
                    auth.TryGetProperty("issuer", out var iss) ? iss.GetString() : null,
                    auth.TryGetProperty("audience", out var aud) ? aud.GetString() : null);
            }

            // EF Core
            if (root.TryGetProperty("efCore", out var ef) && ef.GetProperty("enabled").GetBoolean())
            {
                manifest.WithEfCore(cfg =>
                {
                    cfg.Enabled = true;
                    if (ef.TryGetProperty("useSeparateEntityTypeConfigurations", out var sep))
                        cfg.UseSeparateEntityTypeConfigurations = sep.GetBoolean();
                    if (ef.TryGetProperty("columnNaming", out var cn))
                    {
                        cfg.WithColumnNaming(cn.GetString() switch
                        {
                            "CamelCase" => ColumnNamingConvention.CamelCase,
                            "PascalCase" => ColumnNamingConvention.PascalCase,
                            _ => ColumnNamingConvention.SnakeCase
                        });
                    }
                });
            }

            // Features
            if (root.TryGetProperty("includeOpenTelemetry", out var ot) && !ot.GetBoolean())
                manifest.WithoutOpenTelemetry();
            if (root.TryGetProperty("includeHealthChecks", out var hc) && !hc.GetBoolean())
                manifest.WithoutHealthChecks();
            if (root.TryGetProperty("includeTests", out var ts) && !ts.GetBoolean())
                manifest.WithoutTests();

            // Packages
            if (root.TryGetProperty("packages", out var pkgs))
            {
                foreach (var p in pkgs.EnumerateArray())
                    manifest.AddPackage(p.GetProperty("id").GetString()!, p.GetProperty("version").GetString()!);
            }

            // Agents
            if (root.TryGetProperty("agents", out var agents))
            {
                foreach (var a in agents.EnumerateArray())
                {
                    var kws = a.TryGetProperty("keywords", out var kw)
                        ? kw.EnumerateArray().Select(k => k.GetString()!).Where(s => s is not null).ToArray()!
                        : [];
                    manifest.AddAgent(
                        a.GetProperty("id").GetString()!,
                        a.GetProperty("treeName").GetString()!,
                        a.GetProperty("systemPrompt").GetString()!,
                        kws!);
                }
            }

            // Paradigms
            if (root.TryGetProperty("paradigms", out var pars) && pars.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var p in pars.EnumerateArray())
                {
                    var pName = p.GetString()?.ToLowerInvariant();
                    if (pName == "solid") manifest.AddParadigm(Paradigms.SOLID);
                    else if (pName == "dry") manifest.AddParadigm(Paradigms.DRY);
                    else if (pName == "kiss") manifest.AddParadigm(Paradigms.KISS);
                    else if (pName == "tdd") manifest.AddParadigm(Paradigms.TDD);
                    else if (pName == "yagni") manifest.AddParadigm(Paradigms.YAGNI);
                    else if (pName == "clean-code") manifest.AddParadigm(Paradigms.CleanCode);
                    else if (name == "ddd") manifest.AddParadigm(Paradigms.DDD);
                }
            }

            // Guardrails
            if (root.TryGetProperty("guardrails", out var grs) && grs.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var g in grs.EnumerateArray())
                {
                    var gName = g.GetString();
                    if (gName == "require-unit-tests") manifest.AddGuardrail(GuardrailSpec.RequireUnitTests());
                    else if (gName == "require-structured-logging") manifest.AddGuardrail(GuardrailSpec.RequireStructuredLogging());
                    else if (gName == "require-opentelemetry") manifest.AddGuardrail(GuardrailSpec.RequireOpenTelemetry());
                    else if (gName == "require-health-checks") manifest.AddGuardrail(GuardrailSpec.RequireHealthChecks());
                    else if (gName == "require-error-handling") manifest.AddGuardrail(GuardrailSpec.RequireErrorHandling());
                    else if (gName == "deny-raw-sql") manifest.AddGuardrail(GuardrailSpec.DenyRawSql());
                    else if (gName == "require-solid") manifest.AddGuardrail(GuardrailSpec.RequireSOLID());
                    else if (gName == "require-dry") manifest.AddGuardrail(GuardrailSpec.RequireDRY());
                    else if (gName == "require-tdd") manifest.AddGuardrail(GuardrailSpec.RequireTDD());
                }
            }

            return manifest;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse ProjectManifest from LLM response.\n\nLLM response:\n{json}\n\nError: {ex.Message}");
        }
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// SUPPORT TYPES
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>Fluent configuration for guardrails.</summary>
public sealed class GuardrailConfig
{
    private readonly List<GuardrailSpec> _specs = [];

    public GuardrailConfig RequireUnitTests()
    {
        _specs.Add(GuardrailSpec.RequireUnitTests());
        return this;
    }

    public GuardrailConfig RequireStructuredLogging()
    {
        _specs.Add(GuardrailSpec.RequireStructuredLogging());
        return this;
    }

    public GuardrailConfig RequireOpenTelemetry()
    {
        _specs.Add(GuardrailSpec.RequireOpenTelemetry());
        return this;
    }

    public GuardrailConfig RequireDependencyInjection()
    {
        _specs.Add(GuardrailSpec.RequireDependencyInjection());
        return this;
    }

    public GuardrailConfig DenyRawSql()
    {
        _specs.Add(GuardrailSpec.DenyRawSql());
        return this;
    }

    public GuardrailConfig RequireHealthChecks()
    {
        _specs.Add(GuardrailSpec.RequireHealthChecks());
        return this;
    }

    public GuardrailConfig RequireErrorHandling()
    {
        _specs.Add(GuardrailSpec.RequireErrorHandling());
        return this;
    }

    public GuardrailConfig RequireSOLID()  { _specs.Add(GuardrailSpec.RequireSOLID()); return this; }
    public GuardrailConfig RequireDRY()    { _specs.Add(GuardrailSpec.RequireDRY()); return this; }
    public GuardrailConfig RequireKISS()   { _specs.Add(GuardrailSpec.RequireKISS()); return this; }
    public GuardrailConfig RequireTDD()    { _specs.Add(GuardrailSpec.RequireTDD()); return this; }

    internal List<GuardrailSpec> Build() => [.. _specs];
}

/// <summary>Result of the full generation pipeline.</summary>
public record ProjectGeneratorResult(
    string ProjectName,
    bool Success,
    TimeSpan Duration,
    GenerationResult Generation,
    ValidationResult Validation,
    PromptFulfillmentResult? Fulfillment,
    RuntimeSmokeResult? RuntimeSmoke,
    SandboxResult? Sandbox,
    CooperativePlanResult? CooperativePlan,
    List<GenEvent> Events,
    string OutputPath
);

internal readonly record struct ResolvedAgent(IAgxiomeAgent Agent, bool Owned);

public record RuntimeSmokeCheck(string Id, bool Passed, string Evidence);

public record RuntimeSmokeResult(
    bool Success,
    bool Executed,
    int Port,
    IReadOnlyList<RuntimeSmokeCheck> Checks);

public record GenEvent(string Phase, string Status, string? Detail = null);

public record CooperativePlanStep(
    string Id,
    string ModuleId,
    string TargetPath,
    AgentSpecialization Owner,
    string Goal,
    string Notes);

public record CooperativePlanResult(IReadOnlyList<CooperativePlanStep> Steps);

/// <summary>Result of agentic code generation phase.</summary>
public record AgenticGenerationResult(List<CodeArtifact> Artifacts, GenerationResult Generation, CooperativePlanResult? Plan);
