using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Core agent contract: run, stream, tools, hooks, guardrails, learning.</summary>
public interface IAgxiomeAgent : IDisposable
{
    string Id { get; }
    AgentStatus Status { get; }
    IAgentMemory Memory { get; }
    ILearningMachine? Learning { get; }
    string? SessionId { get; }
    ISessionState SessionState { get; }

    Task<AgentResponse> RunAsync(AgentRequest request, CancellationToken ct = default);
    Task<T> RunAsync<T>(AgentRequest request, CancellationToken ct = default);
    IAsyncEnumerable<AgentChunk> RunStreamAsync(AgentRequest request, CancellationToken ct = default);
    IAsyncEnumerable<RunOutputEvent> RunEventStreamAsync(AgentRequest request, CancellationToken ct = default);
    Task<AgentResponse> ContinueRunAsync(string runId, IReadOnlyList<HITLRequirement> requirements, CancellationToken ct = default);

    void RegisterFunction<T>() where T : AgxiomeFunction;
    void RegisterTool(IAgxiomeTool tool);
    void UseMiddleware(IAgentMiddleware middleware);
    void AddHook(IAgentHook hook);
    void AddToolHook(IToolHook toolHook);
    void AddGuardrail(IGuardrail guardrail);
    void SetOutputSchema<T>() where T : class;
    void SetInputSchema<T>() where T : class;
    void RequireConfirmationFor<T>() where T : AgxiomeFunction;
    void EnableLearning(LearningConfig? config = null);
}
