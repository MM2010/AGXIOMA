# P05 — Document Readers (25+)

> **Priority**: 🔴 P0 | **Dipende da**: P07 (Chunking) | **Usato da**: P08 (Runtime)
> **Target**: 25+ reader (Agno ha 18)

---

## Provider Esistenti (0) ❌

Nessun reader implementato in AGXIOME.

---

## Reader da Implementare (25+)

### 🔴 Wave 1 — File Format (12)

| # | Reader | Formato | Package | NuGet |
|---|--------|---------|---------|-------|
| R-01 | **PDF Reader** | PDF | `PdfPig` / `iText7` | ✅ |
| R-02 | **DOCX Reader** | Word | `DocumentFormat.OpenXml` | ✅ |
| R-03 | **XLSX Reader** | Excel | `ClosedXML` | ✅ |
| R-04 | **PPTX Reader** | PowerPoint | `DocumentFormat.OpenXml` | ✅ |
| R-05 | **CSV Reader** | CSV | `CsvHelper` | ✅ |
| R-06 | **JSON Reader** | JSON | `System.Text.Json` | ✅ |
| R-07 | **XML Reader** | XML | `System.Xml.Linq` | ✅ |
| R-08 | **Markdown Reader** | MD | `Markdig` | ✅ |
| R-09 | **HTML Reader** | HTML | `HtmlAgilityPack` | ✅ |
| R-10 | **Plain Text Reader** | TXT | `System.IO` | ✅ |
| R-11 | **YAML Reader** | YAML | `YamlDotNet` | ✅ |
| R-12 | **RTF Reader** | RTF | `System.Windows.Forms` | ✅ |

### 🔴 Wave 2 — Web & API (6)

| # | Reader | Source | Package | NuGet |
|---|--------|--------|---------|-------|
| R-13 | **Website Reader** | URL scraping | `HtmlAgilityPack` | ✅ |
| R-14 | **Wikipedia Reader** | Wiki API | REST | ✅ |
| R-15 | **Arxiv Reader** | Paper API | REST | ✅ |
| R-16 | **PubMed Reader** | Medical API | REST | ✅ |
| R-17 | **YouTube Transcript** | Video subtitles | `YouTubeTranscriptApi` | ⚠️ |
| R-18 | **Firecrawl** | Web scraping | REST API | ✅ |

### 🟡 Wave 3 — Cloud Storage (5)

| # | Reader | Source | Package | NuGet |
|---|--------|--------|---------|-------|
| R-19 | **AWS S3 Reader** | S3 bucket | `AWSSDK.S3` | ✅ |
| R-20 | **Azure Blob Reader** | Blob storage | `Azure.Storage.Blobs` | ✅ |
| R-21 | **GCS Reader** | Google Cloud | `Google.Cloud.Storage` | ✅ |
| R-22 | **GitHub Reader** | Repo files | `Octokit` | ✅ |
| R-23 | **SharePoint Reader** | SharePoint | `Microsoft.Graph` | ✅ |

### 🟢 Wave 4 — Specialized (5)

| # | Reader | Source | Package | NuGet |
|---|--------|--------|---------|-------|
| R-24 | **Image OCR Reader** | Images | `Tesseract` | ✅ |
| R-25 | **Audio Transcriber** | Audio | Whisper API | ✅ |
| R-26 | **EPUB Reader** | eBook | `EpubReader` | ✅ |
| R-27 | **Notion Reader** | Notion pages | Notion API | ✅ |
| R-28 | **Confluence Reader** | Confluence | Confluence API | ✅ |

---

## Interfaccia Unificata

```csharp
public interface IDocumentReader
{
    string Name { get; }
    IReadOnlySet<string> SupportedExtensions { get; }
    IReadOnlySet<string> SupportedMimeTypes { get; }
    
    Task<DocumentContent> ReadAsync(
        DocumentSource source, 
        ReaderOptions? options,
        CancellationToken ct);
}

public record DocumentSource
{
    public string? FilePath { get; init; }
    public byte[]? RawBytes { get; init; }
    public Uri? Url { get; init; }
    public Stream? ContentStream { get; init; }
}

public record DocumentContent
{
    public string Text { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<DocumentSection> Sections { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
    public long ContentLength { get; init; }
    public string? ContentHash { get; init; } // SHA256
}

public record DocumentSection
{
    public string Heading { get; init; }
    public int Level { get; init; }
    public string Text { get; init; }
    public int PageNumber { get; init; }
}

public record ReaderOptions
{
    public bool ExtractImages { get; init; } = false;
    public int MaxPage { get; init; } = 0; // 0 = all
    public string? Password { get; init; }
    public bool PreserveFormatting { get; init; } = false;
}
```

---

## Pattern di Implementazione

```csharp
// Readers/PdfReader.cs
public sealed class PdfReader : IDocumentReader
{
    public string Name => "PDF";
    public IReadOnlySet<string> SupportedExtensions => new HashSet<string> { ".pdf" };
    
    public async Task<DocumentContent> ReadAsync(
        DocumentSource source, 
        ReaderOptions? options,
        CancellationToken ct)
    {
        // 1. Carica PDF (da file, bytes o stream)
        // 2. Estrai testo con PdfPig
        // 3. Rileva sezioni da headings (font size)
        // 4. Calcola hash SHA256
        // 5. Estrai metadata (author, title, pages)
        // 6. Restituisci DocumentContent
    }
}
```

---

## Test per Reader

```csharp
// Ogni reader ha:
[Fact] public async Task Reader_ReadFromFile_ReturnsContent()
[Fact] public async Task Reader_ReadFromBytes_ReturnsContent()
[Fact] public async Task Reader_ReadFromStream_ReturnsContent()
[Fact] public async Task Reader_ExtractsTitle()
[Fact] public async Task Reader_ExtractsSections()
[Fact] public async Task Reader_InvalidFile_ErrorCode()
[Fact] public async Task Reader_EmptyFile_ReturnsEmpty()
[Fact] public async Task Reader_Metadata_Parsed()
```

**Totale reader: 28 | File: ~40 | Test: ~224**
