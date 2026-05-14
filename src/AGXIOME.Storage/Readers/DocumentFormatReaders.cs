using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using RtfPipe;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using VersOne.Epub;
using YamlDotNet.RepresentationModel;

namespace AGXIOME.Storage.Readers;

// ─────────────────────────────────────────────────────────────────────────────
// R-01..R-12, R-26 — Local Document Format Readers
//
// All readers implement IDocumentReader.
// ReadAsync  → reads from file system path.
// ReadFromStreamAsync → reads from in-memory or file stream (used by tests and pipelines).
//
// Section extraction strategy:
//   PDF/DOCX/PPTX   → paragraph/slide level sections
//   XLSX             → one section per worksheet
//   CSV              → header + row sections
//   JSON/XML/YAML    → one section per top-level key/element
//   Markdown/HTML    → heading-based sections
//   PlainText/RTF    → single section with full content
//   EPUB             → one section per chapter/nav point
// ─────────────────────────────────────────────────────────────────────────────

// ─── R-01 — PDF ───────────────────────────────────────────────────────────────

/// <summary>R-01 — PDF text reader using PdfPig (no native libs required).
/// Extracts page text concatenated, with each page as a DocumentSection.
/// SHA-256 content hash is included in metadata for deduplication.</summary>
public sealed class PdfReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Pdf;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".pdf" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, "application/pdf", ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var pdf = PdfDocument.Open(ms);
        var sb = new StringBuilder();
        var sections = new List<DocumentSection>();

        foreach (var page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            var pageText = page.Text;
            sb.Append(pageText);
            sb.Append('\n');
            sections.Add(new DocumentSection(
                Heading: $"Page {page.Number}",
                Content: pageText,
                Level: 1,
                Metadata: new Dictionary<string, object>
                {
                    ["pageNumber"] = page.Number,
                    ["wordCount"]  = page.GetWords().Count(),
                }));
        }

        var rawText = sb.ToString();
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawText)));

        var meta = new Dictionary<string, object>
        {
            ["pageCount"]  = pdf.NumberOfPages,
            ["contentHash"] = hash,
        };

        return Task.FromResult(new DocumentContent("stream://pdf", DocumentReaderType.Pdf, rawText, sections, meta));
    }
}

// ─── R-02 — DOCX ──────────────────────────────────────────────────────────────

/// <summary>R-02 — DOCX reader using DocumentFormat.OpenXml.
/// Extracts paragraphs grouped by heading styles into DocumentSections.</summary>
public sealed class DocxReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Docx;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".docx" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, null, ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null)
            return Task.FromResult(new DocumentContent("stream://docx", DocumentReaderType.Docx, "", Array.Empty<DocumentSection>()));

        var sections = new List<DocumentSection>();
        var currentHeading = (string?)null;
        var currentLevel = 0;
        var sb = new StringBuilder();
        var contentSb = new StringBuilder();

        foreach (var para in body.Descendants<Paragraph>())
        {
            ct.ThrowIfCancellationRequested();
            var style = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
            var text = string.Concat(para.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));

            if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase) && int.TryParse(style.Replace("Heading", "", StringComparison.OrdinalIgnoreCase), out var lvl))
            {
                // Flush previous section
                if (contentSb.Length > 0)
                    sections.Add(new DocumentSection(currentHeading, contentSb.ToString().Trim(), currentLevel));
                currentHeading = text;
                currentLevel = lvl;
                contentSb.Clear();
            }
            else
            {
                contentSb.AppendLine(text);
            }
            sb.AppendLine(text);
        }

        if (contentSb.Length > 0)
            sections.Add(new DocumentSection(currentHeading, contentSb.ToString().Trim(), currentLevel));

        if (sections.Count == 0 && sb.Length > 0)
            sections.Add(new DocumentSection(null, sb.ToString().Trim(), 0));

        var meta = new Dictionary<string, object> { ["paragraphCount"] = sections.Count };
        if (doc.CoreFilePropertiesPart is { } corePart)
        {
            try
            {
                var coreXml = XDocument.Load(corePart.GetStream());
                if (coreXml.Root?.Element("{http://purl.org/dc/elements/1.1/}title") is { } titleEl)
                    meta["title"] = titleEl.Value;
            }
            catch { /* core properties are optional */ }
        }

        return Task.FromResult(new DocumentContent("stream://docx", DocumentReaderType.Docx, sb.ToString(), sections, meta));
    }
}

