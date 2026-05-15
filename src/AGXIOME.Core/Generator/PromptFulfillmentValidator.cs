namespace AGXIOME.Core.Generator;

/// <summary>
/// Validates whether the generated project satisfies prompt-level requirements,
/// beyond compile success and generic guardrails.
/// </summary>
public sealed class PromptFulfillmentValidator
{
    private readonly string _prompt;
    private readonly ProjectManifest _manifest;
    private readonly string _outputRoot;

    public PromptFulfillmentValidator(string prompt, ProjectManifest manifest, string outputRoot)
    {
        _prompt = prompt ?? string.Empty;
        _manifest = manifest;
        _outputRoot = outputRoot;
    }

    public Task<PromptFulfillmentResult> ValidateAsync(CancellationToken ct = default)
    {
        var checks = new List<PromptFulfillmentCheck>();
        var prompt = _prompt.ToLowerInvariant();
        var files = LoadFiles();

        if (Mentions(prompt, "clean architecture"))
        {
            var passed = _manifest.HasModule("Core")
                && _manifest.HasModule("Application")
                && _manifest.HasModule("Infrastructure")
                && _manifest.HasModule("Api");
            checks.Add(new PromptFulfillmentCheck(
                "clean-architecture",
                "Project includes Core, Application, Infrastructure, and Api modules",
                passed,
                passed ? "Modules found in manifest" : "One or more required architectural modules are missing"));
        }

        if (Mentions(prompt, "entity framework", "ef core"))
        {
            var passed = HasText(files, "Microsoft.EntityFrameworkCore")
                && HasText(files, "DbContext");
            checks.Add(new PromptFulfillmentCheck(
                "ef-core",
                "Project uses Entity Framework Core",
                passed,
                passed ? "Entity Framework references detected" : "EF Core package or DbContext usage missing"));
        }

        if (Mentions(prompt, "in-memory"))
        {
            var passed = HasText(files, "EntityFrameworkCore.InMemory")
                && HasText(files, "UseInMemoryDatabase");
            checks.Add(new PromptFulfillmentCheck(
                "in-memory-db",
                "Project configures EF Core InMemory database",
                passed,
                passed ? "InMemory provider registration found" : "InMemory package or UseInMemoryDatabase configuration missing"));
        }

        if (Mentions(prompt, "mediatr", "command/query separation", "cqrs"))
        {
            var passed = HasText(files, "MediatR")
                && (HasText(files, "IRequest<") || HasText(files, "IRequestHandler<"));
            checks.Add(new PromptFulfillmentCheck(
                "mediatr-cqrs",
                "Project implements MediatR/CQRS handlers",
                passed,
                passed ? "MediatR handlers detected" : "MediatR package exists but request/handler implementation is missing"));
        }

        if (Mentions(prompt, "incident"))
        {
            var passed = HasPath(files, "Incident.cs") || HasText(files, "class Incident") || HasText(files, "record Incident");
            checks.Add(new PromptFulfillmentCheck(
                "incident-entity",
                "Incident entity is present",
                passed,
                passed ? "Incident type detected" : "Incident entity not found"));
        }

        if (Mentions(prompt, "technician"))
        {
            var passed = HasPath(files, "Technician.cs") || HasText(files, "class Technician") || HasText(files, "record Technician");
            checks.Add(new PromptFulfillmentCheck(
                "technician-entity",
                "Technician entity is present",
                passed,
                passed ? "Technician type detected" : "Technician entity not found"));
        }

        if (Mentions(prompt, "create a new incident", "create incident"))
        {
            var passed = HasText(files, "MapPost(\"/incidents\"") || HasText(files, "CreateAsync(");
            checks.Add(new PromptFulfillmentCheck(
                "create-incident",
                "Project exposes create incident flow",
                passed,
                passed ? "POST /incidents or create service found" : "Create incident endpoint/flow missing"));
        }

        if (Mentions(prompt, "list all incidents", "list incidents"))
        {
            var passed = HasText(files, "MapGet(\"/incidents\"") || HasText(files, "ListAsync(");
            checks.Add(new PromptFulfillmentCheck(
                "list-incidents",
                "Project exposes list incidents flow",
                passed,
                passed ? "GET /incidents or list service found" : "List incidents endpoint/flow missing"));
        }

        if (Mentions(prompt, "assign an incident", "assignments to technicians", "assignment to technicians"))
        {
            var passed = HasText(files, "assign") && HasText(files, "technician");
            checks.Add(new PromptFulfillmentCheck(
                "assign-incident",
                "Project supports incident assignment to technician",
                passed,
                passed ? "Assignment-related implementation found" : "Assignment flow to technician missing"));
        }

        if (Mentions(prompt, "update incident status", "status tracking"))
        {
            var passed = HasText(files, "status")
                && (HasText(files, "MapPut(") || HasText(files, "MapPatch(") || HasText(files, "UpdateStatus") || HasText(files, "SetStatus"));
            checks.Add(new PromptFulfillmentCheck(
                "update-status",
                "Project supports updating incident status",
                passed,
                passed ? "Status update implementation found" : "Incident status update flow missing"));
        }

        if (Mentions(prompt, "seeder", "seed", "initial technicians"))
        {
            var passed = HasText(files, "Seeder") || HasText(files, "Seed") || HasText(files, "HasData(");
            checks.Add(new PromptFulfillmentCheck(
                "seed-data",
                "Project includes initial seed data",
                passed,
                passed ? "Seed data implementation found" : "Seeder or initial data population missing"));
        }

        if (Mentions(prompt, "agxioma", "agent", "agents"))
        {
            var passed = HasText(files, "AddAgxiome") || HasText(files, "UseProvider(") || HasText(files, "IAgxiomeAgent") || HasText(files, "ForestSwarm") || HasText(files, "GrokSwarm");
            checks.Add(new PromptFulfillmentCheck(
                "agxioma-agents",
                "Project includes AGXIOMA-based agent wiring when requested",
                passed,
                passed ? "AGXIOMA agent integration detected" : "Prompt mentions agents/AGXIOMA but generated project does not wire AGXIOMA agents"));
        }

        if (checks.Count == 0)
        {
            checks.Add(new PromptFulfillmentCheck(
                "prompt-present",
                "Prompt captured for generation",
                !string.IsNullOrWhiteSpace(_prompt),
                !string.IsNullOrWhiteSpace(_prompt) ? "Prompt available" : "Prompt missing"));
        }

        var passedCount = checks.Count(c => c.Passed);
        return Task.FromResult(new PromptFulfillmentResult(
            checks.Count,
            passedCount,
            checks.Count - passedCount,
            checks));
    }

