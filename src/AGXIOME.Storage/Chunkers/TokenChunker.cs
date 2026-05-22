using System.Text.RegularExpressions;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Token-aware chunker that tokenizes text at word and punctuation boundaries.
/// Unlike <see cref="FixedSizeChunker"/>, which approximates token count via character
/// length (÷4), <c>TokenChunker</c> extracts actual token units — word sequences, numbers,
/// and individual punctuation marks — and measures chunk size in those units.
/// This produces more accurate splits for code, legal text, and multilingual content
/// where the chars-per-token ratio varies significantly.
/// </summary>
public sealed class TokenChunker : IChunker
{
    // Matches word-like tokens (letters, digits, apostrophes) and individual punctuation.
    // Whitespace is intentionally excluded — it is not counted as a token but preserved
    // in reconstructed content by capturing the full source span between first and last token.
    private static readonly Regex _tokenPattern = new(
        @"[\w']+|[^\w\s]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public ChunkerType ChunkerType => ChunkerType.Token;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        var matches = _tokenPattern.Matches(text);

        if (matches.Count == 0)
            return [new DocumentChunk(text, 0, 0, text.Length)];

        // Entire text fits within budget — return as single chunk.
        if (matches.Count <= maxTokens)
            return [new DocumentChunk(text, 0, 0, text.Length)];

        var chunks = new List<DocumentChunk>();
        int step = Math.Max(1, maxTokens - overlap);

        for (int i = 0; i < matches.Count; i += step)
        {
            int end   = Math.Min(i + maxTokens, matches.Count);
            var first = matches[i];
            var last  = matches[end - 1];

            // Preserve original whitespace inside the window by slicing the source string
            // between the first and last token boundary.
            int startChar = first.Index;
            int endChar   = last.Index + last.Length;
            var content   = text[startChar..endChar];

            if (!string.IsNullOrWhiteSpace(content))
                chunks.Add(new DocumentChunk(content, chunks.Count, startChar, endChar));

            if (end >= matches.Count) break;
        }

        return chunks.Count > 0
            ? chunks
            : [new DocumentChunk(text, 0, 0, text.Length)];
    }
}
