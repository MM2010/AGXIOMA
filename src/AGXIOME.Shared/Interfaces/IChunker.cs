using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Result of a chunking operation. Each chunk carries its source offset for replay.</summary>
public record DocumentChunk(
    string Content,
    int ChunkIndex,
    int StartOffset,
    int EndOffset,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Chunker contract. Implementations are stateless value-type alike — one instance, many calls.</summary>
public interface IChunker
{
    ChunkerType ChunkerType { get; }

    /// <summary>Split <paramref name="text"/> into chunks respecting <paramref name="maxTokens"/> and
    /// <paramref name="overlap"/> (token count). Returns at least one chunk even for empty input.</summary>
    IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64);
}
