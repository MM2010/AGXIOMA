namespace AGXIOME.DynamicRuntime.Registry;

/// <summary>Stato del ciclo di vita di un agente nel catalogo.</summary>
public enum AgentEntryStatus
{
    /// <summary>File .cs rilevato, metadati estratti. Assembly NON compilato.</summary>
    Discovered,

    /// <summary>Compilazione in corso (lazy al primo RunAsync).</summary>
    Compiling,

    /// <summary>Assembly compilato e pronto all'uso.</summary>
    Compiled,

    /// <summary>Compilazione fallita. Vedi <see cref="DiscoveredAgentEntry.LastError"/>.</summary>
    Failed,

    /// <summary>File sorgente modificato, assembly cache non più valido. Ricompilerà al prossimo uso.</summary>
    NeedsRecompile
}

/// <summary>
/// Entry immutabile nel catalogo agenti dinamici.
/// Rappresenta un agente rilevato via hot-folder o generato via LLM.
/// </summary>
public sealed record DiscoveredAgentEntry(
    string AgentId,
    string Description,
    string FilePath,
    string SourceHash,
    AgentEntryStatus Status,
    DateTime DiscoveredAt,
    string? LastError = null
)
{
    /// <summary>Ritorna una copia con status aggiornato.</summary>
    public DiscoveredAgentEntry WithStatus(AgentEntryStatus newStatus, string? error = null) =>
        this with { Status = newStatus, LastError = error };
}
