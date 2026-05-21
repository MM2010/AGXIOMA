namespace AGXIOME.Shared.Models;

/// <summary>A generic document submitted for ingestion or processing.</summary>
public record Document(
    string Id,
    string Content,
    DocumentType Type,
    Dictionary<string, string>? Metadata = null
);

/// <summary>A text chunk produced during document processing.</summary>
public record Chunk(
    string Text,
    int Index,
    int StartOffset,
    int EndOffset,
    Dictionary<string, string>? Metadata = null
);
