// MU-09 — Semantic Kernel Bridge
// Wraps Microsoft Semantic Kernel plugins as AGXIOME-callable capabilities.
// Uses in-process adapter pattern: SK kernel is instantiated here, callable via bridge interface.
// Does NOT reference the SK NuGet package directly; uses a thin adapter to avoid hard coupling.

namespace AGXIOME.XLink.Bridges;

// ── Adapter contract (implemented by sk-specific assembly if available) ───────

/// <summary>
/// Abstraction over an SK-compatible kernel.
/// Implemented by AGXIOME.XLink.SK adapter if Microsoft.SemanticKernel is present.
/// </summary>
public interface ISemanticKernelAdapter
{
    Task<string> InvokeFunctionAsync(string pluginName, string functionName,
        Dictionary<string, string> args, CancellationToken ct = default);

    IReadOnlyList<string> ListPlugins();
    bool IsReady { get; }
}

// ── Bridge interface ──────────────────────────────────────────────────────────

public interface ISemanticKernelBridge
{
    bool IsAvailable { get; }

    /// <summary>Invokes a Semantic Kernel plugin function with the given arguments.</summary>
    Task<AgentBridgeResult> InvokePluginAsync(
        string pluginName,
        string functionName,
        Dictionary<string, string> args,
        CancellationToken ct = default);

    /// <summary>Lists all loaded plugins in the underlying SK kernel.</summary>
    IReadOnlyList<string> ListPlugins();
}

// ── Bridge implementation ─────────────────────────────────────────────────────

public sealed class SemanticKernelBridge : ISemanticKernelBridge
{
    private readonly ISemanticKernelAdapter? _adapter;

    public SemanticKernelBridge(ISemanticKernelAdapter? adapter = null)
    {
        _adapter = adapter;
    }

    public bool IsAvailable => _adapter?.IsReady is true;

    public async Task<AgentBridgeResult> InvokePluginAsync(
        string pluginName, string functionName,
        Dictionary<string, string> args, CancellationToken ct = default)
    {
        if (!IsAvailable)
            return new AgentBridgeResult(false, null, "Semantic Kernel adapter not configured", "SemanticKernel");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await _adapter!.InvokeFunctionAsync(pluginName, functionName, args, ct);
            sw.Stop();
            return new AgentBridgeResult(true, result, null, "SemanticKernel", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new AgentBridgeResult(false, null, ex.Message, "SemanticKernel", sw.ElapsedMilliseconds);
        }
    }

    public IReadOnlyList<string> ListPlugins()
        => _adapter?.ListPlugins() ?? [];
}

// ── Null / test adapter ────────────────────────────────────────────────────────

/// <summary>Test double: returns deterministic outputs without SK runtime.</summary>
public sealed class NullSemanticKernelAdapter : ISemanticKernelAdapter
{
    private readonly Dictionary<(string, string), string> _results;

    public NullSemanticKernelAdapter(Dictionary<(string, string), string>? results = null)
        => _results = results ?? [];

    public bool IsReady => true;

    public Task<string> InvokeFunctionAsync(string plugin, string fn,
        Dictionary<string, string> args, CancellationToken ct = default)
    {
        var key    = (plugin, fn);
        var result = _results.TryGetValue(key, out var v) ? v : $"[{plugin}.{fn}] result";
        return Task.FromResult(result);
    }

    public IReadOnlyList<string> ListPlugins() => _results.Keys.Select(k => k.Item1).Distinct().ToList();
}