    private Dictionary<string, string> LoadFiles()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_outputRoot))
            return files;

        foreach (var file in Directory.EnumerateFiles(_outputRoot, "*.*", SearchOption.AllDirectories))
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/bin/") || normalized.Contains("/obj/"))
                continue;

            var extension = Path.GetExtension(file);
            if (extension is not ".cs" and not ".csproj" and not ".json" and not ".md" and not ".slnx")
                continue;

            files[normalized] = File.ReadAllText(file);
        }

        return files;
    }

    private static bool Mentions(string prompt, params string[] patterns) =>
        patterns.Any(prompt.Contains);

    private static bool HasPath(Dictionary<string, string> files, string pathFragment) =>
        files.Keys.Any(k => k.Contains(pathFragment, StringComparison.OrdinalIgnoreCase));

    private static bool HasText(Dictionary<string, string> files, string text) =>
        files.Any(kv => kv.Key.Contains(text, StringComparison.OrdinalIgnoreCase)
            || kv.Value.Contains(text, StringComparison.OrdinalIgnoreCase));
}

public record PromptFulfillmentCheck(
    string Id,
    string Description,
    bool Passed,
    string Evidence
);

public record PromptFulfillmentResult(
    int TotalChecks,
    int Passed,
    int Failed,
    IReadOnlyList<PromptFulfillmentCheck> Checks)
{
    public bool IsSatisfied => Failed == 0;
}