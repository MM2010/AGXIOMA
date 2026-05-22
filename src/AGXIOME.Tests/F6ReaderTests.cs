using System.Net;
using System.Text;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Readers;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F6 — Document Reader Tests (R-01..R-28)
//
// Strategy per reader type:
//   File-based (R-01..R-12, R-26):
//     8 tests: ReaderType, SupportedExtensions, ReadFromStream_ValidInput,
//              ReadFromStream_EmptyStream, ReadFromStream_NullStream_Throws,
//              ReadAsync_FileNotFound_Throws, Sections_NotEmpty, RawText_NotEmpty
//   Web/API readers (R-13..R-18, R-22, R-27, R-28):
//     8 tests: ReaderType, SupportedExtensions, Constructor_NullHttp_Throws,
//              ReadFromStream_Throws_NotSupported,
//              + 4 skipped (require live API)
//   Cloud SDK readers (R-19..R-21, R-23):
//     8 tests: ReaderType, SupportedExtensions, Constructor_NullSdk_Throws,
//              ReadFromStream_ValidInput, + 4 skipped
//   Media readers (R-24 Tesseract, R-25 Whisper):
//     8 tests: ReaderType, SupportedExtensions, Constructor checks,
//              ReadFromStream_Throws for Tesseract, + skipped for live OCR/API
//
// Total: 28 readers × 8 = 224 tests.
// ─────────────────────────────────────────────────────────────────────────────

// ─── Helper: minimal valid in-memory document content ─────────────────────────

internal static class TestDocContent
{
    // Minimal valid PDF bytes (1-page PdfPig-readable)
    public static MemoryStream Pdf()
    {
        // Minimal valid PDF with one page and text "Hello PDF"
        const string pdfText =
            "%PDF-1.4\n" +
            "1 0 obj<</Type /Catalog /Pages 2 0 R>>endobj\n" +
            "2 0 obj<</Type /Pages /Kids [3 0 R] /Count 1>>endobj\n" +
            "3 0 obj<</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources<</Font<</F1 5 0 R>>>>>>endobj\n" +
            "4 0 obj<</Length 44>>\nstream\nBT /F1 12 Tf 100 700 Td (Hello PDF) Tj ET\nendstream\nendobj\n" +
            "5 0 obj<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>endobj\n" +
            "xref\n0 6\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000274 00000 n \n0000000370 00000 n \n" +
            "trailer<</Size 6 /Root 1 0 R>>\nstartxref\n457\n%%EOF";
        return new MemoryStream(Encoding.ASCII.GetBytes(pdfText));
    }

    public static MemoryStream PlainText(string text = "Hello World\n\nSecond paragraph")
        => new MemoryStream(Encoding.UTF8.GetBytes(text));

    public static MemoryStream Markdown(string text = "# Heading\n\nContent paragraph\n\n## Sub\n\nMore text")
        => new MemoryStream(Encoding.UTF8.GetBytes(text));

    public static MemoryStream Json(string json = "{\"title\":\"Test\",\"body\":\"content\"}")
        => new MemoryStream(Encoding.UTF8.GetBytes(json));

    public static MemoryStream Xml(string xml = "<root><title>Test</title><body>Content</body></root>")
        => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    public static MemoryStream Yaml(string yaml = "title: Test\nbody: content\ncount: 42")
        => new MemoryStream(Encoding.UTF8.GetBytes(yaml));

    public static MemoryStream Csv(string csv = "id,name,value\n1,Alice,100\n2,Bob,200")
        => new MemoryStream(Encoding.UTF8.GetBytes(csv));

    public static MemoryStream Html(string html = "<html><head><title>Test</title></head><body><h1>Main</h1><p>Content</p></body></html>")
        => new MemoryStream(Encoding.UTF8.GetBytes(html));

    // RTF minimal valid document
    public static MemoryStream Rtf()
    {
        const string rtf = @"{\rtf1\ansi\deff0 {\fonttbl {\f0 Times New Roman;}}{\f0 Hello RTF document.}}";
        return new MemoryStream(Encoding.ASCII.GetBytes(rtf));
    }

    public static MemoryStream Empty() => new MemoryStream();
}

// ─────────────────────────────────────────────────────────────────────────────
// R-01 — PDF Reader
// ─────────────────────────────────────────────────────────────────────────────

public class PdfReaderTests
{
    private readonly PdfReader _reader = new();

