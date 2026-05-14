using System.Text.Json;

namespace AGXIOME.DynamicRuntime.Skills;

/// <summary>
/// Catalogo persistente delle skills importate per ogni utente.
/// Persiste in ~/.agxioma/skills/{userId}.json.
/// </summary>
public sealed class SkillsShRegistry
{
    private readonly string _basePath;
    private readonly Dictionary<string, List<UserSkillEntry>> _cache = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SkillsShRegistry(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".agxioma", "skills");
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>Importa una skill per un utente (status iniziale: Pending).</summary>
    public async Task ImportAsync(string userId, SkillDefinition definition, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var entries = await LoadAsync(userId);
            var existing = entries.FindIndex(e => e.SkillId == definition.Id);
            var entry = new UserSkillEntry(definition.Id, userId, definition, SkillStatus.Pending, DateTime.UtcNow);

            if (existing >= 0)
                entries[existing] = entry;
            else
                entries.Add(entry);

            await SaveAsync(userId, entries, ct);
            _cache[userId] = entries;
        }
        finally { _lock.Release(); }
    }

    /// <summary>Aggiorna lo status di una skill.</summary>
    public async Task SetStatusAsync(string userId, string skillId, SkillStatus status, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var entries = await LoadAsync(userId);
            var idx = entries.FindIndex(e => e.SkillId == skillId);
            if (idx >= 0)
            {
                entries[idx] = entries[idx] with { Status = status };
                await SaveAsync(userId, entries, ct);
                _cache[userId] = entries;
            }
        }
        finally { _lock.Release(); }
    }

    /// <summary>Ritorna tutte le skills di un utente.</summary>
    public async Task<IReadOnlyList<UserSkillEntry>> GetAllAsync(string userId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(userId, out var cached))
            return cached;

        await _lock.WaitAsync(ct);
        try
        {
            var entries = await LoadAsync(userId);
            _cache[userId] = entries;
            return entries;
        }
        finally { _lock.Release(); }
    }

    private async Task<List<UserSkillEntry>> LoadAsync(string userId)
    {
        if (_cache.TryGetValue(userId, out var cached))
            return cached;

        var path = GetPath(userId);
        if (!File.Exists(path))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<UserSkillEntry>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveAsync(string userId, List<UserSkillEntry> entries, CancellationToken ct)
    {
        var path = GetPath(userId);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, ct);
    }

    private string GetPath(string userId) =>
        Path.Combine(_basePath, $"{userId}.json");
}
