namespace AgxiomaHybridRouting.Routing;

/// <summary>
/// Abstraction for computing text embeddings used during routing.
/// Implementations should be tolerant of service unavailability.
/// </summary>
public interface IRouteEmbedder
{
    /// <summary>Returns true when the embedding service is healthy.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns an embedding vector for <paramref name="text"/>,
    /// or <c>null</c> when the embedding service is unavailable.
    /// Implementations MUST NOT throw.
    /// </summary>
    Task<float[]?> EmbedAsync(string text, CancellationToken ct = default);
}
