namespace AGXIOME.Core.Generator;

/// <summary>Core implementation for the AGXIOME agentic orchestration framework.</summary>
public sealed class ModuleDependencyPlanner
{
    private readonly IReadOnlyList<ModuleSpec> _modules;

    public ModuleDependencyPlanner(IReadOnlyList<ModuleSpec> modules)
    {
        _modules = modules;
    }

    public IReadOnlyList<ModuleSpec> GetDirectDependencies(ModuleSpec module)
    {
        var role = ResolveRole(module.Id);
        var dependencies = role switch
        {
            ModuleRole.Api => GetByRoles(ModuleRole.Application, ModuleRole.Infrastructure),
            ModuleRole.Application => GetByRoles(ModuleRole.Domain),
            ModuleRole.Infrastructure => GetByRoles(ModuleRole.Application, ModuleRole.Domain),
            ModuleRole.Agents => GetByRoles(ModuleRole.Application, ModuleRole.Domain),
            ModuleRole.Toolkits => GetToolkitDependencies(),
            ModuleRole.Documentation => [],
            _ => []
        };

        return dependencies
            .Where(candidate => !candidate.Id.Equals(module.Id, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void ValidateAcyclic()
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in _modules)
            Visit(module, visited, visiting);
    }

    private void Visit(ModuleSpec module, HashSet<string> visited, HashSet<string> visiting)
    {
        if (visited.Contains(module.Id))
            return;

        if (!visiting.Add(module.Id))
            throw new InvalidOperationException($"Circular module dependency detected at '{module.Id}'.");

        foreach (var dependency in GetDirectDependencies(module))
            Visit(dependency, visited, visiting);

        visiting.Remove(module.Id);
        visited.Add(module.Id);
    }

    private IReadOnlyList<ModuleSpec> GetToolkitDependencies()
    {
        var infrastructure = GetByRoles(ModuleRole.Infrastructure);
        if (infrastructure.Count > 0)
            return infrastructure;

        return GetByRoles(ModuleRole.Application);
    }

    private List<ModuleSpec> GetByRoles(params ModuleRole[] roles)
    {
        var roleSet = roles.ToHashSet();
        return _modules
            .Where(module => roleSet.Contains(ResolveRole(module.Id)))
            .ToList();
    }

    private static ModuleRole ResolveRole(string moduleId)
    {
        var normalized = moduleId.Trim().ToLowerInvariant();

        if (normalized is "api" or "webapi" || normalized.EndsWith(".api"))
            return ModuleRole.Api;
        if (normalized is "application" || normalized.EndsWith(".application"))
            return ModuleRole.Application;
        if (normalized is "domain" or "core" || normalized.EndsWith(".domain") || normalized.EndsWith(".core"))
            return ModuleRole.Domain;
        if (normalized is "infrastructure" || normalized.EndsWith(".infrastructure"))
            return ModuleRole.Infrastructure;
        if (normalized is "agents" || normalized.EndsWith(".agents"))
            return ModuleRole.Agents;
        if (normalized is "toolkits" || normalized.EndsWith(".toolkits"))
            return ModuleRole.Toolkits;
        if (normalized is "documentation" || normalized.EndsWith(".documentation") || normalized is "docs")
            return ModuleRole.Documentation;

        return ModuleRole.Other;
    }

    private enum ModuleRole
    {
        Api,
        Application,
        Domain,
        Infrastructure,
        Agents,
        Toolkits,
        Documentation,
        Other
    }
}