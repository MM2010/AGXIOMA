namespace AGXIOME.DynamicRuntime.Generation;

/// <summary>
/// Parametri per la generazione di codice agente via LLM.
/// </summary>
public sealed record AgentGenerationRequest(
    string UserId,
    string Description,
    string? ExampleInput = null,
    string? ExampleOutput = null,
    string[]? AllowedNamespaces = null
);
