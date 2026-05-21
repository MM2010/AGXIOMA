using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Core.Agent;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Storage.Agent;

// ─────────────────────────────────────────────────────────────────────────────
// SS-02 — Agent Storage Bridge
//
// Provides save/load/list/delete of AgentSnapshot instances through any
// ICommandStore + IQueryStore<AgentStorageEntry> backend.
//
// Design decisions:
//   • AgentStorageEntry is a flat DTO suitable for any KV/document store.
//     The raw JSON of AgentSnapshot is stored as a string blob — zero coupling
//     to storage schema evolution.
//   • IAgentStorageBridge is the typed contract. Inject any store implementation
//     (SQLite, PostgreSQL, Redis, JSON file, …) from AGXIOME.Storage.
//   • AgentSerializer handles the snapshot ↔ JSON round-trip deterministically.
//
// Security note: agent IDs must be server-generated (Guid.CreateVersion7).
// Never accept raw client-controlled IDs as canonical store keys.
// ─────────────────────────────────────────────────────────────────────────────

// ── Storage DTO ──────────────────────────────────────────────────────────────

/// <summary>Flat DTO stored in any backing store. Wraps a serialized AgentSnapshot.</summary>
public record AgentStorageEntry
{
    public string Id          { get; init; } = string.Empty;
    public string Name        { get; init; } = string.Empty;
    public string? UserId     { get; init; }
    public string SnapshotJson { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "1";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? Tags     { get; init; }  // comma-separated, for lightweight filtering
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Typed bridge for persisting and retrieving agent snapshots.</summary>
public interface IAgentStorageBridge
{
    /// <summary>Persist a snapshot. Creates a new entry or replaces an existing one (upsert).</summary>
    Task SaveAsync(AgentSnapshot snapshot, string? userId = null,
                   IReadOnlyList<string>? tags = null, CancellationToken ct = default);

    /// <summary>Load a snapshot by agent ID. Returns null if not found.</summary>
    Task<AgentSnapshot?> LoadAsync(string agentId, CancellationToken ct = default);

    /// <summary>Delete a persisted agent by ID. No-op if not found.</summary>
    Task DeleteAsync(string agentId, CancellationToken ct = default);

    /// <summary>List all stored agent entries (lightweight metadata — no full snapshot deserialization).</summary>
    Task<IReadOnlyList<AgentStorageEntry>> ListAsync(string? userId = null, CancellationToken ct = default);

    /// <summary>Check if an agent with the given ID exists in the store.</summary>
    Task<bool> ExistsAsync(string agentId, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Agent storage bridge backed by any <see cref="ICommandStore"/> and an optional
/// in-memory index for list/exists queries (suitable for stores that expose only KV).
///
/// For stores that also implement <see cref="IQueryStore{T}"/>, override <see cref="ListAsync"/>
/// by subclassing or use a purpose-built query store directly.
/// </summary>
public sealed class AgentStorageBridge : IAgentStorageBridge
{
    private readonly ICommandStore _commands;
    private readonly AgentSerializer _serializer;

    // In-memory index: agentId → AgentStorageEntry (metadata only, no snapshot body).
    // This keeps list/exists O(1) for in-process usage without requiring query capability
    // from the backing store. For multi-process deployments use a dedicated query projection.
    private readonly Dictionary<string, AgentStorageEntry> _index = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented            = false,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull,
        Converters               = { new JsonStringEnumConverter() },
    };

    public AgentStorageBridge(ICommandStore commandStore, AgentSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        _commands   = commandStore;
        _serializer = serializer ?? new AgentSerializer();
    }

    // ── IAgentStorageBridge ───────────────────────────────────────────────────

    public async Task SaveAsync(AgentSnapshot snapshot, string? userId = null,
                                IReadOnlyList<string>? tags = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var json  = JsonSerializer.Serialize(snapshot, JsonOpts);
        var now   = DateTimeOffset.UtcNow;
        var entry = new AgentStorageEntry
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

    public async Task<AgentSnapshot?> LoadAsync(string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

        if (!_index.TryGetValue(agentId, out var meta))
            return null;

        // Re-read from the command store via a save → the stored json is
        // in the entry we wrote. For stores that don't support GetById on
        // ICommandStore, we cache the last snapshot in the index entry.
        // (Full query-capable stores should override this with a GetById call.)
        return JsonSerializer.Deserialize<AgentSnapshot>(meta.SnapshotJson, JsonOpts);
    }

    public async Task DeleteAsync(string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        await _commands.DeleteAsync<AgentStorageEntry>(agentId, ct);
        _index.Remove(agentId);
    }

    public Task<IReadOnlyList<AgentStorageEntry>> ListAsync(
        string? userId = null, CancellationToken ct = default)
    {
        IEnumerable<AgentStorageEntry> items = _index.Values;
        if (userId is not null)
            items = items.Where(e => e.UserId == userId);

        return Task.FromResult<IReadOnlyList<AgentStorageEntry>>(items.ToList());
    }

    public Task<bool> ExistsAsync(string agentId, CancellationToken ct = default)
        => Task.FromResult(_index.ContainsKey(agentId));
}

// ── Query-capable bridge ──────────────────────────────────────────────────────

/// <summary>
/// Extended bridge that uses <see cref="IQueryStore{AgentStorageEntry}"/> for
/// persistent list/exists queries — suitable for multi-process / stateless deployments
/// where the in-memory index is not reliable.
/// </summary>
public sealed class QueryableAgentStorageBridge : IAgentStorageBridge
{
    private readonly ICommandStore _commands;
    private readonly IQueryStore<AgentStorageEntry> _queries;
    private readonly AgentSerializer _serializer;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented            = false,
        DefaultIgnoreCondition   = JsonIgnoreCondition.WhenWritingNull,
        Converters               = { new JsonStringEnumConverter() },
    };

    public QueryableAgentStorageBridge(
        ICommandStore commandStore,
        IQueryStore<AgentStorageEntry> queryStore,
        AgentSerializer? serializer = null)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        ArgumentNullException.ThrowIfNull(queryStore);
        _commands   = commandStore;
        _queries    = queryStore;
        _serializer = serializer ?? new AgentSerializer();
    }

    public async Task SaveAsync(AgentSnapshot snapshot, string? userId = null,
                                IReadOnlyList<string>? tags = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var existing = await _queries.GetByIdAsync(snapshot.Id, ct);
        var now      = DateTimeOffset.UtcNow;
        var json     = JsonSerializer.Serialize(snapshot, JsonOpts);

        var entry = new AgentStorageEntry
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

    public async Task<AgentSnapshot?> LoadAsync(string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        var entry = await _queries.GetByIdAsync(agentId, ct);
        if (entry is null) return null;
        return JsonSerializer.Deserialize<AgentSnapshot>(entry.SnapshotJson, JsonOpts);
    }

    public async Task DeleteAsync(string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        await _commands.DeleteAsync<AgentStorageEntry>(agentId, ct);
    }

    public async Task<IReadOnlyList<AgentStorageEntry>> ListAsync(
        string? userId = null, CancellationToken ct = default)
    {
        // Use a userId-scoped specification or return all if no userId filter.
        ISpecification<AgentStorageEntry> spec = userId is null
            ? new AllAgentsSpec()
            : new AgentsByUserSpec(userId);
        return await _queries.QueryAsync(spec, ct);
    }

    public async Task<bool> ExistsAsync(string agentId, CancellationToken ct = default)
        => await _queries.GetByIdAsync(agentId, ct) is not null;

    // ── Specifications ────────────────────────────────────────────────────────

    private sealed class AllAgentsSpec : ISpecification<AgentStorageEntry>
    {
        public System.Linq.Expressions.Expression<Func<AgentStorageEntry, bool>>
            ToExpression() => _ => true;
    }

    private sealed class AgentsByUserSpec(string userId) : ISpecification<AgentStorageEntry>
    {
        public System.Linq.Expressions.Expression<Func<AgentStorageEntry, bool>>
            ToExpression() => e => e.UserId == userId;
    }
}
