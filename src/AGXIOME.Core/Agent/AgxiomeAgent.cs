using AGXIOME.Core.Learning;
using AGXIOME.Core.Logging;
using AGXIOME.Core.Tools;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Errors;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Logging;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AGXIOME.Core.Agent;

/// <summary>
/// Core agent implementation. Each agent = 1 LLMWIKY engine + tool registry + hooks + guardrails + memory.
/// Integrated with IAgxiomeLogger for per-module debug level control.
/// Supports Seq, NLog, Graylog, Serilog, Console providers.
/// </summary>
public sealed class AgxiomeAgent : IAgxiomeAgent, IAsyncDisposable
{
    private readonly ILLMWIKYEngine _engine;
    private readonly IToolRegistry _tools;
    private readonly ILogger<AgxiomeAgent> _log;
    private readonly IAgxiomeLogger _axiomeLog;
    private readonly AgentConfig _config;
    private readonly List<IAgentHook> _hooks = new();
    private readonly List<IGuardrail> _guardrails = new();
    private readonly List<IToolHook> _toolHooks = new();
    private readonly List<IAgentMiddleware> _middleware = new();
    private readonly ConcurrentDictionary<string, PausedRunState> _pausedRuns = new();

    private sealed record PausedRunState(AgentRequest Request, List<AgentMessage> History, int Iteration, CancellationTokenSource Cts);

    public string Id { get; }
    public AgentStatus Status { get; private set; } = AgentStatus.Idle;
    public IAgentMemory Memory { get; }
    public ILearningMachine? Learning { get; private set; }
    public string? SessionId { get; private set; }
    public ISessionState SessionState { get; } = new SessionState();

    internal AgxiomeAgent(string id, ILLMWIKYEngine engine, AgentConfig config, ILogger<AgxiomeAgent> log)
    {
        Id = id; _engine = engine; _config = config; _log = log;
        _axiomeLog = AgxiomeLoggerFactory.Default($"AGXIOME.Core.Agent.{id}");
        _tools = new ToolRegistry();
        Memory = new AgentMemoryManager(config.Persistence);
        _axiomeLog.Information("Agent '{0}' created (engine={1})", id, engine?.GetType().Name ?? "none");
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
        {
            _axiomeLog.Warning("Run cancelled before start"); 
            return CancelledResponse();
        }
        Status = AgentStatus.Thinking;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var runId = Guid.NewGuid().ToString("N")[..8];

        try
        {
            await ExecuteGuardrailsAsync(request.Prompt, GuardrailType.Input, ct);
            var hookCtx = new HookContext(Id, runId, HookPoint.PreRun, Input: request);
            await ExecuteHooksAsync(hookCtx, ct);
            var response = await ExecuteLLMLoopAsync(request, ct);
            await ExecuteGuardrailsAsync(response.Content, GuardrailType.Output, ct);
            var postCtx = hookCtx with { Point = HookPoint.PostRun, Output = response };
            await ExecuteHooksAsync(postCtx, ct);
            var bgCtx = hookCtx with { Point = HookPoint.BackgroundPost };
            _ = Task.Run(() => ExecuteHooksAsync(bgCtx, CancellationToken.None));
            if (Learning != null) await Learning.ProcessAsync(Id, request.Prompt, response.Content, ct);
            Status = AgentStatus.Completed;
            return response;
        }
        catch (Exception ex)
        {
            var agxEx = ex as AgxiomeException ?? AgxiomeException.FromCode(AgxiomeErrorCode.AE_RunFailed, ex);
            _axiomeLog.Exception(ex, "Agent '{Id}' run failed: {Msg}", Id, agxEx.UserMessage);
            _log.LogError(ex, "Agent {Id} failed: {Tech}", Id, agxEx.TechnicalMessage);
            Status = AgentStatus.Error;
            return ErrorResponse(agxEx);
        }
        finally
        {
            sw.Stop();
            _axiomeLog.Information("Run {RunId} -> {Status} ({Duration}ms)", runId, Status, sw.ElapsedMilliseconds);
        }
    }

    public async Task<T> RunAsync<T>(AgentRequest request, CancellationToken ct = default)
    {
        var response = await RunAsync(request, ct);
        try { return System.Text.Json.JsonSerializer.Deserialize<T>(response.Content) ?? throw new("Deserialize null"); }
        catch (System.Text.Json.JsonException ex) { _axiomeLog.Exception(ex, "Deserialize failed"); throw; }
    }

    public async IAsyncEnumerable<AgentChunk> RunStreamAsync(AgentRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        Status = AgentStatus.Streaming; int n = 0;
        await foreach (var c in _engine.InferStreamAsync(MapRequest(request), ct))
        { yield return new AgentChunk(c.Content, c.ToolCallId, c.IsFinal); n++; if (c.IsFinal) break; }
        Status = AgentStatus.Completed; _axiomeLog.Debug("Stream end ({N} chunks)", n);
    }

    public async IAsyncEnumerable<RunOutputEvent> RunEventStreamAsync(AgentRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new RunOutputEvent(RunEvent.RunStart);
        await foreach (var c in _engine.InferStreamAsync(MapRequest(request), ct))
        { if (c.IsToolCall) yield return new(RunEvent.ToolCallStart); yield return new(RunEvent.RunContent, c.Content); }
        yield return new RunOutputEvent(RunEvent.RunCompleted);
    }

    public async Task<AgentResponse> ContinueRunAsync(string runId, IReadOnlyList<HITLRequirement> requirements, CancellationToken ct = default)
    {
        if (!_pausedRuns.TryGetValue(runId, out var state))
            throw new InvalidOperationException($"No paused run: {runId}");
        using var lcts = CancellationTokenSource.CreateLinkedTokenSource(ct, state.Cts.Token);
        foreach (var r in requirements.Where(r => r.Status == HITLStatus.Approved))
            state.History.Add(new AgentMessage("tool_result", $"Tool {r.ToolName}: approved", ToolCallId: r.Id));
        return await ExecuteLLMLoopAsync(state.Request with { History = state.History.AsReadOnly() }, lcts.Token);
    }

