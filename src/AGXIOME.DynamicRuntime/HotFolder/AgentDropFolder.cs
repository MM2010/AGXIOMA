namespace AGXIOME.DynamicRuntime.HotFolder;

/// <summary>Evento emesso quando un file .cs viene rilevato o modificato nella hot-folder.</summary>
public sealed record AgentFileEvent(
    AgentFileEventKind Kind,
    string FilePath,
    string UserId
);

public enum AgentFileEventKind { Discovered, Modified, Removed }

/// <summary>
/// Monitora la cartella ~/.agxioma/agents/{userId}/src/ con FileSystemWatcher.
/// Debounce 300ms per evitare eventi multipli su salvataggio editor.
/// Thread-safe: tutti gli handler vengono invocati sul thread pool.
/// </summary>
public sealed class AgentDropFolder : IDisposable
{
    private readonly string _userId;
    private readonly string _srcPath;
    private FileSystemWatcher? _watcher;

    // Debounce: agentId → ultimo timestamp evento
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _debounce = new();
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(300);

    public event Func<AgentFileEvent, CancellationToken, Task>? OnFileEvent;

    public AgentDropFolder(string userId, string basePath)
    {
        _userId = userId;
        _srcPath = Path.Combine(basePath, "agents", userId, "src");
        Directory.CreateDirectory(_srcPath);
    }

    public void Start()
    {
        _watcher = new FileSystemWatcher(_srcPath, "*.cs")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) => HandleEvent(e.FullPath, AgentFileEventKind.Discovered);
        _watcher.Changed += (_, e) => HandleEvent(e.FullPath, AgentFileEventKind.Modified);
        _watcher.Deleted += (_, e) => HandleEvent(e.FullPath, AgentFileEventKind.Removed);
        _watcher.Renamed += (_, e) =>
        {
            HandleEvent(e.OldFullPath, AgentFileEventKind.Removed);
            HandleEvent(e.FullPath, AgentFileEventKind.Discovered);
        };
    }

    private void HandleEvent(string filePath, AgentFileEventKind kind)
    {
        var now = DateTime.UtcNow;

        // Debounce: se l'evento è troppo recente, aggiorna timestamp e salta
        if (kind != AgentFileEventKind.Removed)
        {
            var last = _debounce.GetOrAdd(filePath, DateTime.MinValue);
            if (now - last < DebounceWindow)
            {
                _debounce[filePath] = now;
                return;
            }
            _debounce[filePath] = now;
        }
        else
        {
            _debounce.TryRemove(filePath, out _);
        }

        // Fire async senza bloccare il thread del watcher
        _ = Task.Run(async () =>
        {
            // Piccolo delay extra per Removed/Modified: assicura che il file sia stato scritto
            if (kind == AgentFileEventKind.Modified)
                await Task.Delay(50);

            var ev = new AgentFileEvent(kind, filePath, _userId);
            if (OnFileEvent is { } handler)
                await handler(ev, CancellationToken.None);
        });
    }

    public string SrcPath => _srcPath;

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }
}
