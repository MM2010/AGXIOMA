namespace AGXIOME.DynamicRuntime.Interfaces;

/// <summary>
/// Contratto minimo che ogni agente dinamico (user-drop o LLM-generated) deve implementare.
/// Volutamente semplice: nessuna dipendenza da AGXIOME.Shared per massima portabilità.
/// </summary>
public interface ISimpleDynamicAgent
{
    /// <summary>Identificatore univoco dell'agente (da [AgxiomeAgent]).</summary>
    string AgentId { get; }

    /// <summary>Descrizione leggibile dell'agente (da [AgxiomeAgent]).</summary>
    string Description { get; }

    /// <summary>Esegue l'agente con un input testuale e ritorna l'output testuale.</summary>
    Task<string> RunAsync(string input, CancellationToken ct = default);
}
