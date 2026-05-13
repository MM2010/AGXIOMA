using AGXIOME.DynamicRuntime.Isolation;
using AGXIOME.DynamicRuntime.Registry;

namespace AGXIOME.DynamicRuntime.Persistence;

/// <summary>
/// Al boot, scansiona ~/.agxioma/agents/{userId}/bin/ e carica gli assembly .dll
/// la cui firma SHA256 corrisponde al sorgente .cs in src/.
/// Zero ricompilazione se il codice non è cambiato.
/// </summary>
public sealed class AgentCacheStore
{
    private readonly DynamicAgentRegistry _registry;
    private readonly string _basePath;

    public AgentCacheStore(DynamicAgentRegistry registry, string? basePath = null)
    {
        _registry = registry;
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agxioma");
    }

    /// <summary>
    /// Carica tutta la cache per un utente.
    /// Per ogni .dll trovata: verifica SHA256 vs sorgente; se match → LoadFromFile (Compiled);
    /// se mismatch o .cs mancante → NeedsRecompile.
    /// </summary>
    public async Task LoadAsync(string userId, CancellationToken ct = default)
    {
        var srcPath = Path.Combine(_basePath, "agents", userId, "src");
        var binPath = Path.Combine(_basePath, "agents", userId, "bin");

        if (!Directory.Exists(srcPath) || !Directory.Exists(binPath))
            return;

        foreach (var dllPath in Directory.GetFiles(binPath, "*.dll"))
        {
            if (ct.IsCancellationRequested) break;

            var agentId = Path.GetFileNameWithoutExtension(dllPath);
            var hashPath = Path.ChangeExtension(dllPath, ".sha256");
            var srcFile = Path.Combine(srcPath, $"{agentId}.cs");

            // Recupera entry dal registry (potrebbe già essere Discovered via FileScanner)
            var entry = _registry.Get(userId, agentId);
            if (entry is null) continue;

            // Verifica hash
            if (!File.Exists(hashPath) || !File.Exists(srcFile))
            {
                _registry.UpdateStatus(userId, agentId, AgentEntryStatus.NeedsRecompile, "Cache incomplete");
                continue;
            }

            var cachedHash = (await File.ReadAllTextAsync(hashPath, ct)).Trim();
            var sourceHash = HotFolder.AgentFileScanner.ComputeHash(
                await File.ReadAllTextAsync(srcFile, ct));

            if (cachedHash != sourceHash)
            {
                _registry.UpdateStatus(userId, agentId, AgentEntryStatus.NeedsRecompile, "Source changed");
                continue;
            }

            // Hash match → carica assembly direttamente senza Roslyn
            try
            {
                var context = new AgentAssemblyContext(agentId);
                var assembly = context.LoadFromAssemblyPath(dllPath);

                var agentType = assembly.GetTypes()
                    .FirstOrDefault(t => t.IsClass && !t.IsAbstract &&
                                         typeof(Interfaces.ISimpleDynamicAgent).IsAssignableFrom(t));

                if (agentType is null)
                {
                    context.Unload();
                    _registry.UpdateStatus(userId, agentId, AgentEntryStatus.NeedsRecompile, "No ISimpleDynamicAgent in cached dll");
                    continue;
                }

                _registry.UpdateStatus(userId, agentId, AgentEntryStatus.Compiled);
                // Nota: l'istanza viene creata by LazyAgentCompiler al primo utilizzo;
                // qui registriamo solo che la cache è valida.
            }
            catch (Exception ex)
            {
                _registry.UpdateStatus(userId, agentId, AgentEntryStatus.NeedsRecompile, ex.Message);
            }
        }
    }
}
