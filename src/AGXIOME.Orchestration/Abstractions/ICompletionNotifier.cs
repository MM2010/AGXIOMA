using AGXIOME.Orchestration.Models;

namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Agnostic completion notification bus.
/// Implementable via SignalR, Redis pub/sub, in-memory Channel&lt;T&gt;, webhook, gRPC stream.
/// Sub-agents publish; the Master subscribes per plan.
/// </summary>
public interface ICompletionNotifier
{
    /// <summary>Called by the sub-agent (via NotifyCompletionTool) when its task is done.</summary>
    Task PublishAsync(TaskResult result, CancellationToken ct = default);

    /// <summary>
    /// Streams TaskResult items as they arrive for the given plan.
    /// The stream ends when the consumer cancels via <paramref name="ct"/>.
    /// </summary>
    IAsyncEnumerable<TaskResult> SubscribeAsync(Guid planId, CancellationToken ct = default);
}