// ─── R-03 — XLSX ──────────────────────────────────────────────────────────────

/// <summary>R-03 — XLSX reader using ClosedXML.
/// One DocumentSection per worksheet; cells serialized as tab-separated rows.</summary>
public sealed class XlsxReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Xlsx;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".xlsx", ".xlsm" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, null, ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var workbook = new XLWorkbook(ms);
        var sections = new List<DocumentSection>();
        var fullSb = new StringBuilder();

        foreach (var ws in workbook.Worksheets)
        {
            ct.ThrowIfCancellationRequested();
            var wsSb = new StringBuilder();
            var range = ws.RangeUsed();
            if (range is null) continue;

            foreach (var row in range.Rows())
            {
                var cells = row.Cells().Select(c => c.GetFormattedString()).ToList();
                wsSb.AppendLine(string.Join("\t", cells));
            }

            var content = wsSb.ToString();
            sections.Add(new DocumentSection(
                Heading: ws.Name,
                Content: content,
                Level: 1,
                Metadata: new Dictionary<string, object>
                {
                    ["rowCount"]    = range.RowCount(),
                    ["columnCount"] = range.ColumnCount(),
                }));
            fullSb.Append(content);
        }

        var meta = new Dictionary<string, object> { ["sheetCount"] = workbook.Worksheets.Count };
        return Task.FromResult(new DocumentContent("stream://xlsx", DocumentReaderType.Xlsx, fullSb.ToString(), sections, meta));
    }
}

// ─── R-04 — PPTX ──────────────────────────────────────────────────────────────

/// <summary>R-04 — PPTX reader using DocumentFormat.OpenXml.
/// One DocumentSection per slide; extracts text and slide notes.</summary>
public sealed class PptxReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Pptx;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".pptx", ".ppt" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, null, ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var pres = PresentationDocument.Open(ms, false);
        var slideIds = pres.PresentationPart?.Presentation?.SlideIdList?.ChildElements;
        var sections = new List<DocumentSection>();
        var fullSb = new StringBuilder();
        var slideNum = 0;

        if (slideIds is null)
            return Task.FromResult(new DocumentContent("stream://pptx", DocumentReaderType.Pptx, "", Array.Empty<DocumentSection>()));

        foreach (var slideIdRef in slideIds)
        {
            ct.ThrowIfCancellationRequested();
            slideNum++;
            if (slideIdRef is not DocumentFormat.OpenXml.Presentation.SlideId sId) continue;
            var relId = sId.RelationshipId?.Value;
            if (relId is null) continue;

            var slidePart = (SlidePart?)pres.PresentationPart?.GetPartById(relId);
            if (slidePart is null) continue;

            // Extract all text from slide shapes
            var slideText = string.Join(" ", slidePart.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text) ?? Enumerable.Empty<string?>());
            // Extract notes if present
            var notes = string.Join(" ", slidePart.NotesSlidePart?.NotesSlide
                ?.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
                .Select(t => t.Text) ?? Array.Empty<string?>());

            var content = notes.Length > 0 ? $"{slideText}\n\nNotes: {notes}" : slideText;
            sections.Add(new DocumentSection($"Slide {slideNum}", content, 1,
                new Dictionary<string, object> { ["slideNumber"] = slideNum, ["hasNotes"] = notes.Length > 0 }));
            fullSb.AppendLine(content);
        }

        var meta = new Dictionary<string, object> { ["slideCount"] = slideNum };
        return Task.FromResult(new DocumentContent("stream://pptx", DocumentReaderType.Pptx, fullSb.ToString(), sections, meta));
    }
}

