using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Tools;

// ─────────────────────────────────────────────────────────────────────────────
// ToolExecutionPipeline — P11 Integration
//
// Composable, provider-agnostic pipeline that connects the tool infrastructure
// components around every IAgxiomeExecutableTool.ExecuteAsync call:
//
//   IToolFilter          → block/allow tools per scope before execution
//   IToolCallLimiter     → enforce call count and error-rate limits per run
//   IToolResultCache     → L1 in-memory cache; avoid redundant external calls
//   IToolMetricsCollector → latency, error rate, call count per tool
//
// All services are optional — the pipeline degrades gracefully when absent.
// The pipeline does NOT know about any specific tool or provider: it is purely
// a middleware stack over IAgxiomeExecutableTool.
//
// Usage (DI):
//   services.AddAgxiome(...).AddToolInfra();
//   // Then resolve IToolExecutor from DI where tool dispatch is needed.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Provider-agnostic contract for executing a named tool through the full infrastructure pipeline.
/// Separates the "what to execute" (IAgxiomeTool) from the "how to execute safely" (pipeline).
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Execute the named tool within the given context, applying all registered middleware:
    /// filter → limit check → cache lookup → execute → record metrics → cache store.
    /// Returns a <see cref="ToolResult"/> regardless of success or failure — never throws
    /// unless the tool itself cannot be found.
    /// </summary>
    /// <param name="toolName">Name of the registered tool to invoke.</param>
    /// <param name="context">
    /// Execution context carrying <see cref="ToolContext.RunId"/>, <see cref="ToolContext.AgentId"/>,
    /// and optional <see cref="ToolContext.Parameters"/>.
    /// </param>
    /// <param name="ct">Cancellation token propagated to the tool.</param>
    Task<ToolResult> ExecuteAsync(string toolName, ToolContext context, CancellationToken ct = default);
}

/// <summary>Options controlling which middleware stages are active.</summary>
public sealed record ToolExecutionPipelineOptions
{
    /// <summary>Block/allow tools per scope. Requires <see cref="IToolFilter"/> in DI.</summary>
    public bool EnableFilter { get; init; } = true;

    /// <summary>Enforce per-run call and error-rate limits. Requires <see cref="IToolCallLimiter"/> in DI.</summary>
    public bool EnableCallLimits { get; init; } = true;

    /// <summary>Cache tool results to avoid redundant external calls. Requires <see cref="IToolResultCache"/> in DI.</summary>
    public bool EnableResultCache { get; init; } = false;

    /// <summary>Track latency, error-rate and call counts per tool. Requires <see cref="IToolMetricsCollector"/> in DI.</summary>
    public bool EnableMetrics { get; init; } = true;
}

/// <summary>
/// Default implementation of <see cref="IToolExecutor"/>.
/// Applies the P11 tool infrastructure pipeline around every <see cref="IAgxiomeExecutableTool"/> invocation.
/// </summary>
public sealed class ToolExecutionPipeline : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly ToolExecutionPipelineOptions _options;
    private readonly IToolFilter? _filter;
    private readonly IToolCallLimiter? _limiter;
    private readonly IToolResultCache? _cache;
    private readonly IToolMetricsCollector? _metrics;

    public ToolExecutionPipeline(
        IToolRegistry registry,
        ToolExecutionPipelineOptions? options = null,
        IToolFilter? filter = null,
        IToolCallLimiter? limiter = null,
        IToolResultCache? cache = null,
        IToolMetricsCollector? metrics = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _options  = options  ?? new ToolExecutionPipelineOptions();
        _filter   = filter;
        _limiter  = limiter;
        _cache    = cache;
        _metrics  = metrics;
    }

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(
        string toolName,
        ToolContext context,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(context);

        // ── 1. Resolve tool ───────────────────────────────────────────────────
        var tool = _registry.GetTool(toolName);
        if (tool is null)
            return new ToolResult(false, null, $"Tool '{toolName}' is not registered.");

        if (tool is not IAgxiomeExecutableTool executable)
            return new ToolResult(false, null, $"Tool '{toolName}' is not executable (does not implement IAgxiomeExecutableTool).");

        // ── 2. Filter check ───────────────────────────────────────────────────
        if (_options.EnableFilter && _filter is not null)
        {
            var scopeId = context.RunId ?? context.AgentId;
            if (!_filter.IsAllowed(scopeId, toolName))
                return new ToolResult(false, null, $"Tool '{toolName}' is blocked by the active filter for scope '{scopeId}'.");
        }

        // ── 3. Call-limit check ───────────────────────────────────────────────
        if (_options.EnableCallLimits && _limiter is not null && context.RunId is not null)
        {
            var check = _limiter.Check(context.RunId, toolName);
            if (!check.Allowed)
                return new ToolResult(false, null, $"Tool call blocked: {check.Reason} (violation: {check.Violation})");
        }

        // ── 4. Cache lookup ───────────────────────────────────────────────────
        if (_options.EnableResultCache && _cache is not null)
        {
            var cached = await _cache.TryGetAsync(toolName, context.Parameters, ct);
            if (cached is not null)
                return cached;
        }

        // ── 5. Execute ────────────────────────────────────────────────────────
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ToolResult result;
        bool success;

        try
        {
            result  = await executable.ExecuteAsync(context, ct);
            success = result.Success;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _metrics?.Record(toolName, false, sw.Elapsed.TotalMilliseconds);
            if (context.RunId is not null) _limiter?.RecordError(context.RunId, toolName);
            throw; // propagate cancellation cleanly
        }
        catch (Exception ex)
        {
            sw.Stop();
            result  = new ToolResult(false, null, ex.Message, sw.Elapsed);
            success = false;
        }

        sw.Stop();

        // ── 6. Record limit outcome ───────────────────────────────────────────
        if (_options.EnableCallLimits && _limiter is not null && context.RunId is not null)
        {
            if (success) _limiter.RecordSuccess(context.RunId, toolName);
            else         _limiter.RecordError(context.RunId, toolName);
        }

        // ── 7. Cache store ────────────────────────────────────────────────────
        if (_options.EnableResultCache && _cache is not null && success)
            await _cache.SetAsync(toolName, context.Parameters, result, ct: ct);

        // ── 8. Record metrics ─────────────────────────────────────────────────
        if (_options.EnableMetrics && _metrics is not null)
            _metrics.Record(toolName, success, sw.Elapsed.TotalMilliseconds);

        return result with { ExecutionTime = sw.Elapsed };
    }
}
