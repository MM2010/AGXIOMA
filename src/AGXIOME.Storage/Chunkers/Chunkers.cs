using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>Splits text into fixed-size chunks with optional overlap.</summary>
public sealed class FixedSizeChunker : IChunker
{
    private readonly int _chunkSize;
    private readonly int _overlap;

    public FixedSizeChunker(int chunkSize = 512, int overlap = 64)
    {
        _chunkSize = chunkSize;
        _overlap = overlap;
    }

    public IReadOnlyList<Chunk> Chunk(string content, DocumentType type)
    {
        var chunks = new List<Chunk>();
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int index = 0;
        for (int i = 0; i < words.Length; i += _chunkSize - _overlap)
        {
            var segment = string.Join(" ", words.Skip(i).Take(_chunkSize));
            if (string.IsNullOrWhiteSpace(segment)) break;
            chunks.Add(new Chunk(segment, index++, i * 5, (i + segment.Length) * 5));
        }
        return chunks;
    }
}

/// <summary>Splits markdown content at heading boundaries.</summary>
public sealed class HeadingChunker : IChunker
{
    private readonly int _maxChunkSize;

    public HeadingChunker(int maxChunkSize = 1024) => _maxChunkSize = maxChunkSize;

    public IReadOnlyList<Chunk> Chunk(string content, DocumentType type)
    {
        if (type != DocumentType.Markdown)
            return new FixedSizeChunker().Chunk(content, type);

        var chunks = new List<Chunk>();
        var lines = content.Split('\n');
        var currentSection = new List<string>();
        int startOffset = 0, index = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("#") && currentSection.Count > 0)
            {
                chunks.Add(new Chunk(string.Join("\n", currentSection), index++, startOffset, startOffset + currentSection.Sum(l => l.Length + 1)));
                currentSection.Clear();
            }
            currentSection.Add(line);
        }
        if (currentSection.Count > 0)
            chunks.Add(new Chunk(string.Join("\n", currentSection), index, startOffset, startOffset + currentSection.Sum(l => l.Length + 1)));

        return chunks;
    }
}

/// <summary>Splits content at sentence boundaries.</summary>
public sealed class SentenceChunker : IChunker
{
    private readonly int _maxTokens;

    public SentenceChunker(int maxTokens = 512) => _maxTokens = maxTokens;

    public IReadOnlyList<Chunk> Chunk(string content, DocumentType type)
    {
        var chunks = new List<Chunk>();
        var sentences = content.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var buffer = new List<string>();
        int charCount = 0, index = 0, startOffset = 0;

        foreach (var s in sentences)
        {
            var trimmed = s.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            buffer.Add(trimmed);
            charCount += trimmed.Length;

            if (charCount >= _maxTokens * 4) // approx chars per token
            {
                chunks.Add(new Chunk(string.Join(". ", buffer) + ".", index++, startOffset, startOffset + charCount));
                buffer.Clear();
                startOffset += charCount;
                charCount = 0;
            }
        }
        if (buffer.Count > 0)
            chunks.Add(new Chunk(string.Join(". ", buffer) + ".", index, startOffset, startOffset + charCount));

        return chunks;
    }
}
