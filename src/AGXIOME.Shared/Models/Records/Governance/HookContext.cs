using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Shared.Models;

/// <summary>Context payload delivered to lifecycle hook handlers.</summary>
public record HookContext(
    string AgentId,
    string RunId,
    HookPoint Point,
    object? Input = null,
    object? Output = null,
    Exception? Error = null,
    ISessionState? SessionState = null
);