    public void RegisterFunction<T>() where T : AgxiomeFunction { _tools.Register<T>(); _axiomeLog.Debug("Fn {T}", typeof(T).Name); }
    public void RegisterTool(IAgxiomeTool tool) { _tools.Register(tool); _axiomeLog.Debug("Tool '{N}'", tool.Name); }
    public void UseMiddleware(IAgentMiddleware m) => _middleware.Add(m);
    public void AddHook(IAgentHook h) => _hooks.Add(h);
    public void AddToolHook(IToolHook h) => _toolHooks.Add(h);
    public void AddGuardrail(IGuardrail g) => _guardrails.Add(g);
    public void SetOutputSchema<T>() where T : class { }
    public void SetInputSchema<T>() where T : class { }
    public void RequireConfirmationFor<T>() where T : AgxiomeFunction { }
    public void EnableLearning(LearningConfig? c = null) => Learning = new LearningMachine(c ?? new());

    private async Task<AgentResponse> ExecuteLLMLoopAsync(AgentRequest request, CancellationToken ct)
    {
        int iter = 0; string? last = null;
        var history = new List<AgentMessage>(request.History ?? []);

        while (iter < _config.MaxIterations && !ct.IsCancellationRequested)
        {
            iter++;
            LLMWIKYResponse llm;
            try { llm = await _engine.InferAsync(MapRequest(request with { History = history.AsReadOnly() }), ct); }
            catch (Exception ex) { _axiomeLog.Exception(ex, "LLM fail iter {I}", iter); throw; }

            if (llm.ToolName != null)
            {
                var tool = _tools.GetTool(llm.ToolName);
                if (tool == null) { history.Add(new("tool", $"Unknown {llm.ToolName}")); continue; }
                var tc = new ToolContext(Id, Guid.NewGuid().ToString());
                foreach (var h in _toolHooks) await h.BeforeExecuteAsync(tc, ct);
                if (tool is IAgxiomeExecutableTool et)
                {
                    try
                    {
                        var r = await et.ExecuteAsync(tc, ct);
                        history.Add(new("tool", r.Data?.ToString() ?? "", ToolName: llm.ToolName));
                        foreach (var h in _toolHooks) await h.AfterExecuteAsync(tc, r, ct);
                    }
                    catch (Exception ex) { _axiomeLog.Exception(ex, "Tool '{N}' threw", llm.ToolName); history.Add(new("tool", $"Error: {ex.Message}")); }
                }
            }
            else { last = llm.Content; break; }
        }
        if (iter >= _config.MaxIterations && last == null) _axiomeLog.Warning("Max iterations ({M}) reached", _config.MaxIterations);
        return new(last ?? "No response", iter >= _config.MaxIterations ? AgentStatus.Error : AgentStatus.Completed, Metadata: new(iter, iter, TimeSpan.Zero));
    }

    private async Task ExecuteGuardrailsAsync(string c, GuardrailType t, CancellationToken ct)
    {
        var ctx = new GuardrailContext(Id, SessionId, null);
        foreach (var g in _guardrails.Where(g => g.Type == t))
        {
            var r = await g.ValidateAsync(c, ctx, ct);
            if (!r.IsValid)
            {
                _axiomeLog.Warning("Guardrail '{N}' {A}: {R}", g.Name, r.OverrideSeverity is GuardrailSeverity.Block ? "BLOCK" : "FLAG", r.Reason);
                if (r.OverrideSeverity is null or GuardrailSeverity.Block) throw new($"Guardrail '{g.Name}' blocked: {r.Reason}");
            }
        }
    }

    private Task ExecuteHooksAsync(HookContext ctx, CancellationToken ct)
    {
        foreach (var h in _hooks.Where(h => h.Point == ctx.Point).OrderBy(h => h.Order))
        { if (h.RunInBackground) _ = Task.Run(() => h.ExecuteAsync(ctx, CancellationToken.None)); else h.ExecuteAsync(ctx, ct).GetAwaiter().GetResult(); }
        return Task.CompletedTask;
    }

    private static LLMWIKYRequest MapRequest(AgentRequest req)
    {
        var sp = "You are a helpful assistant.";
        if (req.Context?.AdditionalData?.TryGetValue("systemPrompt", out var v) == true) sp = v?.ToString() ?? sp;
        return new(sp, req.Prompt, req.History);
    }

    private static AgentResponse CancelledResponse() => new("Operazione annullata.", AgentStatus.Error, Metadata: new(0, 0, TimeSpan.Zero));
    private static AgentResponse ErrorResponse(AgxiomeException ex)
        => new($"[{ErrorCatalog.GetCode(ex.ErrorCode)}] {ex.UserMessage}", AgentStatus.Error, Metadata: new(0, 0, TimeSpan.Zero));

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    public void Dispose() { }
    public async ValueTask DisposeAsync() { GC.SuppressFinalize(this); await Task.CompletedTask; }
}

/// <summary>Thread-safe dictionary-backed session state.</summary>
public sealed class SessionState : ISessionState
{
    private readonly ConcurrentDictionary<string, object> _s = new();
    public T? Get<T>(string k) => _s.TryGetValue(k, out var v) ? (T?)v : default;
    public void Set<T>(string k, T v) => _s[k] = v!;
    public bool ContainsKey(string k) => _s.ContainsKey(k);
    public Dictionary<string, object> ToDictionary() => new(_s);
}
