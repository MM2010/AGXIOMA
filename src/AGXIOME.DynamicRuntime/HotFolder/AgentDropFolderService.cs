using AGXIOME.DynamicRuntime.Registry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AGXIOME.DynamicRuntime.HotFolder;

/// <summary>
/// IHostedService che avvia e gestisce un AgentDropFolder per ogni utente registrato.
/// Riceve eventi file, chiama AgentFileScanner e aggiorna DynamicAgentRegistry.
/// </summary>
public sealed class AgentDropFolderService : IHostedService, IDisposable
{
    private readonly DynamicAgentRegistry _registry;
    private readonly ILogger<AgentDropFolderService> _logger;
    private readonly string _basePath;
    private readonly List<AgentDropFolder> _watchers = [];

    // Lista utenti da monitorare. Può essere espansa a runtime tramite AddUser().
    private readonly List<string> _userIds = [];

    public AgentDropFolderService(
        DynamicAgentRegistry registry,
        ILogger<AgentDropFolderService> logger,
        string? basePath = null)
    {
        _registry = registry;
        _logger = logger;
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agxioma");
    }

    /// <summary>Aggiunge un utente da monitorare (può essere chiamato prima di StartAsync).</summary>
    public void AddUser(string userId)
    {
        if (!_userIds.Contains(userId))
            _userIds.Add(userId);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var userId in _userIds)
            StartWatcherForUser(userId);

        return Task.CompletedTask;
    }

    private void StartWatcherForUser(string userId)
    {
        var folder = new AgentDropFolder(userId, _basePath);
        folder.OnFileEvent += HandleFileEventAsync;
        folder.Start();
        _watchers.Add(folder);

        // Scansione iniziale dei file già presenti
        _ = Task.Run(() => ScanExistingFilesAsync(userId, folder.SrcPath));

        _logger.LogInformation("[DynamicRuntime] Watching {Path} for user {UserId}", folder.SrcPath, userId);
    }

    private async Task ScanExistingFilesAsync(string userId, string srcPath)
    {
        if (!Directory.Exists(srcPath)) return;

        foreach (var file in Directory.GetFiles(srcPath, "*.cs"))
        {
            var meta = await AgentFileScanner.ScanAsync(file);
            if (meta is null) continue;

            RegisterAgent(userId, meta);
        }
    }

    private async Task HandleFileEventAsync(AgentFileEvent ev, CancellationToken ct)
    {
        switch (ev.Kind)
        {
            case AgentFileEventKind.Discovered:
            {
                var meta = await AgentFileScanner.ScanAsync(ev.FilePath, ct: ct);
                if (meta is null)
                {
                    _logger.LogWarning("[DynamicRuntime] File {File} has no [AgxiomeAgent] attribute — skipped", ev.FilePath);
                    return;
                }
                RegisterAgent(ev.UserId, meta);
                _logger.LogInformation("[DynamicRuntime] Discovered agent '{AgentId}' from {File}", meta.AgentId, ev.FilePath);
                break;
            }
            case AgentFileEventKind.Modified:
            {
                var meta = await AgentFileScanner.ScanAsync(ev.FilePath, ct: ct);
                if (meta is null) return;

                var existing = _registry.Get(ev.UserId, meta.AgentId);
                if (existing is not null && existing.SourceHash == meta.SourceHash)
                    return; // Nessun cambiamento reale

                _registry.UpdateStatus(ev.UserId, meta.AgentId, AgentEntryStatus.NeedsRecompile);
                // Re-registra con nuovo hash
                RegisterAgent(ev.UserId, meta, AgentEntryStatus.NeedsRecompile);
                _logger.LogInformation("[DynamicRuntime] Modified agent '{AgentId}' — marked NeedsRecompile", meta.AgentId);
                break;
            }
            case AgentFileEventKind.Removed:
            {
                // Recupera agentId dalla path
                var fileName = Path.GetFileNameWithoutExtension(ev.FilePath);
                // Non possiamo fare scan (file rimosso), usiamo il fileName come fallback
                _registry.Remove(ev.UserId, fileName);
                _logger.LogInformation("[DynamicRuntime] Removed file {File} from registry", ev.FilePath);
                break;
            }
        }
    }

    private void RegisterAgent(string userId, AgentFileMetadata meta, AgentEntryStatus status = AgentEntryStatus.Discovered)
    {
        var entry = new DiscoveredAgentEntry(
            AgentId: meta.AgentId,
            Description: meta.Description,
            FilePath: meta.FilePath,
            SourceHash: meta.SourceHash,
            Status: status,
            DiscoveredAt: DateTime.UtcNow
        );
        _registry.Register(userId, entry);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var w in _watchers)
            w.Dispose();
        _watchers.Clear();
    }
}
