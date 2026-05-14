---
title: Documentformatreaders
category: entity
tags: [document-readers, file-parsing, pdf, docx, xlsx, csv, json, xml, yaml, markdown, html, rtf, epub, plaintext]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Documentformatreaders

## Summary
The `DocumentFormatReaders` namespace in `AGXIOME.Storage.Readers` provides a set of local document format readers, each implementing the [[IDocumentReader]] interface. These readers enable the AGXIOMA framework to parse and extract text and structured sections from a wide variety of file formats — including PDF, DOCX, XLSX, CSV, JSON, XML, YAML, Markdown, HTML, RTF, EPUB, and plain text — either from a file system path or from an in-memory stream. They produce [[DocumentContent]] objects with metadata (such as content hash, page count, or paragraph count) and break documents into [[DocumentSection]] units for downstream processing, indexing, or deduplication.

## Key Details
- All readers implement `IDocumentReader` and expose a `ReaderType` enum and a list of `SupportedExtensions`.
- Two access methods are provided: `ReadAsync(string uri, ...)` for file paths and `ReadFromStreamAsync(Stream, ...)` for streams (used in tests and pipelines).
- Readers extract text and optionally build sections using format-specific strategies (e.g., page-level for PDF, heading-based for DOCX, worksheet-level for XLSX, top-level keys for JSON/XML/YAML).
- SHA-256 content hashes are included in metadata for deduplication (e.g., `PdfReader` computes a hash of the full extracted text).
- The implementation leverages well-known NuGet libraries: PdfPig, ClosedXML, CsvHelper, DocumentFormat.OpenXml, Markdig, RtfPipe, VersOne.Epub, and YamlDotNet.
- Section extraction is designed to be cancellable via `CancellationToken`.
- Core file properties (e.g., title) are optionally read from DOCX metadata.

## Cross-References
- [[IDocumentReader]] — the interface that all readers implement
- [[DocumentContent]] — the output model produced by readers
- [[DocumentSection]] — the sectional unit used to represent headings and content
- [[DocumentReaderType]] — enumeration identifying the source format

## Source Files
- `AGXIOME.Storage/Readers/DocumentFormatReaders.cs` (ingested 2026-05-14)