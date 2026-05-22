namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Chunker types — 12 chunkers
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Typed chunker strategy discriminator for text segmentation algorithms.</summary>
public enum ChunkerType
{
    // Existing 3
    Fixed = 0,
    Heading = 1,
    Sentence = 2,

    // New 9 — C-01..C-09
    Recursive = 10,
    Semantic = 11,
    Agentic = 12,
    Code = 13,
    Document = 14,
    CsvRow = 15,
    JsonPath = 16,
    SlidingWindow = 17,
    Hybrid = 18,
    Token = 19,
}