// ─── R-05 — CSV ───────────────────────────────────────────────────────────────

/// <summary>R-05 — CSV reader using CsvHelper.
/// Header row detected automatically; each batch of rows becomes a section.</summary>
public sealed class CsvReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Csv;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".csv", ".tsv" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, null, ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null };
        using var csv = new global::CsvHelper.CsvReader(reader, config);

        var records = new List<IDictionary<string, object?>>();
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();

        while (csv.Read())
        {
            ct.ThrowIfCancellationRequested();
            var row = new Dictionary<string, object?>();
            foreach (var h in headers)
                row[h] = csv.GetField(h);
            records.Add(row);
        }

        // Header row as section heading; all records as content
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers));
        foreach (var r in records)
            sb.AppendLine(string.Join(",", headers.Select(h => r.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "")));

        var sections = new List<DocumentSection>
        {
            new DocumentSection("Data", sb.ToString().Trim(), 1,
                new Dictionary<string, object> { ["rowCount"] = records.Count, ["columnCount"] = headers.Length }),
        };

        var meta = new Dictionary<string, object>
        {
            ["headers"]   = headers,
            ["rowCount"]  = records.Count,
        };
        return Task.FromResult(new DocumentContent("stream://csv", DocumentReaderType.Csv, sb.ToString(), sections, meta));
    }
}

// ─── R-06 — JSON ──────────────────────────────────────────────────────────────

/// <summary>R-06 — JSON reader using System.Text.Json.
/// Top-level object properties become sections; arrays become a single section.</summary>
public sealed class JsonDocumentReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Json;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".json", ".jsonl" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, "application/json", ct);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var rawText = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        var sections = new List<DocumentSection>();

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                ct.ThrowIfCancellationRequested();
                sections.Add(new DocumentSection(
                    prop.Name,
                    JsonSerializer.Serialize(prop.Value, new JsonSerializerOptions { WriteIndented = true }),
                    Level: 1));
            }
        }
        else
        {
            sections.Add(new DocumentSection(null, rawText, 0));
        }

        return new DocumentContent("stream://json", DocumentReaderType.Json, rawText, sections);
    }
}

// ─── R-07 — XML ───────────────────────────────────────────────────────────────

/// <summary>R-07 — XML reader using System.Xml.Linq.
/// Top-level child elements become sections; namespace-aware.</summary>
public sealed class XmlDocumentReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Xml;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".xml", ".xsd", ".xsl", ".svg" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, "application/xml", ct);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var xdoc = await XDocument.LoadAsync(stream, System.Xml.Linq.LoadOptions.None, ct);
        var rawText = xdoc.ToString();
        var sections = new List<DocumentSection>();

        if (xdoc.Root is not null)
        {
            foreach (var child in xdoc.Root.Elements())
            {
                ct.ThrowIfCancellationRequested();
                var localName = child.Name.LocalName;
                sections.Add(new DocumentSection(localName, child.ToString(), 1,
                    new Dictionary<string, object> { ["ns"] = child.Name.NamespaceName }));
            }
        }

        if (sections.Count == 0)
            sections.Add(new DocumentSection(xdoc.Root?.Name.LocalName, rawText, 0));

        return new DocumentContent("stream://xml", DocumentReaderType.Xml, rawText, sections);
    }
}

// ─── R-08 — Markdown ──────────────────────────────────────────────────────────

