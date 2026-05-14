using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-03 — Team Storage Bridge
//
// Mirrors the Agent Storage Bridge pattern (SS-02) for team persistence:
//   • TeamSnapshot — serializable DTO capturing team identity, topology, mode,
//     and the config of every member agent (including default-tools list).
//   • ITeamStorageBridge — typed contract over any ICommandStore backend.
//   • TeamStorageBridge — in-memory indexed implementation (single-process).
//   • QueryableTeamStorageBridge — query-capable implementation (multi-process).
//
// Security note: team IDs must be server-generated (Guid.CreateVersion7).
// Never accept raw client-controlled IDs as canonical store keys.
// ─────────────────────────────────────────────────────────────────────────────

// ── Snapshot DTO ─────────────────────────────────────────────────────────────

/// <summary>Serializable capture of a team's identity and member configurations.</summary>
public record TeamSnapshot
{
    public string Id             { get; init; } = Guid.CreateVersion7().ToString();
    public string Name           { get; init; } = string.Empty;
    public string? Description   { get; init; }
    public TeamTopology Topology { get; init; }
    public TeamMode Mode         { get; init; }
    public TeamConfig Config     { get; init; } = new();

    /// <summary>Ordered list of agent configs that belong to this team (includes default tools).</summary>
    public IReadOnlyList<AgentConfig> AgentConfigs { get; init; } = Array.Empty<AgentConfig>();

    public DateTimeOffset SnapshotAt   { get; init; } = DateTimeOffset.UtcNow;
    public string         SchemaVersion { get; init; } = "1";
    public Dictionary<string, string>? ExtraProperties { get; init; }
}

// ── Storage DTO ──────────────────────────────────────────────────────────────

