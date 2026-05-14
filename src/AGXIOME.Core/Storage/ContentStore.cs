using System.Text.Json;
using System.Text.Json.Serialization;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

// ─────────────────────────────────────────────────────────────────────────────
// SS-07 — Contents DB
//
// Typed storage for knowledge content entries — text, markdown, code, etc.
// Designed to back a server-side knowledge base that feeds RAG pipelines.
//
// Design:
//   • ContentEntry — flat, indexable DTO; DocumentType enum (no magic strings).
//   • ContentTag — enum-driven primary classification tag.
//   • IContentStore — typed contract: upsert / get / delete / search (by tag / source).
//   • ContentStore — backed by ICommandStore + in-memory full-text / metadata index.
//     For production use pair with a vector store (IKnowledgeBase + RAG pipeline).
//
// Security: content bodies are stored server-side only — never echoed to
// unauthenticated clients. SourceUri must be server-controlled.
// ─────────────────────────────────────────────────────────────────────────────

// ── Enum: content classification ─────────────────────────────────────────────

/// <summary>Primary content classification tag — enum-first, no magic strings.</summary>
public enum ContentTag
{
    Unclassified,
    Guide,
    Reference,
    Tutorial,
    Example,
    Policy,
    Faq,
    Changelog,
    Api,
    DataSchema,
}

// ── Storage DTO ──────────────────────────────────────────────────────────────

/// <summary>Persisted knowledge content entry.</summary>
public record ContentEntry
{
    /// <summary>Unique ID (server-generated).</summary>
    public string Id           { get; init; } = string.Empty;

    public string Title        { get; init; } = string.Empty;

    /// <summary>Raw body — plaintext, markdown, code, etc.</summary>
    public string Body         { get; init; } = string.Empty;

    /// <summary>Content format — maps to DocumentType for reader pipeline compatibility.</summary>
    public DocumentType DocType { get; init; } = DocumentType.PlainText;

    /// <summary>Primary classification tag.</summary>
    public ContentTag Tag      { get; init; } = ContentTag.Unclassified;

    /// <summary>Optional namespace / collection identifier (e.g. "product-docs", "faq").</summary>
    public string? Namespace   { get; init; }

    /// <summary>Optional canonical source URI (server-controlled; never from client input).</summary>
    public string? SourceUri   { get; init; }

    public Dictionary<string, string>? Metadata { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public string SchemaVersion { get; init; } = "1";
}

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Typed storage contract for knowledge content entries.</summary>
public interface IContentStore
{
    /// <summary>Upsert a content entry (insert or replace by ID).</summary>
    Task UpsertAsync(ContentEntry entry, CancellationToken ct = default);

    /// <summary>Retrieve a content entry by ID. Returns null if not found.</summary>
    Task<ContentEntry?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Delete a content entry. No-op if not found.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Search entries by namespace, tag, or title keyword (all filters are AND-combined).</summary>
    Task<IReadOnlyList<ContentEntry>> SearchAsync(
        string? ns        = null,
        ContentTag? tag   = null,
        string? keyword   = null,
        int     limit     = 50,
        CancellationToken ct = default);

    /// <summary>List all entries in a given namespace. Returns all if namespace is null.</summary>
    Task<IReadOnlyList<ContentEntry>> ListAsync(
        string? ns = null, CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Content store backed by <see cref="ICommandStore"/> and an in-memory index.
/// Provides title-keyword and metadata filtering without a full-text engine.
/// For production pair with a vector store for semantic retrieval.
/// </summary>
public sealed class ContentStore : IContentStore
{
    private readonly ICommandStore _commands;

    // In-memory index: id → ContentEntry.
    private readonly Dictionary<string, ContentEntry> _index = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented          = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters             = { new JsonStringEnumConverter() },
    };

    public ContentStore(ICommandStore commandStore)
    {
        ArgumentNullException.ThrowIfNull(commandStore);
        _commands = commandStore;
    }

    // ── IContentStore ─────────────────────────────────────────────────────────

    public async Task UpsertAsync(ContentEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Id);

        if (_index.ContainsKey(entry.Id))
            await _commands.UpdateAsync(entry, ct);
        else
            await _commands.SaveAsync(entry, ct);

        _index[entry.Id] = entry;
    }

    public Task<ContentEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _index.TryGetValue(id, out var entry);
        return Task.FromResult(entry);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _commands.DeleteAsync<ContentEntry>(id, ct);
        _index.Remove(id);
    }

    public Task<IReadOnlyList<ContentEntry>> SearchAsync(
        string? ns        = null,
        ContentTag? tag   = null,
        string? keyword   = null,
        int     limit     = 50,
        CancellationToken ct = default)
    {
        IEnumerable<ContentEntry> results = _index.Values;

        if (ns is not null)
            results = results.Where(e => e.Namespace == ns);

        if (tag.HasValue)
            results = results.Where(e => e.Tag == tag.Value);

        if (keyword is not null)
        {
            // Case-insensitive title + body keyword match.
            var kw = keyword.ToUpperInvariant();
            results = results.Where(e =>
                e.Title.ToUpperInvariant().Contains(kw) ||
                e.Body.ToUpperInvariant().Contains(kw));
        }

        return Task.FromResult<IReadOnlyList<ContentEntry>>(
            results.Take(limit).ToList());
    }

    public Task<IReadOnlyList<ContentEntry>> ListAsync(
        string? ns = null, CancellationToken ct = default)
    {
        IEnumerable<ContentEntry> items = _index.Values;
        if (ns is not null)
            items = items.Where(e => e.Namespace == ns);

        return Task.FromResult<IReadOnlyList<ContentEntry>>(items.ToList());
    }
}
