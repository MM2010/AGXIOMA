using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Single-text embedding contract.
/// All implementations must be thread-safe and async-only (no blocking I/O).</summary>
public interface IEmbedder
{
    EmbedderType EmbedderType { get; }
    int Dimensions { get; }

    /// <summary>Embed a single text. Empty input → zero vector of Dimensions length.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    /// <summary>Embed a batch. Order preserved, parallel internally where provider allows.</summary>
    Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