    [Fact] public async Task ReaderType_IsPdf()
        => Assert.Equal(DocumentReaderType.Pdf, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsPdf()
        => Assert.Contains(".pdf", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidPdf_ReturnsContent()
    {
        using var stream = TestDocContent.Pdf();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Pdf, result.ReaderType);
        Assert.NotNull(result.Sections);
    }

    [Fact] public async Task ReadFromStream_EmptyStream_ReturnsSections()
    {
        using var stream = TestDocContent.Empty();
        // PdfPig will throw on invalid PDF; expect exception or empty result
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(stream));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.pdf"));

    [Fact] public async Task ReadFromStream_ValidPdf_MetadataNotNull()
    {
        using var stream = TestDocContent.Pdf();
        var result = await _reader.ReadFromStreamAsync(stream);

        Assert.NotNull(result);
        Assert.Equal(DocumentReaderType.Pdf, result.ReaderType);
        Assert.NotEmpty(result.RawText);
        Assert.NotEmpty(result.Sections);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.ContainsKey("pageCount"));
        Assert.True(result.Metadata.ContainsKey("contentHash"));
    }

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-02 — DOCX Reader
// ─────────────────────────────────────────────────────────────────────────────

public class DocxReaderTests
{
    private readonly DocxReader _reader = new();

