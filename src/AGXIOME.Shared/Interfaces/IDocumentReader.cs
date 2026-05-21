using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>A section within a document (heading → content mapping).</summary>
public record DocumentSection(
    string? Heading,
    string Content,
    int Level = 0,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Full output of a document reader.</summary>
public record DocumentContent(
    string SourceUri,
    DocumentReaderType ReaderType,
    string RawText,
    IReadOnlyList<DocumentSection> Sections,
    Dictionary<string, object>? Metadata = null
);

/// <summary>Document reader contract. URI can be a file path, HTTP URL, or provider-specific URI.
/// Throw <see cref="NotSupportedException"/> if the URI scheme is unsupported.</summary>
public interface IDocumentReader
{
    DocumentReaderType ReaderType { get; }
    IReadOnlyList<string> SupportedExtensions { get; }

    Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default);
    Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default);
}
