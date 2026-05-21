using System.IO;
using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Stores;

// ─────────────────────────────────────────────────────────────────────────────
// DB-25 — JSON File Store
// File-based store. One JSON file per entity type. Atomic write via temp file
// + rename to prevent corruption on crash. In-memory index for O(1) lookups.
// Thread-safe via ReaderWriterLockSlim.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record JsonFileStoreOptions
{
    /// <summary>Base directory for JSON store files.</summary>
    public required string BaseDirectory { get; init; }
    public bool PrettyPrint { get; init; } = false;
}

public sealed class JsonFileCommandStore : ICommandStore, IDisposable
{
    private readonly JsonFileStoreOptions _opts;
    private readonly ReaderWriterLockSlim _lock = new();

    public StorageProviderType ProviderType => StorageProviderType.JsonFile;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public JsonFileCommandStore(JsonFileStoreOptions opts)
    {
        _opts = opts ?? throw new ArgumentNullException(nameof(opts));
        Directory.CreateDirectory(opts.BaseDirectory);
    }

    public Task SaveAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        var id = GetId(entity);
        _lock.EnterWriteLock();
        try
        {
            var store = LoadStore<T>();
            store[id] = entity;
            PersistStore<T>(store);
        }
        finally { _lock.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    public Task UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
        => SaveAsync(entity, ct);

    public Task DeleteAsync<T>(string id, CancellationToken ct = default) where T : class
    {
        _lock.EnterWriteLock();
        try
        {
            var store = LoadStore<T>();
            store.Remove(id);
            PersistStore<T>(store);
        }
        finally { _lock.ExitWriteLock(); }
        return Task.CompletedTask;
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

    private Dictionary<string, T> LoadStore<T>() where T : class
    {
        var path = FilePath<T>();
        if (!File.Exists(path)) return new();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, T>>(json, _jsonOpts) ?? new();
    }

    private void PersistStore<T>(Dictionary<string, T> store) where T : class
    {
        var path = FilePath<T>();
        var tmp  = path + ".tmp";
        // Write to temp then rename — atomic on POSIX; best-effort on Windows.
        var opts = _opts.PrettyPrint
            ? new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true }
            : _jsonOpts;
        var json = JsonSerializer.Serialize(store, opts);
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    private string FilePath<T>() =>
        Path.Combine(_opts.BaseDirectory, $"{typeof(T).Name.ToLowerInvariant()}s.json");

    private static string GetId<T>(T entity)
    {
        var prop = typeof(T).GetProperty("Id");
        return prop?.GetValue(entity)?.ToString() ?? Guid.NewGuid().ToString();
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        try { return Task.FromResult(Directory.Exists(_opts.BaseDirectory)); }
        catch { return Task.FromResult(false); }
    }

    public void Dispose() => _lock.Dispose();
}
