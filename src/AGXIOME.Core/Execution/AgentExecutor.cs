using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Execution;

/// <summary>Handles agent execution modes: sync, streaming, event-stream, background+resume.</summary>
public sealed class AgentExecutor
{
    // Convenience runner that delegates to the agent's RunAsync
    public Task<AgentResponse> RunAsync(IAgxiomeAgent agent, AgentRequest request, CancellationToken ct = default)
        => agent.RunAsync(request, ct);

    // Convenience runner for streaming
    public IAsyncEnumerable<AgentChunk> RunStreamAsync(IAgxiomeAgent agent, AgentRequest request, CancellationToken ct = default)
        => agent.RunStreamAsync(request, ct);

    // Convenience runner for event stream
    public IAsyncEnumerable<RunOutputEvent> RunEventStreamAsync(IAgxiomeAgent agent, AgentRequest request, CancellationToken ct = default)
        => agent.RunEventStreamAsync(request, ct);
}