/// <summary>Flat DTO persisted in any backing store. Wraps a serialized TeamSnapshot.</summary>
public record TeamStorageEntry
{
    public string Id            { get; init; } = string.Empty;
    public string Name          { get; init; } = string.Empty;
    public string? UserId       { get; init; }
    public string SnapshotJson  { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "1";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? Tags         { get; init; }  // comma-separated, for lightweight filtering
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Typed bridge for persisting and retrieving team snapshots.</summary>
public interface ITeamStorageBridge
{
    /// <summary>Persist a snapshot. Creates or replaces an existing entry (upsert).</summary>
    Task SaveAsync(TeamSnapshot snapshot, string? userId = null,
                   IReadOnlyList<string>? tags = null, CancellationToken ct = default);

    /// <summary>Load a snapshot by team ID. Returns null if not found.</summary>
    Task<TeamSnapshot?> LoadAsync(string teamId, CancellationToken ct = default);

    /// <summary>Delete a persisted team by ID. No-op if not found.</summary>
    Task DeleteAsync(string teamId, CancellationToken ct = default);

    /// <summary>List all stored team entries (metadata only — no full snapshot deserialization).</summary>
    Task<IReadOnlyList<TeamStorageEntry>> ListAsync(string? userId = null, CancellationToken ct = default);

    /// <summary>Check if a team with the given ID exists in the store.</summary>
    Task<bool> ExistsAsync(string teamId, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Team storage bridge backed by any <see cref="ICommandStore"/> and an in-memory
/// index for list/exists queries. Suitable for single-process deployments.
/// </summary>
public sealed class TeamStorageBridge : ITeamStorageBridge
{
    private readonly ICommandStore _commands;

    // In-memory index: teamId → TeamStorageEntry (metadata only).
    // Keeps list/exists O(1) without requiring query capability from the backing store.
    // For multi-process deployments use QueryableTeamStorageBridge.
    private readonly Dictionary<string, TeamStorageEntry> _index = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    public TeamStorageBridge(ICommandStore commandStore)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        _commands = commandStore;
    }

    // ── ITeamStorageBridge ────────────────────────────────────────────────────

    public async Task SaveAsync(TeamSnapshot snapshot, string? userId = null,
                                IReadOnlyList<string>? tags = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var json = JsonSerializer.Serialize(snapshot, JsonOpts);
        var now  = DateTimeOffset.UtcNow;

        var entry = new TeamStorageEntry
        {
            Id            = snapshot.Id,
            Name          = snapshot.Name,
            UserId        = userId,
            SnapshotJson  = json,
            SchemaVersion = snapshot.SchemaVersion,
            CreatedAt     = _index.TryGetValue(snapshot.Id, out var existing)
                                ? existing.CreatedAt : now,
            UpdatedAt     = now,
            Tags          = tags is { Count: > 0 } ? string.Join(",", tags) : null,
        };

        if (_index.ContainsKey(snapshot.Id))
            await _commands.UpdateAsync(entry, ct);
        else
            await _commands.SaveAsync(entry, ct);

        // Update in-memory index after successful persist.
        _index[snapshot.Id] = entry;
    }

    public Task<TeamSnapshot?> LoadAsync(string teamId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);

        if (!_index.TryGetValue(teamId, out var meta))
            return Task.FromResult<TeamSnapshot?>(null);

        var snap = JsonSerializer.Deserialize<TeamSnapshot>(meta.SnapshotJson, JsonOpts);
        return Task.FromResult(snap);
    }

    public async Task DeleteAsync(string teamId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        await _commands.DeleteAsync<TeamStorageEntry>(teamId, ct);
        _index.Remove(teamId);
    }

    public Task<IReadOnlyList<TeamStorageEntry>> ListAsync(
        string? userId = null, CancellationToken ct = default)
    {
        IEnumerable<TeamStorageEntry> items = _index.Values;
        if (userId is not null)
            items = items.Where(e => e.UserId == userId);

        return Task.FromResult<IReadOnlyList<TeamStorageEntry>>(items.ToList());
    }

    public Task<bool> ExistsAsync(string teamId, CancellationToken ct = default)
        => Task.FromResult(_index.ContainsKey(teamId));
}

// ── Query-capable bridge ──────────────────────────────────────────────────────

/// <summary>
/// Extended bridge that uses <see cref="IQueryStore{TeamStorageEntry}"/> for
/// persistent list/exists queries — suitable for multi-process / stateless deployments.
/// </summary>
public sealed class QueryableTeamStorageBridge : ITeamStorageBridge
{
    private readonly ICommandStore _commands;
    private readonly IQueryStore<TeamStorageEntry> _queries;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    public QueryableTeamStorageBridge(
        ICommandStore commandStore,
        IQueryStore<TeamStorageEntry> queryStore)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        ArgumentNullException.ThrowIfNull(queryStore);
        _commands = commandStore;
        _queries  = queryStore;
    }

    public async Task SaveAsync(TeamSnapshot snapshot, string? userId = null,
                                IReadOnlyList<string>? tags = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var existing = await _queries.GetByIdAsync(snapshot.Id, ct);
        var now      = DateTimeOffset.UtcNow;
        var json     = JsonSerializer.Serialize(snapshot, JsonOpts);

        var entry = new TeamStorageEntry
        {
            Id            = snapshot.Id,
            Name          = snapshot.Name,
            UserId        = userId,
            SnapshotJson  = json,
            SchemaVersion = snapshot.SchemaVersion,
            CreatedAt     = existing?.CreatedAt ?? now,
            UpdatedAt     = now,
            Tags          = tags is { Count: > 0 } ? string.Join(",", tags) : null,
        };

        if (existing is null)
            await _commands.SaveAsync(entry, ct);
        else
            await _commands.UpdateAsync(entry, ct);
    }

    public async Task<TeamSnapshot?> LoadAsync(string teamId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        var entry = await _queries.GetByIdAsync(teamId, ct);
        if (entry is null) return null;
        return JsonSerializer.Deserialize<TeamSnapshot>(entry.SnapshotJson, JsonOpts);
    }

    public async Task DeleteAsync(string teamId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teamId);
        await _commands.DeleteAsync<TeamStorageEntry>(teamId, ct);
    }

    public async Task<IReadOnlyList<TeamStorageEntry>> ListAsync(
        string? userId = null, CancellationToken ct = default)
    {
        ISpecification<TeamStorageEntry> spec = userId is null
            ? new AllTeamsSpec()
            : new TeamsByUserSpec(userId);
        return await _queries.QueryAsync(spec, ct);
    }

    public async Task<bool> ExistsAsync(string teamId, CancellationToken ct = default)
        => await _queries.GetByIdAsync(teamId, ct) is not null;

    // ── Specifications ────────────────────────────────────────────────────────

    private sealed class AllTeamsSpec : ISpecification<TeamStorageEntry>
    {
        public System.Linq.Expressions.Expression<Func<TeamStorageEntry, bool>>
            ToExpression() => _ => true;
    }

    private sealed class TeamsByUserSpec(string userId) : ISpecification<TeamStorageEntry>
    {
        public System.Linq.Expressions.Expression<Func<TeamStorageEntry, bool>>
            ToExpression() => e => e.UserId == userId;
    }
}
