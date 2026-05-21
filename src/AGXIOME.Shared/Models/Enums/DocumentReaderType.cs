namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Document Reader types — 28 readers
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Typed document reader discriminator covering file formats and remote sources.</summary>
public enum DocumentReaderType
{
    Pdf = 0,
    Docx = 1,
    Xlsx = 2,
    Pptx = 3,
    Csv = 4,
    Json = 5,
    Xml = 6,
    Markdown = 7,
    Html = 8,
    PlainText = 9,
    Yaml = 10,
    Rtf = 11,
    Website = 12,
    Wikipedia = 13,
    Arxiv = 14,
    PubMed = 15,
    YoutubeTranscript = 16,
    Firecrawl = 17,
    AWSS3 = 18,
    AzureBlob = 19,
    GCS = 20,
    GitHub = 21,
    SharePoint = 22,
    ImageOCR = 23,
    AudioWhisper = 24,
    Epub = 25,
    Notion = 26,
    Confluence = 27,
}
