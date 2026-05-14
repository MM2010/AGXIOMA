using System.Collections.Concurrent;

namespace AGXIOME.DynamicRuntime.Registry;

/// <summary>
/// Catalogo thread-safe degli agenti dinamici registrati per tutti gli utenti.
/// Stato in memoria; la persistenza su disco è gestita da AgentCacheStore.
/// </summary>
public sealed class DynamicAgentRegistry
{
    // Key: "{userId}::{agentId}"
    private readonly ConcurrentDictionary<string, DiscoveredAgentEntry> _entries = new();

    private static string Key(string userId, string agentId) => $"{userId}::{agentId}";

    /// <summary>Registra o aggiorna un agente nel catalogo.</summary>
    public void Register(string userId, DiscoveredAgentEntry entry)
    {
        var key = Key(userId, entry.AgentId);
        _entries[key] = entry;
    }

    /// <summary>Ritorna tutti gli agenti dell'utente, indipendentemente dal loro status.</summary>
    public IReadOnlyList<DiscoveredAgentEntry> GetAll(string userId)
    {
        var prefix = $"{userId}::";
        return _entries
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kv => kv.Value)
            .ToList();
    }

    /// <summary>Ritorna l'entry per un agente specifico, o null se non trovato.</summary>
    public DiscoveredAgentEntry? Get(string userId, string agentId) =>
        _entries.TryGetValue(Key(userId, agentId), out var entry) ? entry : null;

    /// <summary>Ritorna lo status corrente dell'agente, o null se non registrato.</summary>
    public AgentEntryStatus? GetStatus(string userId, string agentId) =>
        _entries.TryGetValue(Key(userId, agentId), out var entry) ? entry.Status : null;

    /// <summary>Aggiorna lo status di un agente esistente. No-op se non trovato.</summary>
    public void UpdateStatus(string userId, string agentId, AgentEntryStatus newStatus, string? error = null)
    {
        var key = Key(userId, agentId);
        if (_entries.TryGetValue(key, out var existing))
            _entries[key] = existing.WithStatus(newStatus, error);
    }

    /// <summary>Rimuove un agente dal catalogo. Ritorna true se era presente.</summary>
    public bool Remove(string userId, string agentId) =>
        _entries.TryRemove(Key(userId, agentId), out _);

    /// <summary>Numero totale di agenti registrati (tutti gli utenti).</summary>
    public int TotalCount => _entries.Count;
}