/// <summary>R-08 — Markdown reader using Markdig.
/// Extracts headings and splits content into sections by heading level.</summary>
public sealed class MarkdownReader : IDocumentReader
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public DocumentReaderType ReaderType => DocumentReaderType.Markdown;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".md", ".markdown", ".mdx" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(uri, Encoding.UTF8, ct);
        return ParseMarkdown("file://" + uri, text);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(ct);
        return ParseMarkdown("stream://md", text);
    }

    private static DocumentContent ParseMarkdown(string uri, string text)
    {
        // Parse via Markdig to get rendered HTML, then extract headings from raw text
        var html = Markdown.ToHtml(text, Pipeline);
        var sections = new List<DocumentSection>();
        var lines = text.Split('\n');
        var currentHeading = (string?)null;
        var currentLevel = 0;
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                if (sb.Length > 0)
                    sections.Add(new DocumentSection(currentHeading, sb.ToString().Trim(), currentLevel));
                var level = trimmed.TakeWhile(c => c == '#').Count();
                currentHeading = trimmed.TrimStart('#').Trim();
                currentLevel = level;
                sb.Clear();
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        if (sb.Length > 0)
            sections.Add(new DocumentSection(currentHeading, sb.ToString().Trim(), currentLevel));

        if (sections.Count == 0)
            sections.Add(new DocumentSection(null, text.Trim(), 0));

        return new DocumentContent(uri, DocumentReaderType.Markdown, text, sections,
            new Dictionary<string, object> { ["renderedHtmlLength"] = html.Length });
    }
}

// ─── R-09 — HTML ──────────────────────────────────────────────────────────────

/// <summary>R-09 — HTML reader using HtmlAgilityPack.
/// Strips tags, extracts headings, title, and main content sections.</summary>
public sealed class HtmlDocumentReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Html;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".html", ".htm", ".xhtml" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, "text/html", ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.Load(stream, Encoding.UTF8);

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "";
        var sections = new List<DocumentSection>();
        var fullSb = new StringBuilder();

        // Extract heading-based sections
        var headingNodes = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4|//h5|//h6");
        if (headingNodes is not null)
        {
            foreach (var heading in headingNodes)
            {
                ct.ThrowIfCancellationRequested();
                var level = int.Parse(heading.Name.Substring(1), CultureInfo.InvariantCulture);
                var hText = HtmlAgilityPack.HtmlEntity.DeEntitize(heading.InnerText).Trim();
                // Collect following sibling text until next heading
                var contentSb = new StringBuilder();
                var next = heading.NextSibling;
                while (next is not null && !next.Name.StartsWith('h'))
                {
                    if (next.NodeType == HtmlAgilityPack.HtmlNodeType.Text || next.Name is not ("script" or "style"))
                        contentSb.Append(HtmlAgilityPack.HtmlEntity.DeEntitize(next.InnerText));
                    next = next.NextSibling;
                }
                var content = contentSb.ToString().Trim();
                sections.Add(new DocumentSection(hText, content, level));
                fullSb.AppendLine(hText).AppendLine(content);
            }
        }

        if (sections.Count == 0)
        {
            var body = doc.DocumentNode.SelectSingleNode("//body");
            var bodyText = body is null ? "" : HtmlAgilityPack.HtmlEntity.DeEntitize(body.InnerText).Trim();
            sections.Add(new DocumentSection(title, bodyText, 0));
            fullSb.Append(bodyText);
        }

        var meta = new Dictionary<string, object> { ["title"] = title };
        return Task.FromResult(new DocumentContent("stream://html", DocumentReaderType.Html, fullSb.ToString(), sections, meta));
    }
}

// ─── R-10 — Plain Text ────────────────────────────────────────────────────────

/// <summary>R-10 — Plain text reader with UTF-8 BOM auto-detection.
/// Splits content into sections by double newlines (paragraph breaks).</summary>
public sealed class PlainTextReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.PlainText;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".txt", ".text", ".log" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(uri, Encoding.UTF8, ct);
        return Build("file://" + uri, text);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return Build("stream://txt", text);
    }

    private static DocumentContent Build(string uri, string text)
    {
        // Split by double newline into paragraphs → sections
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var sections = paragraphs
            .Select((p, i) => new DocumentSection($"Paragraph {i + 1}", p.Trim(), 0))
            .Where(s => s.Content.Length > 0)
            .ToList<DocumentSection>();

        if (sections.Count == 0)
            sections.Add(new DocumentSection(null, text.Trim(), 0));

        return new DocumentContent(uri, DocumentReaderType.PlainText, text, sections);
    }
}

