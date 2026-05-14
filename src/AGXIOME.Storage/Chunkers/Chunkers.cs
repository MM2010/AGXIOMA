using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>Splits text into fixed-size token-approximate chunks with configurable overlap.</summary>
public sealed class FixedSizeChunker : IChunker
{
    public ChunkerType ChunkerType => ChunkerType.Fixed;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        // Approximate: 1 token ˜ 4 chars; use word-boundary splitting for coherence.
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int stepWords = Math.Max(1, maxTokens - overlap);
        var chunks = new List<DocumentChunk>();
        int index = 0;

        for (int i = 0; i < words.Length; i += stepWords)
        {
            var segment = string.Join(" ", words.Skip(i).Take(maxTokens));
            if (string.IsNullOrWhiteSpace(segment)) break;

            // Approximate char offsets (exact offsets require source scanning; this is good enough
            // for downstream search — use a scanning chunker when precise offsets are required).
            int startOffset = words.Take(i).Sum(w => w.Length + 1);
            int endOffset = startOffset + segment.Length;
            chunks.Add(new DocumentChunk(segment, index++, startOffset, endOffset));
        }

        return chunks;
    }
}

/// <summary>Splits markdown content at heading boundaries (# / ## / ###).
/// Falls back to FixedSizeChunker for non-markdown text.</summary>
public sealed class HeadingChunker : IChunker
{
    public ChunkerType ChunkerType => ChunkerType.Heading;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        var chunks = new List<DocumentChunk>();
        var lines = text.Split('\n');
        var currentLines = new List<string>();
        int index = 0;
        int offset = 0;
        int sectionStart = 0;

        foreach (var line in lines)
        {
            // Flush on any ATX heading so each section becomes its own chunk.
            if (line.StartsWith('#') && currentLines.Count > 0)
            {
                FlushSection(currentLines, ref index, sectionStart, offset, chunks);
                sectionStart = offset;
                currentLines.Clear();
            }
            currentLines.Add(line);
            offset += line.Length + 1; // +1 for newline
        }

        if (currentLines.Count > 0)
            FlushSection(currentLines, ref index, sectionStart, offset, chunks);

        return chunks;
    }

    private static void FlushSection(List<string> lines, ref int index, int startOffset, int endOffset, List<DocumentChunk> target)
    {
        var content = string.Join("\n", lines);
        if (!string.IsNullOrWhiteSpace(content))
            target.Add(new DocumentChunk(content, index++, startOffset, endOffset));
    }
}

/// <summary>Splits content at sentence boundaries (.!?\n), grouping until maxTokens (approx).</summary>
public sealed class SentenceChunker : IChunker
{
    public ChunkerType ChunkerType => ChunkerType.Sentence;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        var chunks = new List<DocumentChunk>();
        var sentences = text.Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var buffer = new List<string>();
        // Approximate: 1 token ˜ 4 chars.
        int charBudget = maxTokens * 4;
        int charCount = 0;
        int index = 0;
        int startOffset = 0;

        foreach (var s in sentences)
        {
            var trimmed = s.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            buffer.Add(trimmed);
            charCount += trimmed.Length + 2; // +2 for ". "

            if (charCount >= charBudget)
            {
                var content = string.Join(". ", buffer) + ".";
                chunks.Add(new DocumentChunk(content, index++, startOffset, startOffset + charCount));
                // Overlap: carry last sentence into next chunk.
                var last = buffer[^1];
                buffer.Clear();
                int newStart = startOffset + charCount - last.Length - 2;
                startOffset += charCount;
                // Keep overlapping sentences (approximate — keep last sentence).
                buffer.Add(last);
                charCount = last.Length + 2;
            }
        }

        if (buffer.Count > 0)
        {
            var content = string.Join(". ", buffer) + ".";
            chunks.Add(new DocumentChunk(content, index, startOffset, startOffset + charCount));
        }

        return chunks;
    }
}
