using AGXIOME.Orchestration.Models;

namespace AGXIOME.Orchestration.Abstractions;

/// <summary>
/// Agnostic sub-agent factory and lifecycle contract.
/// Implementable via BackgroundRunWorker, HTTP remote, SignalR, WASM sandbox, etc.
/// JobId returned is opaque — caller must not assume its format.
/// </summary>
public interface IAgentSpawner
{
    /// <summary>
    /// Starts a sub-agent for the given task spec.
    /// Returns an opaque job ID for tracking and cancellation.
    /// </summary>
    Task<string> SpawnAsync(TaskSpec spec, CancellationToken ct = default);

    /// <summary>Requests cancellation of a running or queued sub-agent job.</summary>
    Task CancelAsync(string jobId, CancellationToken ct = default);

    /// <summary>Returns true if the job is still in a non-terminal state.</summary>
    Task<bool> IsActiveAsync(string jobId, CancellationToken ct = default);
}
