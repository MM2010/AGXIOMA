---
title: Agxiome Readers
category: source
tags: [readers, document-parsing, pdf, office, csv, markdown, html, yaml]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Readers

## Summary
The `AGXIOME.Readers` project provides a unified reading infrastructure for multiple document and data formats used by the AGXIOME agent framework. It abstracts file parsing behind an extensible interface, supporting PDF (via UglyToad.PdfPig), Office Open XML (via DocumentFormat.OpenXml and ClosedXML), CSV (via CsvHelper), Markdown (via Markdig), HTML (via HtmlAgilityPack), and YAML (via YamlDotNet). EPUB support is planned once the VersOne.Epub package reaches a stable release.

## Key Details
- Targets .NET 10.0 with nullable reference types enabled.
- Depends on the [[AGXIOME.Shared]] project for base types and contracts.
- PDF reading uses a prerelease version of UglyToad.PdfPig (v0.1.9-alpha001-patch1) until a stable release is available.
- Office format support is split between `DocumentFormat.OpenXml` for raw OpenXML processing and `ClosedXML` for higher-level Excel (.xlsx) operations.
- CSV reading leverages `CsvHelper` for configurable, performant parsing.
- Markdown parsing is powered by `Markdig` (v1.2.0), a fast and extensible Markdown processor.
- HTML parsing uses `HtmlAgilityPack` (v1.12.4), enabling flexible DOM traversal.
- YAML parsing uses `YamlDotNet` (v17.1.0) for serialization/deserialization.
- EPUB support is commented out pending a stable release of `VersOne.Epub` (v3.x line).

## Cross-References
- [[AGXIOME.Shared]] — Shared types, interfaces, and base classes consumed by all readers.
- [[Agxiome Writers]] — Complementary project for writing output documents.

## Source Files
- `AGXIOME.Readers/AGXIOME.Readers.csproj` (ingested 2026-05-14)