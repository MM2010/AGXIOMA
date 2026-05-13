namespace AGXIOME.DynamicRuntime.Generation;

/// <summary>Risultato della generazione di un agente via LLM.</summary>
public sealed record AgentGenerationResult(
    string AgentId,
    string GeneratedCode,
    bool Success,
    IReadOnlyList<string> Errors,
    TimeSpan TotalTime,
    string? SavedFilePath = null
);
