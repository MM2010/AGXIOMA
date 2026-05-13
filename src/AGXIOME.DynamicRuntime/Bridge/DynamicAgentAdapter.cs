using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.DynamicRuntime.Bridge;

/// <summary>
/// Adatta un ISimpleDynamicAgent (interfaccia minimale utente) a IAgxiomeAgent
/// (interfaccia completa del framework AGXIOMA).
/// I metodi non supportati sono stub/no-op.
/// </summary>
public sealed class DynamicAgentAdapter : IAgxiomeAgent
{
    private readonly Interfaces.ISimpleDynamicAgent _inner;

    public DynamicAgentAdapter(Interfaces.ISimpleDynamicAgent inner) => _inner = inner;

    public string Id => _inner.AgentId;
    public AgentStatus Status { get; private set; } = AgentStatus.Idle;
    public IAgentMemory Memory { get; } = new NullAgentMemory();
    public ILearningMachine? Learning => null;
    public string? SessionId => null;
    public ISessionState SessionState { get; } = new NullSessionState();

    public async Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default)
    {
        Status = AgentStatus.Thinking;
        try
        {
            var output = await _inner.RunAsync(request.Prompt, ct);
            return new AgentResponse(output, AgentStatus.Completed);
        }
        finally { Status = AgentStatus.Idle; }
    }

    public async Task<T> RunAsync<T>(AgentRequest request, CancellationToken ct = default)
    {
        var response = await RunAsync(request, ct);
        return System.Text.Json.JsonSerializer.Deserialize<T>(response.Content)
            ?? throw new InvalidOperationException($"Cannot deserialize output of agent '{Id}' to {typeof(T).Name}");
    }

    public async IAsyncEnumerable<AgentChunk> RunStreamAsync(
        AgentRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var output = await _inner.RunAsync(request.Prompt, ct);
        yield return new AgentChunk(output, IsFinal: true);
    }

    public async IAsyncEnumerable<RunOutputEvent> RunEventStreamAsync(
        AgentRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new RunOutputEvent(RunEvent.RunStart);
        var output = await _inner.RunAsync(request.Prompt, ct);
        yield return new RunOutputEvent(RunEvent.RunCompleted, Content: output);
    }

    public Task<AgentResponse> ContinueRunAsync(
        string runId, IReadOnlyList<HITLRequirement> requirements, CancellationToken ct = default)
        => throw new NotSupportedException($"Agent '{Id}' does not support HITL continuation");

    public void RegisterFunction<T>() where T : AgxiomeFunction { }
    public void RegisterTool(IAgxiomeTool tool) { }
    public void UseMiddleware(IAgentMiddleware middleware) { }
    public void AddHook(IAgentHook hook) { }
    public void AddToolHook(IToolHook toolHook) { }
    public void AddGuardrail(IGuardrail guardrail) { }
    public void SetOutputSchema<T>() where T : class { }
    public void SetInputSchema<T>() where T : class { }
    public void RequireConfirmationFor<T>() where T : AgxiomeFunction { }
    public void EnableLearning(LearningConfig? config = null) { }
    public void Dispose() { }
}

file sealed class NullAgentMemory : IAgentMemory
{
    public Task SaveInteractionAsync(AgentInteraction interaction, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AgentInteraction>>(Array.Empty<AgentInteraction>());
    public Task ClearAsync(string agentId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
    public Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class NullSessionState : ISessionState
{
    public T? Get<T>(string key) => default;
    public void Set<T>(string key, T value) { }
    public bool ContainsKey(string key) => false;
    public Dictionary<string, object> ToDictionary() => [];
}