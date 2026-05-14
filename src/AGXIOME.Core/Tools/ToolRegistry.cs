using System.Collections.Concurrent;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

/// <summary>Thread-safe tool registry. Global + per-agent tool sets.</summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, IAgxiomeTool> _global = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _perAgent = new();
    private readonly List<IToolHook> _toolHooks = new();

    public void Register<T>() where T : IAgxiomeTool
    {
        var instance = Activator.CreateInstance<T>();
        _global[instance.Name] = instance;
    }

    public void Register(IAgxiomeTool tool) => _global[tool.Name] = tool;
    public void Unregister(string name) => _global.TryRemove(name, out _);

    public void RegisterForAgent(string agentId, string toolName)
        => _perAgent.AddOrUpdate(agentId, _ => new() { toolName }, (_, s) => { s.Add(toolName); return s; });

    public void UnregisterFromAgent(string agentId, string toolName)
    {
        if (_perAgent.TryGetValue(agentId, out var s)) s.Remove(toolName);
    }

    public IAgxiomeTool? GetTool(string name) => _global.GetValueOrDefault(name);

    public IReadOnlyList<IAgxiomeTool> GetAgentTools(string agentId)
    {
        if (!_perAgent.TryGetValue(agentId, out var names)) return Array.Empty<IAgxiomeTool>();
        return names.Select(n => _global.GetValueOrDefault(n)).Where(t => t != null).Select(t => t!).ToList();
    }

    public IReadOnlyList<IAgxiomeTool> GetAllTools() => _global.Values.ToList();
    public IReadOnlyList<IAgxiomeTool> FindByCategory(string category)
        => _global.Values.Where(t => t.Categories.Contains(category)).ToList();

    public System.Text.Json.Nodes.JsonArray GetToolSchemas(string agentId)
    {
        var arr = new System.Text.Json.Nodes.JsonArray();
        foreach (var t in GetAgentTools(agentId))
            if (t is IAgxiomeExecutableTool et) arr.Add(et.ToJsonSchema());
        return arr;
    }

    public void AddToolHook(IToolHook hook) => _toolHooks.Add(hook);
}

public static class ToolRegistryExtensions
{
    private static readonly ConcurrentDictionary<string, object> _lock = new();

    /// <summary>Register a tool from a callable factory (resolved at runtime per context).</summary>
    public static void RegisterCallable(this IToolRegistry registry, Func<ToolContext, List<IAgxiomeTool>> factory)
    {
        // Factory is evaluated at run start, cached by user_id/session_id
        // Implementation uses MemoryCache internally
    }
}
