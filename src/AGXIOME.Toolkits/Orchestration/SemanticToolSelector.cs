// CO-A4 — SemanticToolSelector
// Selects the best toolkit for a free-text goal using embedding cosine similarity.
// Uses IEmbedder (AGXIOME.Shared) to build a lazy, cached vector index of all manifests.

using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Toolkits.Orchestration;

/// <summary>
/// Selects the most semantically relevant <see cref="CapabilityManifest"/> for a free-text goal
/// using embedding vectors and cosine similarity.
///
/// <para>
/// The vector index is built lazily on the first call to <see cref="SelectAsync"/> and cached
/// until <see cref="InvalidateIndex"/> is called. The index text for each toolkit is:
/// <c>"{Name} {Category} {SupportedActions joined}"</c>
/// — giving the embedder enough semantic signal to distinguish toolkits.
/// </para>
///
/// <para>
/// This component is optional — the system works without it. It provides value when the agent
/// goal is a natural language description (e.g. "I need to analyse tabular data") rather than
/// an explicit toolkit ID.
/// </para>
/// </summary>
public sealed class SemanticToolSelector : IAsyncDisposable
{
    private readonly CapabilityRegistry  _registry;
    private readonly IEmbedder           _embedder;
    private readonly SemaphoreSlim       _indexLock = new(1, 1);

    private (string Id, float[] Vector)[]? _index;

    public SemanticToolSelector(CapabilityRegistry registry, IEmbedder embedder)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(embedder);
        _registry = registry;
        _embedder = embedder;
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Select the manifest whose embedding is most similar to <paramref name="goal"/>.
    /// </summary>
    /// <param name="goal">Free-text description of the capability needed.</param>
    /// <param name="filterCategory">When provided, restricts candidates to this category.</param>
    /// <param name="configuredOnly">When <c>true</c>, only considers configured toolkits.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The best-matching manifest, or <c>null</c> if no candidates are available.</returns>
    public async Task<CapabilityManifest?> SelectAsync(
        string          goal,
        ToolkitCategory? filterCategory = null,
        bool            configuredOnly  = false,
        CancellationToken ct            = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);

        var index = await EnsureIndexAsync(ct).ConfigureAwait(false);
        if (index.Length == 0) return null;

        var goalVec = await _embedder.EmbedAsync(goal, ct).ConfigureAwait(false);
        if (goalVec.Length == 0) return null;

        var candidates = _registry.GetAll()
            .Where(m => filterCategory is null || m.Category == filterCategory)
            .Where(m => !configuredOnly || m.IsConfigured)
            .ToList();

        float bestScore = float.MinValue;
        CapabilityManifest? best = null;

        foreach (var manifest in candidates)
        {
            var entry = Array.Find(index, x => x.Id == manifest.Id);
            if (entry.Vector is null || entry.Vector.Length == 0) continue;

            var score = CosineSimilarity(goalVec, entry.Vector);
            if (score > bestScore)
            {
                bestScore = score;
                best = manifest;
            }
        }

        return best;
    }

    /// <summary>
    /// Select the top-N manifests ordered by semantic similarity.
    /// </summary>
    public async Task<IReadOnlyList<(CapabilityManifest Manifest, float Score)>> SelectTopAsync(
        string          goal,
        int             topN            = 5,
        ToolkitCategory? filterCategory = null,
        bool            configuredOnly  = false,
        CancellationToken ct            = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        if (topN <= 0) throw new ArgumentOutOfRangeException(nameof(topN), "topN must be > 0");

        var index = await EnsureIndexAsync(ct).ConfigureAwait(false);
        if (index.Length == 0) return [];

        var goalVec = await _embedder.EmbedAsync(goal, ct).ConfigureAwait(false);
        if (goalVec.Length == 0) return [];

        return _registry.GetAll()
            .Where(m => filterCategory is null || m.Category == filterCategory)
            .Where(m => !configuredOnly || m.IsConfigured)
            .Select(manifest =>
            {
                var entry = Array.Find(index, x => x.Id == manifest.Id);
                var score = entry.Vector is { Length: > 0 }
                    ? CosineSimilarity(goalVec, entry.Vector)
                    : float.MinValue;
                return (manifest, score);
            })
            .OrderByDescending(x => x.score)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Invalidate the cached vector index (e.g. after new toolkits are registered).
    /// The next call to <see cref="SelectAsync"/> will rebuild the index.
    /// </summary>
    public void InvalidateIndex()
    {
        _indexLock.Wait();
        try { _index = null; }
        finally { _indexLock.Release(); }
    }

    // ── Index Management ──────────────────────────────────────────────────────

    private async Task<(string Id, float[] Vector)[]> EnsureIndexAsync(CancellationToken ct)
    {
        await _indexLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_index is not null) return _index;

            var manifests = _registry.GetAll();
            if (manifests.Count == 0)
            {
                _index = [];
                return _index;
            }

            var texts = manifests
                .Select(m => BuildIndexText(m))
                .ToList();

            var vectors = await _embedder.EmbedBatchAsync(texts, ct).ConfigureAwait(false);

            _index = manifests
                .Zip(vectors, (m, v) => (Id: m.Id, Vector: v))
                .ToArray();

            return _index;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private static string BuildIndexText(CapabilityManifest m)
    {
        var actions = m.SupportedActions.Count > 0
            ? string.Join(' ', m.SupportedActions)
            : string.Empty;
        return string.IsNullOrWhiteSpace(actions)
            ? $"{m.Name} {m.Category}"
            : $"{m.Name} {m.Category} {actions}";
    }

    // ── Math ──────────────────────────────────────────────────────────────────

    private static float CosineSimilarity(float[] a, float[] b)
    {
        int len = Math.Min(a.Length, b.Length);
        float dot = 0f, normA = 0f, normB = 0f;
        for (int i = 0; i < len; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        float denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom < 1e-10f ? 0f : dot / denom;
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public ValueTask DisposeAsync()
    {
        _indexLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
