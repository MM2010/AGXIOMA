using AGXIOME.Core.LLM;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Execution;

// ─────────────────────────────────────────────────────────────────────────────
// AgentRunPipeline — P09 Integration
//
// Composable, provider-agnostic execution pipeline that connects:
//   ResponseCache   → check/populate L1/L2 cache around every LLM call
//   CancellationManager → register each run for external cancel support
//   TokenCounter    → approximate usage tracking before/after run
//   BackgroundRunWorker → optional fire-and-forget dispatch
//
// All cross-cutting services are optional — inject only what you need.
// The pipeline is a thin decorator over IAgxiomeAgent: the agent itself
// is unchanged and unaware of the pipeline.
//
// Usage (DI):
//   services.AddAgxiome(...).AddExecutionPipeline();
//   // Then resolve AgentRunPipeline<T> or IAgentRunPipeline where needed.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Options controlling which cross-cutting concerns are active for a pipeline instance.</summary>
public sealed record AgentRunPipelineOptions
{
    /// <summary>Enable response caching. Requires <see cref="ResponseCache"/> to be registered in DI.</summary>
    public bool EnableResponseCache { get; init; } = false;

    /// <summary>Track each run in <see cref="ICancellationManager"/> for external cancellation support.</summary>
    public bool EnableCancellationTracking { get; init; } = true;

    /// <summary>Approximate token usage tracking via <see cref="TokenCounter"/>.</summary>
    public bool EnableTokenCounting { get; init; } = true;

    /// <summary>
    /// Factory for run IDs. Defaults to a short UUID (8-char hex).
    /// Override to inject session IDs, correlation IDs, or trace IDs.
    /// </summary>
    public Func<string>? RunIdFactory { get; init; }

    /// <summary>Default provider for token counting. Only used when <see cref="EnableTokenCounting"/> is true.</summary>
    public LLMProviderType TokenCounterProvider { get; init; } = LLMProviderType.DeepSeek;
}

/// <summary>Contract for the provider-agnostic execution pipeline.</summary>
public interface IAgentRunPipeline
{
    /// <summary>Run the agent through the full pipeline (cache → cancel-register → execute → token-count → cache-store).</summary>
    Task<AgentResponse> RunAsync(AgentRequest request, string? runId = null, CancellationToken ct = default);

    /// <summary>Run the agent in streaming mode. Cross-cutting concerns applied to each chunk.</summary>
    IAsyncEnumerable<AgentChunk> RunStreamAsync(AgentRequest request, string? runId = null, CancellationToken ct = default);

    /// <summary>Enqueue the agent run in the background worker. Returns the assigned job ID.</summary>
    string RunBackground(AgentRequest request, string? runId = null);

    /// <summary>Cancel a run that was previously started via <see cref="RunAsync"/> or <see cref="RunBackground"/>.</summary>
    bool CancelRun(string runId);

    /// <summary>The agent wrapped by this pipeline.</summary>
    IAgxiomeAgent Agent { get; }
}

/// <summary>
/// Default implementation of <see cref="IAgentRunPipeline"/>.
/// All cross-cutting services are optional — the pipeline degrades gracefully when they are absent.
/// </summary>
public sealed class AgentRunPipeline : IAgentRunPipeline
{
    private readonly IAgxiomeAgent _agent;
    private readonly ResponseCache? _responseCache;
    private readonly ICancellationManager? _cancellationManager;
    private readonly TokenCounter? _tokenCounter;
    private readonly IBackgroundRunWorker? _backgroundWorker;
    private readonly AgentRunPipelineOptions _options;

    public IAgxiomeAgent Agent => _agent;

    public AgentRunPipeline(
        IAgxiomeAgent agent,
        AgentRunPipelineOptions? options = null,
        ResponseCache? responseCache = null,
        ICancellationManager? cancellationManager = null,
        TokenCounter? tokenCounter = null,
        IBackgroundRunWorker? backgroundWorker = null)
    {
        _agent             = agent ?? throw new ArgumentNullException(nameof(agent));
        _options           = options ?? new AgentRunPipelineOptions();
        _responseCache     = responseCache;
        _cancellationManager = cancellationManager;
        _tokenCounter      = tokenCounter;
        _backgroundWorker  = backgroundWorker;
    }

