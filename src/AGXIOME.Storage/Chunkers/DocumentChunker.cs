using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Storage.Chunkers;

/// <summary>
/// Multi-level document chunker.
///
/// Hierarchy:  Heading (H1/H2/H3) → Paragraph (\n\n) → Sentence (., !, ?)
///
/// Splits the document top-down:
///  1. First by ATX headings (# / ## / ###) — each heading section becomes a candidate.
///  2. If a section exceeds the token budget, split by paragraph (\n\n).
///  3. If a paragraph still exceeds the budget, split by sentence boundaries.
///  4. Hard-split any remaining over-budget piece via <see cref="RecursiveChunker"/>.
///
/// Overlap is applied at the leaf level (tail of previous chunk prepended to next).
/// </summary>
public sealed class DocumentChunker : IChunker
{
    private readonly RecursiveChunker _recursiveFallback = new();

    public ChunkerType ChunkerType => ChunkerType.Document;

    public IReadOnlyList<DocumentChunk> Chunk(string text, int maxTokens = 512, int overlap = 64)
    {
        if (string.IsNullOrEmpty(text))
            return [new DocumentChunk(string.Empty, 0, 0, 0)];

        int charBudget  = maxTokens * 4;
        int overlapChars = overlap * 4;

        // Level 1: split by headings.
        var headingSections = SplitByHeadings(text);
        var leafSegments = new List<string>();

        foreach (string section in headingSections)
        {
            if (section.Length <= charBudget)
            {
                leafSegments.Add(section);
                continue;
            }

            // Level 2: split by paragraph.
            var paragraphs = section.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            foreach (string para in paragraphs)
            {
                string trimmedPara = para.Trim();
                if (string.IsNullOrWhiteSpace(trimmedPara)) continue;

                if (trimmedPara.Length <= charBudget)
                {
                    leafSegments.Add(trimmedPara);
                    continue;
                }

                // Level 3: split by sentence.
                var sentences = trimmedPara.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);
                var sentBuffer = new System.Text.StringBuilder();

                foreach (string sentence in sentences)
                {
                    string s = sentence.Trim();
                    if (string.IsNullOrWhiteSpace(s)) continue;

                    if (sentBuffer.Length + s.Length + 2 > charBudget && sentBuffer.Length > 0)
                    {
                        leafSegments.Add(sentBuffer.ToString().Trim());
                        sentBuffer.Clear();
                    }

                    sentBuffer.Append(s).Append(". ");
                }

                if (sentBuffer.Length > 0)
                {
                    string remaining = sentBuffer.ToString().Trim();
                    if (remaining.Length <= charBudget)
                        leafSegments.Add(remaining);
                    else
                    {
                        // Level 4: hard split via recursive fallback.
                        foreach (var sub in _recursiveFallback.Chunk(remaining, maxTokens, 0))
                            leafSegments.Add(sub.Content);
                    }
                }
            }
        }

        // Build final chunks with overlap.
        var chunks = new List<DocumentChunk>(leafSegments.Count);
        int cursor = 0, index = 0;

        for (int i = 0; i < leafSegments.Count; i++)
        {
            string content = leafSegments[i];

            if (i > 0 && overlapChars > 0)
            {
                string prev = leafSegments[i - 1];
                string tail = prev.Length > overlapChars ? prev[^overlapChars..] : prev;
                content = tail + " " + content;
            }

            int start = cursor;
            cursor += leafSegments[i].Length + 1;
            chunks.Add(new DocumentChunk(content.Trim(), index++, start, cursor));
        }

        return chunks;
    }

    private static List<string> SplitByHeadings(string text)
    {
        var sections  = new List<string>();
        var lines     = text.Split('\n');
        var buffer    = new System.Text.StringBuilder();

        foreach (string line in lines)
        {
            if (line.StartsWith('#') && buffer.Length > 0)
            {
                sections.Add(buffer.ToString().Trim());
                buffer.Clear();
            }
            buffer.AppendLine(line);
        }

        if (buffer.Length > 0)
            sections.Add(buffer.ToString().Trim());

        return sections.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }
}
