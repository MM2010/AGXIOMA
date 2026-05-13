using System.Collections.Concurrent;
using AGXIOME.DynamicRuntime.Compilation;
using AGXIOME.DynamicRuntime.Registry;

namespace AGXIOME.DynamicRuntime.Compilation;

/// <summary>
/// Compiler lazy thread-safe per agenti dinamici.
/// Il primo chiamante su un agentId avvia la compilazione; tutti i successivi
/// attendono la stessa Lazy[Task[CompiledAgent]] — zero race condition.
/// </summary>
public sealed class LazyAgentCompiler
{
    private readonly AgentCompiler _compiler;
    private readonly DynamicAgentRegistry _registry;
    private readonly string _basePath;

    // Key: "{userId}::{agentId}"
    private readonly ConcurrentDictionary<string, Lazy<Task<CompiledAgent?>>> _pending = new();

    public LazyAgentCompiler(AgentCompiler compiler, DynamicAgentRegistry registry, string? basePath = null)
    {
        _compiler = compiler;
        _registry = registry;
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agxioma");
    }

    /// <summary>
    /// Ritorna l'agente compilato per userId/agentId.
    /// Se non ancora compilato, avvia la compilazione e attende il risultato.
    /// Ritorna null se la compilazione fallisce.
    /// </summary>
    public Task<CompiledAgent?> GetOrCompileAsync(string userId, string agentId, CancellationToken ct = default)
    {
        var key = $"{userId}::{agentId}";

        var lazy = _pending.GetOrAdd(key, _ =>
            new Lazy<Task<CompiledAgent?>>(() => CompileInternalAsync(userId, agentId, ct)));

        return lazy.Value;
    }

    /// <summary>
    /// Invalida la cache per un agente (es. dopo NeedsRecompile).
    /// Il prossimo GetOrCompileAsync riavvierà la compilazione.
    /// </summary>
    public void Invalidate(string userId, string agentId)
    {
        var key = $"{userId}::{agentId}";
        _pending.TryRemove(key, out _);
    }

    private async Task<CompiledAgent?> CompileInternalAsync(string userId, string agentId, CancellationToken ct)
    {
        var entry = _registry.Get(userId, agentId);
        if (entry is null) return null;

        _registry.UpdateStatus(userId, agentId, AgentEntryStatus.Compiling);

        // Leggi sorgente dal file
        string source;
        try
        {
            source = await File.ReadAllTextAsync(entry.FilePath, ct);
        }
        catch (IOException ex)
        {
            _registry.UpdateStatus(userId, agentId, AgentEntryStatus.Failed, ex.Message);
            return null;
        }

        // Validazione statica
        var validation = AgentCodeValidator.Validate(source);
        if (!validation.Ok)
        {
            var err = string.Join("; ", validation.Violations);
            _registry.UpdateStatus(userId, agentId, AgentEntryStatus.Failed, $"Validation failed: {err}");
            return null;
        }

        // Compilazione
        var binPath = Path.Combine(_basePath, "agents", userId, "bin");
        var result = await _compiler.CompileAsync(source, agentId, binPath, ct);

        if (!result.Success)
        {
            var err = string.Join("; ", result.Errors);
            _registry.UpdateStatus(userId, agentId, AgentEntryStatus.Failed, err);
            return null;
        }

        _registry.UpdateStatus(userId, agentId, AgentEntryStatus.Compiled);
        return result.Agent;
    }
}
