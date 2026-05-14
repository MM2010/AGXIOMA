---
title: F6 Reader Tests
category: entity
tags: [test, reader, unit-tests, document-readers]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F6 Reader Tests

## Summary
The F6 Reader Tests suite comprehensively validates all document reader implementations in the AGXIOMA framework. It covers 28 reader types (R-01 through R-28) with 8 tests each, totaling 224 automated checks. Tests are organized by reader category—file-based readers, web/API readers, cloud SDK readers, and media readers—ensuring consistent interface compliance, correct handling of valid and invalid input streams, and proper error responses. The suite uses NSubstitute for mocking and XUnit as the test framework.

## Key Details
- Tests 28 distinct document readers (via test classes like `PdfReaderTests`, `DocxReaderTests`, etc.)
- Each reader class follows an 8-test pattern: ReaderType, SupportedExtensions, ReadFromStream_ValidInput, ReadFromStream_EmptyStream, ReadFromStream_NullStream_Throws, ReadAsync_FileNotFound_Throws, Sections_NotEmpty, RawText_NotEmpty
- Web/API and cloud SDK readers include 4 skipped tests requiring live services
- Media readers (Tesseract, Whisper) also skip live OCR/API tests
- Uses a shared helper `TestDocContent` providing minimal valid streams for PDF, TXT, MD, JSON, XML, YAML, CSV, HTML, RTF
- All tests verify the `IDocumentReader` interface is properly implemented
- Located in `AGXIOME.Tests/F6ReaderTests.cs`

## Cross-References
- [[IDocumentReader]] — the interface all tested readers implement
- [[DocumentReaderType]] — the enum tested for each reader
- [[TestDocContent]] — the static helper class that supplies test streams
- [[F6 Document Readers]] — the actual reader implementations under test

## Source Files
- `AGXIOME.Tests/F6ReaderTests.cs` (ingested 2026-05-14)