// ─── R-11 — YAML ──────────────────────────────────────────────────────────────

/// <summary>R-11 — YAML reader using YamlDotNet.
/// Top-level keys become sections; nested values serialized as YAML text.</summary>
public sealed class YamlDocumentReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Yaml;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".yaml", ".yml" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(uri, Encoding.UTF8, ct);
        return Parse("file://" + uri, text);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(ct);
        return Parse("stream://yaml", text);
    }

    private static DocumentContent Parse(string uri, string text)
    {
        var yamlStream = new YamlStream();
        yamlStream.Load(new StringReader(text));
        var sections = new List<DocumentSection>();

        foreach (var doc in yamlStream.Documents)
        {
            if (doc.RootNode is YamlMappingNode mapping)
            {
                foreach (var pair in mapping.Children)
                {
                    var key = pair.Key.ToString();
                    var value = pair.Value.ToString();
                    sections.Add(new DocumentSection(key, value, 1));
                }
            }
            else
            {
                sections.Add(new DocumentSection(null, doc.RootNode.ToString(), 0));
            }
        }

        if (sections.Count == 0)
            sections.Add(new DocumentSection(null, text.Trim(), 0));

        return new DocumentContent(uri, DocumentReaderType.Yaml, text, sections);
    }
}

// ─── R-12 — RTF ───────────────────────────────────────────────────────────────

/// <summary>R-12 — RTF reader using RtfPipe.
/// Strips RTF formatting and returns plain text as a single section.</summary>
public sealed class RtfDocumentReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Rtf;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".rtf" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, "application/rtf", ct);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        // RtfPipe requires Windows-1252 encoding; register code page provider for .NET Core
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var rtfText = await reader.ReadToEndAsync(ct);

        // RtfPipe converts RTF to HTML; extract inner text from HTML
        var html = Rtf.ToHtml(rtfText);
        var htmlDoc = new HtmlAgilityPack.HtmlDocument();
        htmlDoc.LoadHtml(html);
        var plainText = HtmlAgilityPack.HtmlEntity.DeEntitize(
            htmlDoc.DocumentNode.InnerText).Trim();

        var sections = new List<DocumentSection>
        {
            new DocumentSection(null, plainText, 0),
        };

        return new DocumentContent("stream://rtf", DocumentReaderType.Rtf, plainText, sections);
    }
}

// ─── R-26 — EPUB ──────────────────────────────────────────────────────────────

/// <summary>R-26 — EPUB reader using VersOne.Epub.
/// Extracts chapters as sections; strips HTML tags from chapter content.
/// TOC navigation points used as section headings when available.</summary>
public sealed class EpubDocumentReader : IDocumentReader
{
    public DocumentReaderType ReaderType => DocumentReaderType.Epub;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".epub" };

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var book = await EpubReader.ReadBookAsync(uri);
        return ExtractContent("file://" + uri, book);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var book = await EpubReader.ReadBookAsync(stream);
        return ExtractContent("stream://epub", book);
    }

    private static DocumentContent ExtractContent(string uri, EpubBook book)
    {
        var sections = new List<DocumentSection>();
        var fullSb = new StringBuilder();

        foreach (var chapter in book.ReadingOrder)
        {
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            htmlDoc.LoadHtml(chapter.Content);
            var text = HtmlAgilityPack.HtmlEntity.DeEntitize(
                htmlDoc.DocumentNode.InnerText).Trim();
            if (text.Length == 0) continue;

            sections.Add(new DocumentSection(chapter.FilePath, text, 1));
            fullSb.AppendLine(text);
        }

        var meta = new Dictionary<string, object>
        {
            ["title"]   = book.Title ?? "",
            ["author"]  = book.Author ?? "",
            ["chapterCount"] = sections.Count,
        };

        return new DocumentContent(uri, DocumentReaderType.Epub, fullSb.ToString(), sections, meta);
    }
}