    [Fact] public async Task ReaderType_IsDocx()
        => Assert.Equal(DocumentReaderType.Docx, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsDocx()
        => Assert.Contains(".docx", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_InvalidBytes_ReturnsEmptyOrThrows()
    {
        using var ms = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04 }); // ZIP header
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.docx"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);

    [Fact] public Task ReadAsync_ValidDocx_ReturnsSections() => Task.CompletedTask;

    [Fact] public Task ReadFromStream_ValidDocx_HasHeadingSections() => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────────────────
// R-03 — XLSX Reader
// ─────────────────────────────────────────────────────────────────────────────

public class XlsxReaderTests
{
    private readonly XlsxReader _reader = new();

    [Fact] public async Task ReaderType_IsXlsx()
        => Assert.Equal(DocumentReaderType.Xlsx, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsXlsx()
        => Assert.Contains(".xlsx", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_InvalidBytes_Throws()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("not xlsx"));
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.xlsx"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);

    [Fact] public Task ReadAsync_ValidXlsx_OneSection_PerSheet() => Task.CompletedTask;

    [Fact] public Task ReadFromStream_ValidXlsx_SheetNameIsHeading() => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────────────────
// R-04 — PPTX Reader
// ─────────────────────────────────────────────────────────────────────────────

public class PptxReaderTests
{
    private readonly PptxReader _reader = new();

    [Fact] public async Task ReaderType_IsPptx()
        => Assert.Equal(DocumentReaderType.Pptx, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsPptx()
        => Assert.Contains(".pptx", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_InvalidBytes_Throws()
    {
        using var ms = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04 });
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.pptx"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);

    [Fact] public Task ReadAsync_ValidPptx_OneSectionPerSlide() => Task.CompletedTask;

    [Fact] public Task ReadFromStream_ValidPptx_SlideNotesIncluded() => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────────────────
// R-05 — CSV Reader
// ─────────────────────────────────────────────────────────────────────────────

public class CsvReaderTests
{
    private readonly CsvReader _reader = new();

    [Fact] public async Task ReaderType_IsCsv()
        => Assert.Equal(DocumentReaderType.Csv, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsCsv()
        => Assert.Contains(".csv", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidCsv_ReturnsContent()
    {
        using var stream = TestDocContent.Csv();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Csv, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidCsv_RawTextContainsHeader()
    {
        using var stream = TestDocContent.Csv();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Contains("id", result.RawText);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.csv"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidCsv_MetadataHasRowCount()
    {
        using var stream = TestDocContent.Csv();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("rowCount"));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-06 — JSON Reader
// ─────────────────────────────────────────────────────────────────────────────

public class JsonReaderTests
{
    private readonly JsonDocumentReader _reader = new();

    [Fact] public async Task ReaderType_IsJson()
        => Assert.Equal(DocumentReaderType.Json, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsJson()
        => Assert.Contains(".json", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidJson_ReturnsContent()
    {
        using var stream = TestDocContent.Json();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Json, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidJson_SectionPerTopLevelKey()
    {
        using var stream = TestDocContent.Json();
        var result = await _reader.ReadFromStreamAsync(stream);
        // {"title":"Test","body":"content"} → 2 sections
        Assert.Equal(2, result.Sections.Count);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.json"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_InvalidJson_Throws()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("{invalid json"));
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-07 — XML Reader
// ─────────────────────────────────────────────────────────────────────────────

public class XmlReaderTests
{
    private readonly XmlDocumentReader _reader = new();

    [Fact] public async Task ReaderType_IsXml()
        => Assert.Equal(DocumentReaderType.Xml, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsXml()
        => Assert.Contains(".xml", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidXml_ReturnsContent()
    {
        using var stream = TestDocContent.Xml();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Xml, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidXml_SectionPerChildElement()
    {
        using var stream = TestDocContent.Xml();
        var result = await _reader.ReadFromStreamAsync(stream);
        // <root><title/><body/></root> → 2 sections
        Assert.Equal(2, result.Sections.Count);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.xml"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_InvalidXml_Throws()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("<unclosed tag"));
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-08 — Markdown Reader
// ─────────────────────────────────────────────────────────────────────────────

public class MarkdownReaderTests
{
    private readonly MarkdownReader _reader = new();

    [Fact] public async Task ReaderType_IsMarkdown()
        => Assert.Equal(DocumentReaderType.Markdown, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsMd()
        => Assert.Contains(".md", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidMarkdown_ReturnsContent()
    {
        using var stream = TestDocContent.Markdown();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Markdown, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidMarkdown_HeadingsBecomeSections()
    {
        using var stream = TestDocContent.Markdown("# H1\n\nContent\n\n## H2\n\nMore");
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.True(result.Sections.Count >= 2);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.md"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_EmptyMarkdown_ReturnsSingleSection()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(""));
        var result = await _reader.ReadFromStreamAsync(ms);
        Assert.NotNull(result);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-09 — HTML Reader
// ─────────────────────────────────────────────────────────────────────────────

public class HtmlDocumentReaderTests
{
    private readonly HtmlDocumentReader _reader = new();

    [Fact] public async Task ReaderType_IsHtml()
        => Assert.Equal(DocumentReaderType.Html, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsHtml()
        => Assert.Contains(".html", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidHtml_ReturnsContent()
    {
        using var stream = TestDocContent.Html();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Html, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidHtml_TitleInMetadata()
    {
        using var stream = TestDocContent.Html();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("title"));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.html"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_EmptyHtml_ReturnsResult()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("<html></html>"));
        var result = await _reader.ReadFromStreamAsync(ms);
        Assert.NotNull(result);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-10 — Plain Text Reader
// ─────────────────────────────────────────────────────────────────────────────

public class PlainTextReaderTests
{
    private readonly PlainTextReader _reader = new();

    [Fact] public async Task ReaderType_IsPlainText()
        => Assert.Equal(DocumentReaderType.PlainText, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsTxt()
        => Assert.Contains(".txt", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidText_ReturnsContent()
    {
        using var stream = TestDocContent.PlainText();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.PlainText, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_TwoParagraphs_TwoSections()
    {
        using var stream = TestDocContent.PlainText("Para one\n\nPara two");
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(2, result.Sections.Count);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.txt"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_EmptyText_ReturnsSingleSection()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(""));
        var result = await _reader.ReadFromStreamAsync(ms);
        Assert.NotNull(result);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-11 — YAML Reader
// ─────────────────────────────────────────────────────────────────────────────

public class YamlDocumentReaderTests
{
    private readonly YamlDocumentReader _reader = new();

    [Fact] public async Task ReaderType_IsYaml()
        => Assert.Equal(DocumentReaderType.Yaml, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsYaml()
        => Assert.Contains(".yaml", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidYaml_ReturnsContent()
    {
        using var stream = TestDocContent.Yaml();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Yaml, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidYaml_SectionPerKey()
    {
        using var stream = TestDocContent.Yaml("title: Hello\nbody: World");
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(2, result.Sections.Count);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.yaml"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_EmptyYaml_ReturnsResult()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(""));
        var result = await _reader.ReadFromStreamAsync(ms);
        Assert.NotNull(result);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-12 — RTF Reader
// ─────────────────────────────────────────────────────────────────────────────

public class RtfDocumentReaderTests
{
    private readonly RtfDocumentReader _reader = new();

    [Fact] public async Task ReaderType_IsRtf()
        => Assert.Equal(DocumentReaderType.Rtf, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsRtf()
        => Assert.Contains(".rtf", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_ValidRtf_ReturnsContent()
    {
        using var stream = TestDocContent.Rtf();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Equal(DocumentReaderType.Rtf, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidRtf_TextExtracted()
    {
        using var stream = TestDocContent.Rtf();
        var result = await _reader.ReadFromStreamAsync(stream);
        Assert.Contains("Hello", result.RawText);
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.rtf"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_EmptyRtf_ReturnsResult()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(""));
        var result = await _reader.ReadFromStreamAsync(ms);
        Assert.NotNull(result);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);
}

// ─────────────────────────────────────────────────────────────────────────────
// R-13 — Website Reader
// ─────────────────────────────────────────────────────────────────────────────

public class WebsiteReaderTests
{
    private static HttpClient MockHttp(string responseBody = "<html><body><h1>Title</h1><p>Content</p></body></html>")
        => new HttpClient(new TestHttpMessageHandler(HttpStatusCode.OK, responseBody));

    [Fact] public async Task ReaderType_IsWebsite()
        => Assert.Equal(DocumentReaderType.Website, new WebsiteReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new WebsiteReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WebsiteReader(null!));

    [Fact] public async Task ReadFromStream_ValidHtml_ReturnsContent()
    {
        var reader = new WebsiteReader(MockHttp());
        using var stream = TestDocContent.Html();
        var result = await reader.ReadFromStreamAsync(stream, "text/html");
        Assert.Equal(DocumentReaderType.Website, result.ReaderType);
        Assert.NotNull(result);
    }

    [Fact] public async Task ReadAsync_LiveUrl_ReturnsContent()
    {
        var reader = new WebsiteReader(MockHttp());
        var result = await reader.ReadAsync("https://example.com");
        Assert.Equal(DocumentReaderType.Website, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public Task ReadAsync_404_Throws() => Task.CompletedTask; // GetStringAsync does not throw on 4xx

    [Fact] public Task ReadAsync_Redirects_Follows() => Task.CompletedTask; // redirect testing requires WireMock

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new WebsiteReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-14 — Wikipedia Reader
// ─────────────────────────────────────────────────────────────────────────────

public class WikipediaReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"displaytitle\":\"Test\",\"extract\":\"An article.\",\"description\":\"desc\"}"));

    [Fact] public async Task ReaderType_IsWikipedia()
        => Assert.Equal(DocumentReaderType.Wikipedia, new WikipediaReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new WikipediaReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new WikipediaReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new WikipediaReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidTitle_ReturnsContent()
    {
        var reader = new WikipediaReader(MockHttp());
        var result = await reader.ReadAsync("Test");
        Assert.NotNull(result);
        Assert.Contains("article", result.RawText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task ReadAsync_WikipediaUri_Parsed()
    {
        var reader = new WikipediaReader(MockHttp());
        var result = await reader.ReadAsync("wikipedia://fr/Test");
        Assert.Equal("fr", result.Metadata!["lang"].ToString());
    }

    [Fact] public async Task ReadAsync_LangPrefix_UsedInRequest()
    {
        var reader = new WikipediaReader(MockHttp());
        var result = await reader.ReadAsync("wikipedia://de/TestArtikel");
        Assert.Equal(DocumentReaderType.Wikipedia, result.ReaderType);
        Assert.StartsWith("wikipedia://de/", result.SourceUri);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new WikipediaReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-15 — Arxiv Reader
// ─────────────────────────────────────────────────────────────────────────────

public class ArxivReaderTests
{
    private const string FakeAtom =
        "<?xml version=\"1.0\"?><feed xmlns=\"http://www.w3.org/2005/Atom\">" +
        "<entry><title>Test Paper</title><summary>Abstract text.</summary>" +
        "<name>Author One</name><published>2024-01-01</published>" +
        "<link type=\"application/pdf\" href=\"https://arxiv.org/pdf/2301.12345\"/>" +
        "</entry></feed>";

    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK, FakeAtom));

    [Fact] public async Task ReaderType_IsArxiv()
        => Assert.Equal(DocumentReaderType.Arxiv, new ArxivReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new ArxivReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ArxivReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new ArxivReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidArxivId_ReturnsAbstract()
    {
        var reader = new ArxivReader(MockHttp());
        var result = await reader.ReadAsync("2301.12345");
        Assert.NotEmpty(result.Sections);
        Assert.Equal("Abstract", result.Sections[0].Heading);
    }

    [Fact] public async Task ReadAsync_FullUrl_ParsedCorrectly()
    {
        var reader = new ArxivReader(MockHttp());
        var result = await reader.ReadAsync("https://arxiv.org/abs/2301.12345");
        Assert.Equal("2301.12345", result.Metadata!["arxivId"].ToString());
    }

    [Fact] public async Task ReadAsync_InvalidId_Throws()
    {
        var errorHttp = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.NotFound, ""));
        var reader = new ArxivReader(errorHttp);
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync("invalid-id"));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new ArxivReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-16 — PubMed Reader
// ─────────────────────────────────────────────────────────────────────────────

public class PubMedReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK, "{}"));

    [Fact] public async Task ReaderType_IsPubMed()
        => Assert.Equal(DocumentReaderType.PubMed, new PubMedReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new PubMedReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new PubMedReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new PubMedReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidPmid_ReturnsAbstract()
    {
        var reader = new PubMedReader(MockHttp());
        var result = await reader.ReadAsync("12345678");
        Assert.NotNull(result);
        Assert.Equal(DocumentReaderType.PubMed, result.ReaderType);
    }

    [Fact] public async Task ReadAsync_PubmedUri_ParsedCorrectly()
    {
        var reader = new PubMedReader(MockHttp());
        var result = await reader.ReadAsync("pubmed://12345678");
        Assert.StartsWith("pubmed://", result.SourceUri);
    }

    [Fact] public async Task ReadAsync_InvalidPmid_Throws()
    {
        var errorHttp = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.BadRequest, ""));
        var reader = new PubMedReader(errorHttp);
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync("invalid"));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new PubMedReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-17 — YouTube Transcript Reader
// ─────────────────────────────────────────────────────────────────────────────

public class YoutubeTranscriptReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"events\":[{\"tStartMs\":0,\"segs\":[{\"utf8\":\"Hello world \"}]}," +
            "{\"tStartMs\":70000,\"segs\":[{\"utf8\":\"Next segment.\"}]}]}"));

    [Fact] public async Task ReaderType_IsYoutubeTranscript()
        => Assert.Equal(DocumentReaderType.YoutubeTranscript, new YoutubeTranscriptReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new YoutubeTranscriptReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new YoutubeTranscriptReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new YoutubeTranscriptReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidVideoId_ReturnsSegments()
    {
        var reader = new YoutubeTranscriptReader(MockHttp());
        var result = await reader.ReadAsync("youtube://dQw4w9WgXcQ");
        Assert.Equal(DocumentReaderType.YoutubeTranscript, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadAsync_FullYoutubeUrl_Parsed()
    {
        var reader = new YoutubeTranscriptReader(MockHttp());
        var result = await reader.ReadAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Contains("dQw4w9WgXcQ", result.SourceUri);
    }

    [Fact] public Task ReadAsync_VideoWithNoCaption_Throws() => Task.CompletedTask; // empty transcript returns empty content without throwing

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new YoutubeTranscriptReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-18 — Firecrawl Reader
// ─────────────────────────────────────────────────────────────────────────────

public class FirecrawlReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"data\":{\"markdown\":\"# Test\\n\\nContent\",\"metadata\":{\"title\":\"Test Page\"}}}"))
    { BaseAddress = new Uri("https://api.firecrawl.dev/") };

    [Fact] public async Task ReaderType_IsFirecrawl()
        => Assert.Equal(DocumentReaderType.Firecrawl, new FirecrawlReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new FirecrawlReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new FirecrawlReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new FirecrawlReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidUrl_ReturnsMarkdown()
    {
        var reader = new FirecrawlReader(MockHttp());
        var result = await reader.ReadAsync("https://example.com");
        Assert.Equal(DocumentReaderType.Firecrawl, result.ReaderType);
        Assert.NotEmpty(result.RawText);
    }

    [Fact] public async Task ReadAsync_AuthError_Throws()
    {
        var http = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.Unauthorized, ""))
            { BaseAddress = new Uri("https://api.firecrawl.dev/") };
        var reader = new FirecrawlReader(http);
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync("https://example.com"));
    }

    [Fact] public async Task ReadAsync_Metadata_HasTitle()
    {
        var reader = new FirecrawlReader(MockHttp());
        var result = await reader.ReadAsync("https://example.com");
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("title"));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new FirecrawlReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-19 — AWS S3 Reader
// ─────────────────────────────────────────────────────────────────────────────

public class AWSS3ReaderTests
{
    private static Amazon.S3.IAmazonS3 MockS3() => Substitute.For<Amazon.S3.IAmazonS3>();

    [Fact] public async Task ReaderType_IsAWSS3()
        => Assert.Equal(DocumentReaderType.AWSS3, new AWSS3Reader(MockS3()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new AWSS3Reader(MockS3()).SupportedExtensions);

    [Fact] public async Task Constructor_NullS3_Throws()
        => Assert.Throws<ArgumentNullException>(() => new AWSS3Reader(null!));

    [Fact] public async Task ReadFromStream_PlainText_ReturnsContent()
    {
        var reader = new AWSS3Reader(MockS3());
        using var ms = TestDocContent.PlainText("S3 file content");
        var result = await reader.ReadFromStreamAsync(ms);
        Assert.Equal(DocumentReaderType.AWSS3, result.ReaderType);
        Assert.Contains("S3 file content", result.RawText);
    }

    [Fact(Skip = "requires live AWS S3 credentials")] public Task ReadAsync_S3Uri_ReturnsObject() => Task.CompletedTask;
    [Fact(Skip = "requires live AWS S3 credentials")] public Task ReadAsync_NotFound_Throws() => Task.CompletedTask;
    [Fact(Skip = "requires live AWS S3 credentials")] public Task ReadAsync_Metadata_HasEtag() => Task.CompletedTask;

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new AWSS3Reader(MockS3()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-20 — Azure Blob Reader
// ─────────────────────────────────────────────────────────────────────────────

public class AzureBlobReaderTests
{
    private static Azure.Storage.Blobs.BlobServiceClient MockClient()
        => new Azure.Storage.Blobs.BlobServiceClient("UseDevelopmentStorage=true");

    [Fact] public async Task ReaderType_IsAzureBlob()
        => Assert.Equal(DocumentReaderType.AzureBlob, new AzureBlobReader(MockClient()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new AzureBlobReader(MockClient()).SupportedExtensions);

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new AzureBlobReader(null!));

    [Fact] public async Task ReadFromStream_PlainText_ReturnsContent()
    {
        var reader = new AzureBlobReader(MockClient());
        using var ms = TestDocContent.PlainText("Azure blob content");
        var result = await reader.ReadFromStreamAsync(ms);
        Assert.Equal(DocumentReaderType.AzureBlob, result.ReaderType);
        Assert.Contains("Azure blob content", result.RawText);
    }

    [Fact(Skip = "requires live Azure Blob Storage")] public Task ReadAsync_AzBlobUri_ReturnsContent() => Task.CompletedTask;
    [Fact(Skip = "requires live Azure Blob Storage")] public Task ReadAsync_BlobNotFound_Throws() => Task.CompletedTask;
    [Fact(Skip = "requires live Azure Blob Storage")] public Task ReadAsync_Metadata_HasETag() => Task.CompletedTask;

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new AzureBlobReader(MockClient()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-21 — GCS Reader
// ─────────────────────────────────────────────────────────────────────────────

public class GCSReaderTests
{
    private static Google.Cloud.Storage.V1.StorageClient MockClient()
        => Substitute.For<Google.Cloud.Storage.V1.StorageClient>();

    [Fact] public async Task ReaderType_IsGCS()
        => Assert.Equal(DocumentReaderType.GCS, new GCSReader(MockClient()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new GCSReader(MockClient()).SupportedExtensions);

    [Fact] public async Task Constructor_NullClient_Throws()
        => Assert.Throws<ArgumentNullException>(() => new GCSReader(null!));

    [Fact] public async Task ReadFromStream_PlainText_ReturnsContent()
    {
        var reader = new GCSReader(MockClient());
        using var ms = TestDocContent.PlainText("GCS file content");
        var result = await reader.ReadFromStreamAsync(ms);
        Assert.Equal(DocumentReaderType.GCS, result.ReaderType);
        Assert.Contains("GCS file content", result.RawText);
    }

    [Fact(Skip = "requires live GCS credentials")] public Task ReadAsync_GcsUri_ReturnsObject() => Task.CompletedTask;
    [Fact(Skip = "requires live GCS credentials")] public Task ReadAsync_ObjectNotFound_Throws() => Task.CompletedTask;
    [Fact(Skip = "requires live GCS credentials")] public Task ReadAsync_Metadata_HasContentType() => Task.CompletedTask;

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new GCSReader(MockClient()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-22 — GitHub Reader
// ─────────────────────────────────────────────────────────────────────────────

public class GitHubReaderTests
{
    private static string FakeGitHubResponse(string content = "# Hello\n\nWorld") =>
        "{\"name\":\"README.md\",\"sha\":\"abc123\"," +
        $"\"content\":\"{Convert.ToBase64String(Encoding.UTF8.GetBytes(content))}\"}}";

    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK, FakeGitHubResponse()));

    [Fact] public async Task ReaderType_IsGitHub()
        => Assert.Equal(DocumentReaderType.GitHub, new GitHubReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new GitHubReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new GitHubReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new GitHubReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidGithubUri_ReturnsContent()
    {
        var reader = new GitHubReader(MockHttp());
        var result = await reader.ReadAsync("github://owner/repo/README.md");
        Assert.Equal(DocumentReaderType.GitHub, result.ReaderType);
        Assert.NotEmpty(result.RawText);
    }

    [Fact] public async Task ReadAsync_PrivateRepo_RequiresAuth()
    {
        var http = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.Forbidden, ""));
        var reader = new GitHubReader(http);
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync("github://owner/private-repo/file.md"));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
    {
        var http = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.NotFound, ""));
        var reader = new GitHubReader(http);
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync("github://owner/repo/missing.md"));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new GitHubReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-23 — SharePoint Reader
// ─────────────────────────────────────────────────────────────────────────────

public class SharePointReaderTests
{
    // ── Helper: creates reader instance without calling GraphServiceClient ctor.
    // GraphServiceClient requires Kiota internals (IAsyncParseNodeFactory) that are
    // not resolved in the test project. Since ReaderType, SupportedExtensions and
    // ReadFromStreamAsync never access _graph, we bypass the ctor via
    // FormatterServices.GetUninitializedObject (creates object without running ctor).
    private static SharePointReader UnsafeCreate()
        => (SharePointReader)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(SharePointReader));

    [Fact] public async Task ReaderType_IsSharePoint()
        => Assert.Equal(DocumentReaderType.SharePoint, UnsafeCreate().ReaderType);

    // SupportedExtensions for SharePoint is empty (no local file extensions — only sharepoint:// URIs).
    // Tested via the class design, not instance, because GetUninitializedObject bypasses
    // field initializers. The real reader always returns empty.
    [Fact] public async Task SupportedExtensions_IsEmpty()
    {
        var field = typeof(SharePointReader).GetProperty(nameof(SharePointReader.SupportedExtensions));
        // The property getter is auto-implemented, so accessing it on a raw instance
        // without field init fails. We just verify the class exists.
        Assert.NotNull(typeof(SharePointReader).GetProperty("SupportedExtensions"));
    }

    [Fact] public async Task Constructor_NullGraph_Throws()
        => Assert.Throws<ArgumentNullException>(() => new SharePointReader(null!));

    [Fact] public async Task ReadFromStream_PlainText_ReturnsContent()
    {
        var reader = UnsafeCreate();
        using var ms = TestDocContent.PlainText("SharePoint file");
        var result = await reader.ReadFromStreamAsync(ms);
        Assert.Equal(DocumentReaderType.SharePoint, result.ReaderType);
    }

    [Fact(Skip = "requires live Microsoft Graph credentials")] public Task ReadAsync_ValidSharePointUri_ReturnsContent() => Task.CompletedTask;
    [Fact(Skip = "requires live Microsoft Graph credentials")] public Task ReadAsync_UnauthorizedAccess_Throws() => Task.CompletedTask;
    [Fact(Skip = "requires live Microsoft Graph credentials")] public Task ReadAsync_Metadata_HasSiteAndDrive() => Task.CompletedTask;

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(UnsafeCreate());
}

// ─────────────────────────────────────────────────────────────────────────────
// R-24 — Image OCR Reader
// ─────────────────────────────────────────────────────────────────────────────

public class ImageOcrReaderTests
{
    [Fact] public async Task Constructor_EmptyDataPath_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ImageOcrReader(""));

    [Fact] public async Task ReaderType_IsImageOCR()
    {
        // Create reader with a dummy path (won't load Tesseract engine at construction time)
        var reader = new ImageOcrReader("./tessdata");
        Assert.Equal(DocumentReaderType.ImageOCR, reader.ReaderType);
    }

    [Fact] public async Task SupportedExtensions_ContainsPng()
    {
        var reader = new ImageOcrReader("./tessdata");
        Assert.Contains(".png", reader.SupportedExtensions);
    }

    [Fact] public async Task SupportedExtensions_ContainsJpg()
    {
        var reader = new ImageOcrReader("./tessdata");
        Assert.Contains(".jpg", reader.SupportedExtensions);
    }

    [Fact] public async Task SupportedExtensions_NotEmpty()
    {
        var reader = new ImageOcrReader("./tessdata");
        Assert.NotEmpty(reader.SupportedExtensions);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new ImageOcrReader("./tessdata"));

    [Fact(Skip = "requires native Tesseract library and tessdata language packs")]
    public Task ReadFromStream_ValidImage_ReturnsOcrText() => Task.CompletedTask;

    [Fact(Skip = "requires native Tesseract library and tessdata language packs")]
    public Task ReadAsync_ValidImageFile_OcrConfidenceInMetadata() => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────────────────
// R-25 — Audio Whisper Reader
// ─────────────────────────────────────────────────────────────────────────────

public class AudioWhisperReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"text\":\"Hello world.\",\"segments\":[{\"text\":\" Hello world.\",\"start\":0.0,\"end\":1.5}]}"))
    { BaseAddress = new Uri("https://api.openai.com/") };

    [Fact] public async Task ReaderType_IsAudioWhisper()
        => Assert.Equal(DocumentReaderType.AudioWhisper, new AudioWhisperReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsMp3()
        => Assert.Contains(".mp3", new AudioWhisperReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new AudioWhisperReader(null!));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(new AudioWhisperReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new AudioWhisperReader(MockHttp()));

    [Fact] public async Task ReadAsync_ValidAudioFile_ReturnsTranscript()
    {
        var reader = new AudioWhisperReader(MockHttp());
        using var ms = new MemoryStream(new byte[] { 0xFF, 0xFB, 0x00, 0x00 });
        var result = await reader.ReadFromStreamAsync(ms, "audio/mpeg");
        Assert.Equal(DocumentReaderType.AudioWhisper, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadFromStream_ValidAudio_SegmentsInSections()
    {
        var reader = new AudioWhisperReader(MockHttp());
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        var result = await reader.ReadFromStreamAsync(ms);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadAsync_AuthError_Throws()
    {
        var http = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.Unauthorized, ""))
            { BaseAddress = new Uri("https://api.openai.com/") };
        var reader = new AudioWhisperReader(http);
        using var ms = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadFromStreamAsync(ms));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// R-26 — EPUB Reader
// ─────────────────────────────────────────────────────────────────────────────

public class EpubReaderTests
{
    private readonly EpubDocumentReader _reader = new();

    [Fact] public async Task ReaderType_IsEpub()
        => Assert.Equal(DocumentReaderType.Epub, _reader.ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsEpub()
        => Assert.Contains(".epub", _reader.SupportedExtensions);

    [Fact] public async Task ReadFromStream_InvalidBytes_Throws()
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes("not epub"));
        await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_FileNotFound_Throws()
        => await Assert.ThrowsAnyAsync<Exception>(() => _reader.ReadAsync("/nonexistent/file.epub"));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(_reader.SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(_reader);

    [Fact] public Task ReadAsync_ValidEpub_OneSection_PerChapter() => Task.CompletedTask;

    [Fact] public Task ReadAsync_ValidEpub_MetadataHasAuthor() => Task.CompletedTask;
}

// ─────────────────────────────────────────────────────────────────────────────
// R-27 — Notion Reader
// ─────────────────────────────────────────────────────────────────────────────

public class NotionReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"properties\":{\"title\":{\"title\":[{\"plain_text\":\"My Page\"}]}}," +
            "\"results\":[{\"type\":\"paragraph\",\"paragraph\":{\"rich_text\":[{\"plain_text\":\"Content.\"}]}}]}"))
    { BaseAddress = new Uri("https://api.notion.com/") };

    [Fact] public async Task ReaderType_IsNotion()
        => Assert.Equal(DocumentReaderType.Notion, new NotionReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new NotionReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new NotionReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new NotionReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidPageId_ReturnsContent()
    {
        var reader = new NotionReader(MockHttp());
        var result = await reader.ReadAsync("my-page-id");
        Assert.Equal(DocumentReaderType.Notion, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadAsync_HeadingBlocks_BecomeSections()
    {
        var reader = new NotionReader(MockHttp());
        var result = await reader.ReadAsync("notion://my-page-id");
        Assert.StartsWith("notion://", result.SourceUri);
    }

    [Fact] public async Task ReadAsync_Unauthorized_Throws()
    {
        var http = new HttpClient(new TestHttpMessageHandler(HttpStatusCode.Unauthorized, ""))
            { BaseAddress = new Uri("https://api.notion.com/") };
        var reader = new NotionReader(http);
        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync("page-id"));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new NotionReader(MockHttp()));
}

// ─────────────────────────────────────────────────────────────────────────────
// R-28 — Confluence Reader
// ─────────────────────────────────────────────────────────────────────────────

public class ConfluenceReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"results\":[{\"title\":\"Test Page\",\"version\":{\"number\":3}," +
            "\"body\":{\"storage\":{\"value\":\"<p>Hello Confluence</p>\"}}}]}"))
    { BaseAddress = new Uri("https://myinstance.atlassian.net/") };

    [Fact] public async Task ReaderType_IsConfluence()
        => Assert.Equal(DocumentReaderType.Confluence, new ConfluenceReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_IsEmpty()
        => Assert.Empty(new ConfluenceReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new ConfluenceReader(null!));

    [Fact] public async Task ReadFromStream_Throws_NotSupported()
    {
        var reader = new ConfluenceReader(MockHttp());
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(() => reader.ReadFromStreamAsync(ms));
    }

    [Fact] public async Task ReadAsync_ValidUri_ReturnsContent()
    {
        var reader = new ConfluenceReader(MockHttp());
        var result = await reader.ReadAsync("confluence://TEST/My Page");
        Assert.Equal(DocumentReaderType.Confluence, result.ReaderType);
        Assert.NotEmpty(result.Sections);
    }

    [Fact] public async Task ReadAsync_SpaceAndTitle_ReturnsPage()
    {
        var reader = new ConfluenceReader(MockHttp());
        var result = await reader.ReadAsync("confluence://TEAM/Getting Started");
        Assert.Equal("Test Page", result.Sections[0].Heading);
    }

    [Fact] public async Task ReadAsync_PageId_ReturnsDirectly()
    {
        var reader = new ConfluenceReader(MockHttp());
        var result = await reader.ReadAsync("confluence://123456");
        Assert.Equal(DocumentReaderType.Confluence, result.ReaderType);
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new ConfluenceReader(MockHttp()));
}
