using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;
using System.Text.Json;

namespace AGXIOME.Core.Agent;

// ─────────────────────────────────────────────────────────────────────────────
// AgentSerializer — CE-04
// Serializes/deserializes agent configuration snapshots to/from dictionaries and
// JSON files. Enables save/load/clone workflows without referencing any specific
// agent implementation class.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A portable snapshot of an agent's configuration and runtime-observable state.
/// Does NOT capture in-flight run state — use <see cref="BackgroundRunWorker"/> for that.</summary>
public record AgentSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required AgentConfig Config { get; init; }
    public IReadOnlyDictionary<string, object>? ExtraProperties { get; init; }
    public DateTimeOffset SnapshotAt { get; init; } = DateTimeOffset.UtcNow;
    public string SchemaVersion { get; init; } = "1";
}

/// <summary>Serializes and deserializes <see cref="AgentSnapshot"/> instances.
/// Stateless utility — no side effects, safe to use as a singleton.</summary>
public sealed class AgentSerializer
{
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    // ── Dictionary round-trip ─────────────────────────────────────────────────

    /// <summary>Convert a snapshot to a string-keyed property dictionary for transport or inspection.</summary>
    public Dictionary<string, object?> ToDictionary(AgentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new Dictionary<string, object?>
        {
            ["id"]            = snapshot.Id,
            ["name"]          = snapshot.Name,
            ["description"]   = snapshot.Description,
            ["schemaVersion"] = snapshot.SchemaVersion,
            ["snapshotAt"]    = snapshot.SnapshotAt.ToString("O"),
            ["config"]        = JsonSerializer.SerializeToDocument(snapshot.Config, _jsonOpts),
            ["extra"]         = snapshot.ExtraProperties is null ? null
                                : JsonSerializer.SerializeToDocument(snapshot.ExtraProperties, _jsonOpts),
        };
    }

    /// <summary>Reconstruct a snapshot from a property dictionary previously produced by
    /// <see cref="ToDictionary"/>. Throws <see cref="InvalidOperationException"/> on invalid input.</summary>
    public AgentSnapshot FromDictionary(IReadOnlyDictionary<string, object?> dict)
    {
        ArgumentNullException.ThrowIfNull(dict);
        var id     = Required<string>(dict, "id");
        var name   = Required<string>(dict, "name");
        var schema = dict.TryGetValue("schemaVersion", out var sv) ? sv?.ToString() ?? "1" : "1";
        var ts     = dict.TryGetValue("snapshotAt", out var ta) && ta is string s
                     ? DateTimeOffset.Parse(s) : DateTimeOffset.UtcNow;

        AgentConfig config;
        if (dict.TryGetValue("config", out var cfgRaw) && cfgRaw is JsonDocument cfgDoc)
            config = cfgDoc.Deserialize<AgentConfig>(_jsonOpts) ?? new AgentConfig();
        else
            config = new AgentConfig();

        Dictionary<string, object>? extra = null;
        if (dict.TryGetValue("extra", out var extraRaw) && extraRaw is JsonDocument extraDoc)
            extra = extraDoc.Deserialize<Dictionary<string, object>>(_jsonOpts);

        return new AgentSnapshot
        {
            Id              = id,
            Name            = name,
            Description     = dict.TryGetValue("description", out var d) ? d?.ToString() : null,
            Config          = config,
            ExtraProperties = extra,
            SnapshotAt      = ts,
            SchemaVersion   = schema,
        };
    }

    // ── File I/O ──────────────────────────────────────────────────────────────

    /// <summary>Serialize a snapshot to a JSON file at <paramref name="filePath"/>.</summary>
    public async Task SaveAsync(AgentSnapshot snapshot, string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var json = JsonSerializer.Serialize(snapshot, _jsonOpts);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    /// <summary>Load a snapshot from a JSON file. Throws <see cref="FileNotFoundException"/> if the file is absent.</summary>
    public async Task<AgentSnapshot> LoadAsync(string filePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Agent snapshot file not found: {filePath}");

        var json = await File.ReadAllTextAsync(filePath, ct);
        return JsonSerializer.Deserialize<AgentSnapshot>(json, _jsonOpts)
               ?? throw new InvalidOperationException($"Failed to deserialize snapshot from: {filePath}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static T Required<T>(IReadOnlyDictionary<string, object?> dict, string key) where T : class
    {
        if (!dict.TryGetValue(key, out var val) || val is null)
            throw new InvalidOperationException($"Required field '{key}' is missing from snapshot dictionary");
        if (val is T typed) return typed;
        return (T)Convert.ChangeType(val, typeof(T));
    }
}
