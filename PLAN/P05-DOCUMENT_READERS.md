# P05 — Document Readers (25+)

> **Priority**: 🔴 P0 | **Dipende da**: P07 (Chunking) | **Usato da**: P08 (Runtime)
> **Target**: 25+ reader (Agno ha 18)

---

## Provider Esistenti (20) ✅

I reader locali `R-01..R-12` sono implementati in `AGXIOME.Storage.Readers.DocumentFormatReaders`
e risultano coperti dal slice `F6ReaderTests.cs` (`172 passed, 0 failed` il `29/05/26`).
`R-13 Website Reader`, `R-14 Wikipedia Reader` e `R-16 PubMed Reader` sono implementati in
`AGXIOME.Storage.Readers.WebApiReaders` e dispongono di live test env-gated
eseguiti con endpoint HTTP reali il `29/05/26`.
`R-26 EPUB Reader` e' implementato in `AGXIOME.Storage.Readers.DocumentFormatReaders` e ora
dispone di test locali reali con fixture `.epub` minima generata a runtime.
Per `R-14` e' stata necessaria una fix runtime locale: richieste esplicite con
`User-Agent` verso le API Wikipedia per evitare `403 Forbidden` su `HttpClient` reale.
Per `R-16` e' stata necessaria una fix runtime locale: parsing dell'XML reale di
NCBI Entrez al posto del JSON ipotizzato in origine.
`R-15 Arxiv Reader` resta aperto: probe live verso `export.arxiv.org` hanno restituito
`429`, quindi non e' ancora validabile in modo veritiero da questa macchina.
`R-17 YouTube Transcript Reader` resta in backlog operativo: anche riprovando `type=list`,
`fmt=json3`, `fmt=vtt` e `kind=asr` su video pubblici noti, l'endpoint timedtext ha restituito
payload vuoto (`LEN=0`), quindi manca ancora un campione live chiudibile.
`R-18 Firecrawl Reader` resta in backlog operativo: i live test env-gated e l'hook di esecuzione
sono pronti, ma senza `AGXIOME_FIRECRAWL_API_KEY` e `AGXIOME_FIRECRAWL_TEST_URL` il slice resta
onestamente a `5 passed / 3 skipped`.
`R-19 AWS S3 Reader` e' stato verificato live il `29/05/26` su oggetto pubblico reale senza
credenziali esplicite usando `https://noaa-goes16.s3.amazonaws.com/index.html` e un path mancante
nello stesso bucket: `8 passed / 0 skipped` sullo slice dedicato.
`R-20 Azure Blob Reader` e' stato verificato live il `29/05/26` su blob pubblico reale senza
connection string usando `https://azuremlexamples.blob.core.windows.net/datasets/iris.csv` e un
path mancante nello stesso container: `8 passed / 0 skipped` sullo slice dedicato.
`R-21 GCS Reader` e' stato verificato live il `29/05/26` su oggetto pubblico reale senza file
credenziali usando `gs://cloud-samples-data/bigquery/us-states/us-states.csv` e un path mancante
nello stesso bucket: `8 passed / 0 skipped` sullo slice dedicato.
Per `R-22 GitHub Reader` il path pubblico e' stato verificato live il `29/05/26` senza token usando
`github://harry0703/MoneyPrinterTurbo/README.md` e un path mancante nello stesso repository:
`7 passed / 1 skipped`, con l'unico skip rimasto sul ramo `private repo requires auth`.
Per questo `R-22` viene considerato chiuso sul perimetro del reader base pubblico; la copertura di
repository privati resta hardening opzionale e non blocca la chiusura del provider.
`R-23 SharePoint Reader` resta invece in backlog operativo: oggi non c'e' ancora una proof pubblica
equivalente gia' stabilizzata e il path live utile resta legato a item Graph reale piu' token.
`R-24 Image OCR Reader` resta in backlog tecnico: su questa macchina non emergono ne' binario
`tesseract` ne' directory `tessdata`, quindi non esiste ancora una proof OCR locale eseguibile.
E' stato aggiunto anche un proof di servizio OCR dedicato in Docker (`agxiome-ocr`): espone
`/health` e `POST /ocr` sopra Tesseract, cosi' le richieste OCR possono essere distribuite fuori
dal container principale, che quindi non porta piu' Tesseract dentro la sua immagine runtime. Questo
pero' non chiude ancora `R-24`: il reader corrente resta in-process
finche' non viene introdotto o adottato un client remoto verso il servizio OCR. Ora esiste anche
`RemoteImageOcrReader` come adapter HTTP separato, utile come tool aggiuntivo per scenari distribuiti.
Engine OCR compatibili con questo pattern:
- `Tesseract` dietro servizio HTTP interno: costo basso, self-hosted, buono per testo stampato.
- `PaddleOCR` o `EasyOCR`: migliori su layout complessi o lingue miste, ma piu' pesanti lato runtime.
- `Azure AI Vision`, `Google Vision`, `AWS Textract`: SaaS piu' forti su documenti enterprise, ma con
    costo per chiamata, dipendenza esterna e requisiti credenziali.
