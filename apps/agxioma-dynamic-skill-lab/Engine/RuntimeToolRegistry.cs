using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Loader;
using AGXIOME.DynamicRuntime.Interfaces;

namespace AgxiomaDynamicSkillLab.Engine;

/// <summary>
/// Runtime registry for ephemeral dynamic tools.
/// Manages: load, execute, cache, unload, TTL, usage tracking.
/// </summary>
public sealed class RuntimeToolRegistry : IDisposable
{
    // Tool instances loaded in isolated ALCs
    private readonly ConcurrentDictionary<string, LoadedTool> _loaded = new();
    private readonly TimeSpan _defaultTtl = TimeSpan.FromMinutes(15);

    public void Register(string toolId, ISimpleDynamicAgent instance, AssemblyLoadContext context)
    {
        _loaded[toolId] = new LoadedTool(
            Instance: instance,
            Context: context,
            LoadedAt: DateTime.UtcNow,
            Ttl: _defaultTtl,
            UseCount: 0
        );
    }

    public ISimpleDynamicAgent? Get(string toolId)
    {
        if (_loaded.TryGetValue(toolId, out var entry) && !IsExpired(entry))
        {
            _loaded[toolId] = entry with { UseCount = entry.UseCount + 1 };
            return entry.Instance;
        }

        if (entry != null) Unload(toolId);
        return null;
    }

    public void Touch(string toolId)
    {
        if (_loaded.TryGetValue(toolId, out var entry))
            _loaded[toolId] = entry with { LoadedAt = DateTime.UtcNow };
    }

    public void Unload(string toolId)
    {
        if (_loaded.TryRemove(toolId, out var entry))
        {
            try { entry.Context.Unload(); } catch { /* GC will collect */ }
        }
    }

    public int Count => _loaded.Count;

    public List<string> GetActiveToolIds() =>
        _loaded.Where(kv => !IsExpired(kv.Value)).Select(kv => kv.Key).ToList();

    public void EvictExpired()
    {
        foreach (var (id, entry) in _loaded)
        {
            if (IsExpired(entry)) Unload(id);
        }
    }

    public void Dispose()
    {
        foreach (var id in _loaded.Keys.ToList()) Unload(id);
    }

    private bool IsExpired(LoadedTool entry) =>
        DateTime.UtcNow - entry.LoadedAt > entry.Ttl;
}

internal sealed record LoadedTool(
    ISimpleDynamicAgent Instance,
    AssemblyLoadContext Context,
    DateTime LoadedAt,
    TimeSpan Ttl,
    int UseCount
);