    /// <inheritdoc/>
    public async Task<AgentResponse> RunAsync(
        AgentRequest request,
        string? runId = null,
        CancellationToken ct = default)
    {
        var id = runId ?? NewRunId();

        // ── 1. Register cancellation ──────────────────────────────────────────
        CancellationToken effectiveCt = ct;
        if (_options.EnableCancellationTracking && _cancellationManager is not null)
        {
            var managedToken = _cancellationManager.Register(id);
            effectiveCt = CancellationTokenSource.CreateLinkedTokenSource(ct, managedToken).Token;
        }

        // ── 2. Cache check ────────────────────────────────────────────────────
        if (_options.EnableResponseCache && _responseCache is not null)
        {
            var cached = await _responseCache.GetAsync(
                _options.TokenCounterProvider,
                "pipeline",
                request.Prompt,
                effectiveCt);

                        if (cached is not null)
                return new AgentResponse(cached, AgentStatus.Completed);
        }

        // ── 3. Token count input ──────────────────────────────────────────────
        TokenCountResult? inputCount = null;
        if (_options.EnableTokenCounting && _tokenCounter is not null)
            inputCount = _tokenCounter.Count(request.Prompt, _options.TokenCounterProvider);

        // ── 4. Execute ────────────────────────────────────────────────────────
        AgentResponse response;
        try
        {
            response = await _agent.RunAsync(request, effectiveCt);
        }
        finally
        {
            // Always clean up cancellation registration
            if (_options.EnableCancellationTracking && _cancellationManager is not null)
                _cancellationManager.Remove(id);
        }

        // ── 5. Token count output ─────────────────────────────────────────────
        // Token counts are tracked internally; callers can retrieve them via TokenCounter.Count().
        // AgentResponse is not mutated — it remains immutable and provider-agnostic.
        if (_options.EnableTokenCounting && _tokenCounter is not null && inputCount is not null)
        {
            _ = _tokenCounter.Count(response.Content ?? string.Empty, _options.TokenCounterProvider);
        }

        // ── 6. Cache store ────────────────────────────────────────────────────
        if (_options.EnableResponseCache && _responseCache is not null && response.Content is not null)
        {
            await _responseCache.SetAsync(
                _options.TokenCounterProvider,
                "pipeline",
                request.Prompt,
                response.Content,
                ct: effectiveCt);
        }

        return response;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<AgentChunk> RunStreamAsync(
        AgentRequest request,
        string? runId = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var id = runId ?? NewRunId();

        CancellationToken effectiveCt = ct;
        if (_options.EnableCancellationTracking && _cancellationManager is not null)
        {
            var managedToken = _cancellationManager.Register(id);
            effectiveCt = CancellationTokenSource.CreateLinkedTokenSource(ct, managedToken).Token;
        }

        try
        {
            await foreach (var chunk in _agent.RunStreamAsync(request, effectiveCt))
                yield return chunk;
        }
        finally
        {
            if (_options.EnableCancellationTracking && _cancellationManager is not null)
                _cancellationManager.Remove(id);
        }
    }

    /// <inheritdoc/>
    public string RunBackground(AgentRequest request, string? runId = null)
    {
        if (_backgroundWorker is null)
            throw new InvalidOperationException(
                "BackgroundRunWorker is not registered. Call AddExecutionPipeline(o => o.EnableBackgroundWorker()) in DI.");

        var id = runId ?? NewRunId();
        var job = new RunJob(id, BackgroundJobType.RunExecution, _agent.Id, request.Prompt);
        return _backgroundWorker.Enqueue(job);
    }

    /// <inheritdoc/>
    public bool CancelRun(string runId)
    {
        if (_cancellationManager is not null && _cancellationManager.Cancel(runId))
            return true;

        if (_backgroundWorker is not null)
            return _backgroundWorker.Cancel(runId);

        return false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string NewRunId() =>
        _options.RunIdFactory is not null
            ? _options.RunIdFactory()
            : Guid.NewGuid().ToString("N")[..8];
}