Strategia pragmatica: Tesseract remoto come default self-hosted; engine cloud o ML piu' pesanti solo
come implementazioni alternative dietro lo stesso contratto del reader remoto.
`R-25 Audio Whisper Reader` resta in backlog operativo: il reader e i test live ora possono essere
agganciati a una chiave reale e a un file audio reale, ma senza `AGXIOME_WHISPER_API_KEY` e
`AGXIOME_WHISPER_AUDIO_FILE` il slice non e' ancora chiudibile in modo veritiero.
I reader che dipendono da API, credenziali o runtime esterni restano da validare in modo veritiero
prima di essere chiusi nel piano esecutivo.

### Backlog Operativo

- `R-15 Arxiv Reader`: backlog esplicito finche' l'endpoint `export.arxiv.org` non consente
    almeno una proof live stabile senza `429`.
- Uscita dal backlog `R-15`: eseguire `ArxivReaderTests` con ID reale, URL completo e caso
    negativo, poi aggiornare piano esecutivo solo dopo passaggio live.
- `R-18 Firecrawl Reader`: backlog esplicito finche' non sono disponibili una API key reale e un
    target URL reale per `test-reader-live-hooks.ps1 -OnlyFirecrawl`.
- Uscita dal backlog `R-18`: eseguire `FirecrawlReaderTests` con `AGXIOME_FIRECRAWL_API_KEY` e
    `AGXIOME_FIRECRAWL_TEST_URL` valorizzati, poi chiudere il piano solo dopo passaggio live.
- `R-17 YouTube Transcript Reader`: backlog esplicito finche' non viene identificato almeno un
    video pubblico con transcript realmente leggibile e un caso negativo senza caption.
- Uscita dal backlog `R-17`: eseguire `test-reader-live-hooks.ps1 -OnlyYouTube` con
    `AGXIOME_YOUTUBE_TEST_VIDEO_ID`, `AGXIOME_YOUTUBE_TEST_URI` e
    `AGXIOME_YOUTUBE_TEST_NO_CAPTION_VIDEO_ID` valorizzati, poi chiudere solo dopo passaggio live.
- `R-23 SharePoint Reader`: backlog esplicito finche' non sono disponibili `sharepoint://SITE/DRIVE/ITEM`
    e token Graph reale per una proof live controllata.
- Uscita dal backlog `R-23`: eseguire `SharePointReaderTests` con `AGXIOME_SHAREPOINT_TEST_URI` e
    `AGXIOME_SHAREPOINT_ACCESS_TOKEN`, poi chiudere il piano solo dopo passaggio live.
- `R-24 Image OCR Reader`: backlog tecnico esplicito finche' non sono disponibili runtime Tesseract
    nativo e language packs `tessdata` realmente caricabili su questa macchina.
- Uscita dal backlog `R-24`: eseguire `ImageOcrReaderTests` con una fixture immagine reale solo dopo
    presenza verificata di `tesseract` e `tessdata/eng.traineddata`.
- `R-25 Audio Whisper Reader`: backlog esplicito finche' non sono disponibili una API key reale e un
    file audio reale per `test-reader-live-hooks.ps1 -OnlyWhisper`.
- Uscita dal backlog `R-25`: eseguire `AudioWhisperReaderTests` con `AGXIOME_WHISPER_API_KEY` e
    `AGXIOME_WHISPER_AUDIO_FILE` valorizzati, poi chiudere il piano solo dopo passaggio live.
- Nota `R-22 GitHub`: per repository pubblici non serve token; `AGXIOME_GITHUB_TOKEN` resta
    necessario solo per coverage esplicita di repository privati o per maggiore stabilita' contro
    rate limiting.
- `R-22 GitHub` esce dal backlog operativo sul perimetro pubblico: resta aperto solo come hardening
    facoltativo il ramo `private repo requires auth` se vorremo validarlo con un repository privato reale.
- `R-27 Notion Reader`: backlog esplicito finche' non sono disponibili un integration token e un
    page ID Notion reali. I test live sono ora env-gated e usano `test-reader-live-hooks.ps1 -OnlyNotion`.
- Uscita dal backlog `R-27`: eseguire `NotionReaderTests` con `AGXIOME_NOTION_TOKEN` e
    `AGXIOME_NOTION_PAGE_ID` valorizzati, poi chiudere il piano solo dopo passaggio live.
- `R-28 Confluence Reader`: backlog esplicito finche' non sono disponibili un token, un instance URL
    e un page URI Confluence reali. I test live sono ora env-gated e usano `test-reader-live-hooks.ps1 -OnlyConfluence`.
- Uscita dal backlog `R-28`: eseguire `ConfluenceReaderTests` con `AGXIOME_CONFLUENCE_TOKEN`,
    `AGXIOME_CONFLUENCE_INSTANCE` e `AGXIOME_CONFLUENCE_PAGE_URI` valorizzati, poi chiudere il piano
    solo dopo passaggio live.
- Nota `R-19 AWS S3`: per bucket pubblici il reader ora e' validabile anche senza credenziali
    esplicite usando client anonimo; le credenziali restano opzionali per bucket non pubblici.
- Nota `R-20 Azure Blob`: per container pubblici il reader ora e' validabile anche senza connection
    string usando `BlobServiceClient(Uri)` derivato dalla blob URI; la connection string resta
    opzionale per container non pubblici.
- Nota `R-21 GCS`: per bucket pubblici il reader ora e' validabile anche senza
    `GOOGLE_APPLICATION_CREDENTIALS`; il file credenziali resta necessario solo per coverage privata.

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
