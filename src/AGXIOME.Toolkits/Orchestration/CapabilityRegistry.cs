// CO-A1 — CapabilityRegistry
// Enhanced toolkit registry with lazy assembly scanning, manifest caching, and capability queries.
// Complements the basic ToolkitRegistry (manual registration) with auto-discovery support.

using System.Reflection;

namespace AGXIOME.Toolkits.Orchestration;

/// <summary>
/// Discovers and manages all registered toolkits, exposing typed <see cref="CapabilityManifest"/>
/// descriptors for routing, selection, and governance.
///
/// <para>
/// Two registration modes:
/// <list type="bullet">
///   <item><description><see cref="Register"/> — manual, for explicit DI wiring.</description></item>
///   <item><description><see cref="ScanAssembly"/> — reflection-based, scans <c>[Toolkit]</c> attributes.</description></item>
/// </list>
/// </para>
///
/// Thread-safe. Manifest cache is invalidated on every <see cref="Register"/> or <see cref="ScanAssembly"/> call.
/// </summary>
public sealed class CapabilityRegistry
{
    private readonly List<IToolkit>  _toolkits = [];
    private List<CapabilityManifest>? _cache;
    private readonly object           _lock = new();

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>Register a single toolkit instance.</summary>
    public CapabilityRegistry Register(IToolkit toolkit)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        lock (_lock)
        {
            _toolkits.RemoveAll(t => t.Id == toolkit.Id); // replace existing
            _toolkits.Add(toolkit);
            _cache = null;
        }
        return this;
    }

    /// <summary>
    /// Scan an assembly for all types decorated with <c>[Toolkit]</c> and instantiate them
    /// using <paramref name="config"/> (may be null for zero-config toolkits).
    /// Skips types that cannot be instantiated.
    /// </summary>
    public CapabilityRegistry ScanAssembly(Assembly assembly, ToolkitConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var types = ToolkitRegistry.DiscoverTypes(assembly);
        foreach (var type in types)
        {
            try
            {
                if (Activator.CreateInstance(type, config) is IToolkit toolkit)
                    Register(toolkit);
            }
            catch
            {
                // Non-instantiable toolkit (e.g. missing required deps) — skip gracefully.
            }
        }
        return this;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>All registered toolkit manifests.</summary>
    public IReadOnlyList<CapabilityManifest> GetAll()
        => GetManifests();

    /// <summary>Only manifests for toolkits that are ready to execute (<c>IsConfigured == true</c>).</summary>
    public IReadOnlyList<CapabilityManifest> GetConfigured()
        => GetManifests().Where(m => m.IsConfigured).ToList();

    /// <summary>Manifests filtered by capability category.</summary>
    public IReadOnlyList<CapabilityManifest> GetByCategory(ToolkitCategory category)
        => GetManifests().Where(m => m.Category == category).ToList();

    /// <summary>Find a manifest by exact toolkit ID. Returns null if not registered.</summary>
    public CapabilityManifest? FindById(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return GetManifests().FirstOrDefault(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Resolve the live <see cref="IToolkit"/> instance for a given ID.</summary>
    public IToolkit? ResolveToolkit(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_lock)
            return _toolkits.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Total number of registered toolkits.</summary>
    public int Count { get { lock (_lock) return _toolkits.Count; } }

    // ── Internal ──────────────────────────────────────────────────────────────

    private List<CapabilityManifest> GetManifests()
    {
        lock (_lock)
        {
            if (_cache is not null) return _cache;
            _cache = _toolkits.Select(BuildManifest).ToList();
            return _cache;
        }
    }

    private static CapabilityManifest BuildManifest(IToolkit t)
    {
        var actions = t is ISupportedActionsProvider provider
            ? provider.SupportedActions
            : (IReadOnlyList<string>)[];

        return new CapabilityManifest(
            Id:               t.Id,
            Name:             t.Name,
            Category:         t.Category,
            IsConfigured:     t.IsConfigured,
            SupportedActions: actions);
    }
}
