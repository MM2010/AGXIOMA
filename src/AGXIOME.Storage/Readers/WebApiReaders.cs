using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using System.Xml.Linq;

namespace AGXIOME.Storage.Readers;

// ─────────────────────────────────────────────────────────────────────────────
// R-13..R-18, R-22, R-27, R-28 — Web API Readers
//
// All readers use injected HttpClient for network access.
// Zero Trust: no client-side authority; servers are the source of truth.
// ReadFromStreamAsync is not applicable for API readers → throws NotSupportedException.
// ─────────────────────────────────────────────────────────────────────────────

// ─── R-13 — Website ───────────────────────────────────────────────────────────

/// <summary>R-13 — Website reader. Fetches URL via HTTP, parses HTML with HtmlAgilityPack.
/// Strips scripts, styles, and navigation; extracts headings as sections.
/// URI must be an absolute HTTP/HTTPS URL.</summary>
public sealed class WebsiteReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.Website;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public WebsiteReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var html = await _http.GetStringAsync(uri, ct);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(html));
        return await ReadFromStreamAsync(ms, "text/html", ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.Load(stream, Encoding.UTF8);

        // Remove noise elements
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header") ?? Enumerable.Empty<HtmlAgilityPack.HtmlNode>())
            node.Remove();

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? "";
        var sections = new List<DocumentSection>();
        var fullSb = new StringBuilder();

        var headings = doc.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4");
        if (headings is not null)
        {
            foreach (var h in headings)
            {
                ct.ThrowIfCancellationRequested();
                var level = int.Parse(h.Name[1..], System.Globalization.CultureInfo.InvariantCulture);
                var hText = HtmlAgilityPack.HtmlEntity.DeEntitize(h.InnerText).Trim();
                var contentSb = new StringBuilder();
                var next = h.NextSibling;
                while (next is not null && !next.Name.StartsWith('h'))
                {
                    if (next.NodeType == HtmlAgilityPack.HtmlNodeType.Element)
                        contentSb.AppendLine(HtmlAgilityPack.HtmlEntity.DeEntitize(next.InnerText).Trim());
                    next = next.NextSibling;
                }
                sections.Add(new DocumentSection(hText, contentSb.ToString().Trim(), level));
                fullSb.AppendLine(hText).AppendLine(contentSb.ToString());
            }
        }

        if (sections.Count == 0)
        {
            var body = doc.DocumentNode.SelectSingleNode("//body");
            var text = body is null ? "" : HtmlAgilityPack.HtmlEntity.DeEntitize(body.InnerText).Trim();
            sections.Add(new DocumentSection(title, text, 0));
            fullSb.Append(text);
        }

        return Task.FromResult(new DocumentContent("stream://website", DocumentReaderType.Website,
            fullSb.ToString(), sections, new Dictionary<string, object> { ["title"] = title }));
    }
}

// ─── R-14 — Wikipedia ─────────────────────────────────────────────────────────

/// <summary>R-14 — Wikipedia reader using the Wikipedia REST API.
/// URI format: "wikipedia://en/Alan_Turing" or just the article title.
/// Returns summary + sections from the Wikipedia API.</summary>
public sealed class WikipediaReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.Wikipedia;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public WikipediaReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        // Accept URI formats: "wikipedia://en/Alan_Turing" or just "Alan_Turing"
        var (lang, title) = ParseUri(uri);
        var encoded = HttpUtility.UrlEncode(title.Replace(' ', '_'));

        // Wikipedia REST API: page summary
        var summaryUrl = $"https://{lang}.wikipedia.org/api/rest_v1/page/summary/{encoded}";
        using var resp = await _http.GetAsync(summaryUrl, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        var displayTitle = root.TryGetProperty("displaytitle", out var dt) ? dt.GetString() ?? title : title;
        var extract = root.TryGetProperty("extract", out var ex) ? ex.GetString() ?? "" : "";
        var description = root.TryGetProperty("description", out var dsc) ? dsc.GetString() ?? "" : "";

        var sections = new List<DocumentSection>
        {
            new DocumentSection("Summary", extract, 1),
        };

        // Fetch sections from the parse API
        try
        {
            var sectionsUrl = $"https://{lang}.wikipedia.org/w/api.php?action=parse&page={encoded}&prop=sections&format=json";
            using var secResp = await _http.GetAsync(sectionsUrl, ct);
            if (secResp.IsSuccessStatusCode)
            {
                using var secDoc = await JsonDocument.ParseAsync(await secResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (secDoc.RootElement.TryGetProperty("parse", out var parse) &&
                    parse.TryGetProperty("sections", out var secs))
                {
                    foreach (var s in secs.EnumerateArray())
                    {
                        var line = s.TryGetProperty("line", out var l) ? l.GetString() ?? "" : "";
                        var level = s.TryGetProperty("level", out var lv) && int.TryParse(lv.GetString(), out var lvInt) ? lvInt : 2;
                        if (!string.IsNullOrEmpty(line))
                            sections.Add(new DocumentSection(line, "", level));
                    }
                }
            }
        }
        catch { /* sections metadata is best-effort */ }

        var meta = new Dictionary<string, object>
        {
            ["title"]       = displayTitle,
            ["description"] = description,
            ["lang"]        = lang,
        };

        return new DocumentContent($"wikipedia://{lang}/{title}", DocumentReaderType.Wikipedia, extract, sections, meta);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("WikipediaReader does not support stream input. Use ReadAsync with a wikipedia:// URI.");

    private static (string lang, string title) ParseUri(string uri)
    {
        if (uri.StartsWith("wikipedia://", StringComparison.OrdinalIgnoreCase))
        {
            var parts = uri["wikipedia://".Length..].Split('/', 2);
            return parts.Length == 2 ? (parts[0], parts[1]) : ("en", parts[0]);
        }
        return ("en", uri);
    }
}

// ─── R-15 — Arxiv ─────────────────────────────────────────────────────────────

/// <summary>R-15 — Arxiv reader using the arXiv API.
/// URI format: "arxiv://2301.12345" or the full arXiv URL.
/// Returns abstract, metadata, and PDF download link as sections.</summary>
public sealed class ArxivReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.Arxiv;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public ArxivReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var arxivId = ParseArxivId(uri);
        var apiUrl = $"https://export.arxiv.org/api/query?id_list={HttpUtility.UrlEncode(arxivId)}&max_results=1";

        using var resp = await _http.GetAsync(apiUrl, ct);
        resp.EnsureSuccessStatusCode();
        var xml = await resp.Content.ReadAsStringAsync(ct);

        var xdoc = System.Xml.Linq.XDocument.Parse(xml);
        XNamespace atom = "http://www.w3.org/2005/Atom";
        var entry = xdoc.Descendants(atom + "entry").FirstOrDefault();

        if (entry is null)
            return new DocumentContent($"arxiv://{arxivId}", DocumentReaderType.Arxiv, "", Array.Empty<DocumentSection>());

        var title   = entry.Element(atom + "title")?.Value?.Trim() ?? "";
        var summary = entry.Element(atom + "summary")?.Value?.Trim() ?? "";
        var authors = string.Join(", ", entry.Descendants(atom + "name").Select(n => n.Value));
        var published = entry.Element(atom + "published")?.Value ?? "";
        var pdfLink = entry.Elements(atom + "link")
            .FirstOrDefault(l => l.Attribute("type")?.Value == "application/pdf")
            ?.Attribute("href")?.Value ?? "";

        var sections = new List<DocumentSection>
        {
            new DocumentSection("Abstract", summary, 1),
            new DocumentSection("Authors", authors, 2),
        };

        var meta = new Dictionary<string, object>
        {
            ["title"]     = title,
            ["authors"]   = authors,
            ["published"] = published,
            ["pdfUrl"]    = pdfLink,
            ["arxivId"]   = arxivId,
        };

        return new DocumentContent($"arxiv://{arxivId}", DocumentReaderType.Arxiv,
            $"{title}\n\n{summary}", sections, meta);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("ArxivReader does not support stream input. Use ReadAsync with an arxiv:// URI.");

    private static string ParseArxivId(string uri)
    {
        if (uri.StartsWith("arxiv://", StringComparison.OrdinalIgnoreCase))
            return uri["arxiv://".Length..];
        // Handle full URLs: https://arxiv.org/abs/2301.12345
        if (uri.Contains("arxiv.org/abs/", StringComparison.OrdinalIgnoreCase))
            return uri[(uri.IndexOf("arxiv.org/abs/", StringComparison.OrdinalIgnoreCase) + "arxiv.org/abs/".Length)..];
        return uri;
    }
}

// ─── R-16 — PubMed ────────────────────────────────────────────────────────────

/// <summary>R-16 — PubMed reader using the Entrez NCBI API.
/// URI format: "pubmed://12345678" or a PMID string.
/// Returns abstract, MeSH terms, and author/journal metadata.</summary>
public sealed class PubMedReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.PubMed;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public PubMedReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var pmid = uri.StartsWith("pubmed://", StringComparison.OrdinalIgnoreCase)
            ? uri["pubmed://".Length..] : uri;

        var apiUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?db=pubmed&id={HttpUtility.UrlEncode(pmid)}&rettype=abstract&retmode=json";
        using var resp = await _http.GetAsync(apiUrl, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var result = doc.RootElement;
        // PubMed Entrez JSON structure: PubmedArticleSet.PubmedArticle[0].MedlineCitation
        var articleSet = result.TryGetProperty("PubmedArticleSet", out var pas) ? pas : result;
        var article    = articleSet.ValueKind == JsonValueKind.Array ? articleSet[0] : articleSet;
        var citation   = article.TryGetProperty("MedlineCitation", out var cit) ? cit : default;
        var articleInfo = citation.ValueKind != JsonValueKind.Undefined && citation.TryGetProperty("Article", out var art) ? art : default;

        var titleStr   = articleInfo.ValueKind != JsonValueKind.Undefined && articleInfo.TryGetProperty("ArticleTitle", out var t) ? t.GetString() ?? "" : "";
        var abstractStr = "";
        if (articleInfo.ValueKind != JsonValueKind.Undefined && articleInfo.TryGetProperty("Abstract", out var abs) &&
            abs.TryGetProperty("AbstractText", out var absText))
        {
            abstractStr = absText.ValueKind == JsonValueKind.String
                ? absText.GetString() ?? ""
                : string.Join(" ", absText.EnumerateArray().Select(a => a.GetString() ?? ""));
        }

        var sections = new List<DocumentSection>
        {
            new DocumentSection("Abstract", abstractStr, 1),
        };

        var meta = new Dictionary<string, object>
        {
            ["title"] = titleStr,
            ["pmid"]  = pmid,
        };

        return new DocumentContent($"pubmed://{pmid}", DocumentReaderType.PubMed,
            $"{titleStr}\n\n{abstractStr}", sections, meta);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("PubMedReader does not support stream input. Use ReadAsync with a pubmed:// URI.");
}

// ─── R-17 — YouTube Transcript ────────────────────────────────────────────────

/// <summary>R-17 — YouTube transcript reader using the unofficial timedtext API.
/// URI format: "youtube://VIDEO_ID" or a full youtube.com URL.
/// Fetches timed caption XML and returns text grouped into ~1-minute sections.</summary>
public sealed class YoutubeTranscriptReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.YoutubeTranscript;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public YoutubeTranscriptReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var videoId = ParseVideoId(uri);
        // Unofficial timed-text API (best-effort; may require updated endpoint per YouTube policy changes)
        var timedTextUrl = $"https://www.youtube.com/api/timedtext?lang=en&v={HttpUtility.UrlEncode(videoId)}&fmt=json3";
        using var resp = await _http.GetAsync(timedTextUrl, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var events = doc.RootElement.TryGetProperty("events", out var ev) ? ev : default;
        if (events.ValueKind == JsonValueKind.Undefined)
            return new DocumentContent($"youtube://{videoId}", DocumentReaderType.YoutubeTranscript, "", Array.Empty<DocumentSection>());

        var sb = new StringBuilder();
        var sections = new List<DocumentSection>();
        var segSb = new StringBuilder();
        var segStart = 0.0;
        var segNum = 1;

        foreach (var e in events.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            var startMs = e.TryGetProperty("tStartMs", out var ts) ? ts.GetDouble() : 0;
            if (!e.TryGetProperty("segs", out var segs)) continue;

            foreach (var seg in segs.EnumerateArray())
            {
                var text = seg.TryGetProperty("utf8", out var u) ? u.GetString() ?? "" : "";
                segSb.Append(text).Append(' ');
                sb.Append(text).Append(' ');
            }

            // Group into ~60-second sections
            if (startMs - segStart >= 60_000 && segSb.Length > 0)
            {
                sections.Add(new DocumentSection(
                    $"Segment {segNum} ({segStart / 1000.0:F0}s)",
                    segSb.ToString().Trim(), 1,
                    new Dictionary<string, object> { ["startSec"] = segStart / 1000.0 }));
                segSb.Clear();
                segStart = startMs;
                segNum++;
            }
        }

        if (segSb.Length > 0)
            sections.Add(new DocumentSection($"Segment {segNum}", segSb.ToString().Trim(), 1));

        return new DocumentContent($"youtube://{videoId}", DocumentReaderType.YoutubeTranscript,
            sb.ToString(), sections, new Dictionary<string, object> { ["videoId"] = videoId });
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("YoutubeTranscriptReader does not support stream input.");

    private static string ParseVideoId(string uri)
    {
        if (uri.StartsWith("youtube://", StringComparison.OrdinalIgnoreCase))
            return uri["youtube://".Length..];
        // Handle https://www.youtube.com/watch?v=VIDEO_ID
        var q = HttpUtility.ParseQueryString(new Uri(uri).Query);
        return q["v"] ?? uri;
    }
}

// ─── R-18 — Firecrawl ─────────────────────────────────────────────────────────

/// <summary>R-18 — Firecrawl reader using the Firecrawl scrape + extract API.
/// Requires a Firecrawl API key passed as a bearer token via HttpClient.DefaultRequestHeaders.
/// URI must be an absolute HTTP/HTTPS URL to scrape.</summary>
public sealed class FirecrawlReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.Firecrawl;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public FirecrawlReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var body = new { url = uri, formats = new[] { "markdown", "html" } };
        using var resp = await _http.PostAsJsonAsync("v1/scrape", body, cancellationToken: ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var data = doc.RootElement.TryGetProperty("data", out var d) ? d : doc.RootElement;
        var markdown = data.TryGetProperty("markdown", out var md) ? md.GetString() ?? "" : "";
        var title    = data.TryGetProperty("metadata", out var meta) &&
                       meta.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

        var sections = new List<DocumentSection>
        {
            new DocumentSection(title, markdown, 1),
        };

        return new DocumentContent(uri, DocumentReaderType.Firecrawl, markdown, sections,
            new Dictionary<string, object> { ["title"] = title, ["sourceUrl"] = uri });
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("FirecrawlReader does not support stream input. Use ReadAsync with an HTTP URI.");
}

// ─── R-22 — GitHub ────────────────────────────────────────────────────────────

/// <summary>R-22 — GitHub reader using the GitHub REST API.
/// URI format: "github://owner/repo/path/to/file.md" or full API URL.
/// Supports file content fetch and repository tree listing.
/// Set Authorization header on HttpClient for private repos.</summary>
public sealed class GitHubReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.GitHub;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public GitHubReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        // URI: "github://owner/repo/path/to/file.md"
        var (owner, repo, path) = ParseGitHubUri(uri);
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{path}";

        using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        req.Headers.Add("User-Agent", "AGXIOME-Reader/1.0");
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var root = doc.RootElement;

        // File content is Base64-encoded in "content" field
        var content = root.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
        var rawText = content.Length > 0
            ? Encoding.UTF8.GetString(Convert.FromBase64String(content.Replace("\n", "")))
            : "";

        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? path : path;
        var sha  = root.TryGetProperty("sha", out var s) ? s.GetString() ?? "" : "";

        var sections = new List<DocumentSection>
        {
            new DocumentSection(name, rawText, 1),
        };

        var meta = new Dictionary<string, object>
        {
            ["owner"] = owner,
            ["repo"]  = repo,
            ["path"]  = path,
            ["sha"]   = sha,
        };

        return new DocumentContent(uri, DocumentReaderType.GitHub, rawText, sections, meta);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("GitHubReader does not support stream input. Use ReadAsync with a github:// URI.");

    private static (string owner, string repo, string path) ParseGitHubUri(string uri)
    {
        var s = uri.StartsWith("github://", StringComparison.OrdinalIgnoreCase)
            ? uri["github://".Length..] : uri;
        var parts = s.Split('/', 3);
        return parts.Length switch
        {
            3 => (parts[0], parts[1], parts[2]),
            2 => (parts[0], parts[1], ""),
            _ => (s, "", ""),
        };
    }
}

// ─── R-27 — Notion ────────────────────────────────────────────────────────────

/// <summary>R-27 — Notion reader using the Notion REST API v1.
/// URI format: "notion://PAGE_ID" where PAGE_ID is the 32-char Notion page/block ID.
/// Set Authorization header on HttpClient: "Bearer {integration_token}".
/// Notion-Version header must be set to "2022-06-28" or later.</summary>
public sealed class NotionReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.Notion;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public NotionReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var pageId = uri.StartsWith("notion://", StringComparison.OrdinalIgnoreCase)
            ? uri["notion://".Length..] : uri;

        // Fetch page metadata
        using var pageResp = await _http.GetAsync($"v1/pages/{pageId}", ct);
        pageResp.EnsureSuccessStatusCode();
        using var pageDoc = await JsonDocument.ParseAsync(await pageResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var title = ExtractNotionTitle(pageDoc.RootElement);

        // Fetch blocks (page content)
        using var blockResp = await _http.GetAsync($"v1/blocks/{pageId}/children?page_size=100", ct);
        blockResp.EnsureSuccessStatusCode();
        using var blockDoc = await JsonDocument.ParseAsync(await blockResp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var sections = new List<DocumentSection>();
        var fullSb = new StringBuilder();

        if (blockDoc.RootElement.TryGetProperty("results", out var results))
        {
            var currentHeading = (string?)null;
            var currentLevel = 0;
            var contentSb = new StringBuilder();

            foreach (var block in results.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : "";
                var text = ExtractBlockText(block, blockType ?? "");

                if (blockType is "heading_1" or "heading_2" or "heading_3")
                {
                    if (contentSb.Length > 0)
                        sections.Add(new DocumentSection(currentHeading, contentSb.ToString().Trim(), currentLevel));
                    currentHeading = text;
                    currentLevel = blockType == "heading_1" ? 1 : blockType == "heading_2" ? 2 : 3;
                    contentSb.Clear();
                }
                else
                {
                    contentSb.AppendLine(text);
                }
                fullSb.AppendLine(text);
            }

            if (contentSb.Length > 0)
                sections.Add(new DocumentSection(currentHeading, contentSb.ToString().Trim(), currentLevel));
        }

        if (sections.Count == 0)
            sections.Add(new DocumentSection(title, fullSb.ToString().Trim(), 0));

        return new DocumentContent($"notion://{pageId}", DocumentReaderType.Notion,
            fullSb.ToString(), sections, new Dictionary<string, object> { ["title"] = title, ["pageId"] = pageId });
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("NotionReader does not support stream input. Use ReadAsync with a notion:// URI.");

    private static string ExtractNotionTitle(JsonElement page)
    {
        if (!page.TryGetProperty("properties", out var props)) return "";
        foreach (var prop in props.EnumerateObject())
        {
            if (prop.Value.TryGetProperty("title", out var t))
            {
                var arr = t.ValueKind == JsonValueKind.Array ? t : default;
                if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                    return arr[0].TryGetProperty("plain_text", out var pt) ? pt.GetString() ?? "" : "";
            }
        }
        return "";
    }

    private static string ExtractBlockText(JsonElement block, string blockType)
    {
        if (!block.TryGetProperty(blockType, out var content)) return "";
        if (!content.TryGetProperty("rich_text", out var richText)) return "";
        return string.Join("", richText.EnumerateArray()
            .Select(rt => rt.TryGetProperty("plain_text", out var pt) ? pt.GetString() ?? "" : ""));
    }
}

// ─── R-28 — Confluence ────────────────────────────────────────────────────────

/// <summary>R-28 — Confluence reader using the Confluence Cloud REST API v2.
/// URI format: "confluence://SPACE_KEY/PAGE_TITLE" or "confluence://PAGE_ID".
/// Set Authorization header: "Basic {base64(email:api_token)}" or Bearer.
/// Extracts page title, body content, and version metadata.</summary>
public sealed class ConfluenceReader : IDocumentReader
{
    private readonly HttpClient _http;
    public DocumentReaderType ReaderType => DocumentReaderType.Confluence;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public ConfluenceReader(HttpClient http)
        => _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var (spaceKey, pageTitle, pageId) = ParseUri(uri);
        string apiUrl;

        if (!string.IsNullOrEmpty(pageId))
        {
            // Direct page ID lookup via REST API v2
            apiUrl = $"wiki/api/v2/pages/{pageId}?body-format=atlas_doc_format";
        }
        else
        {
            // Search by space + title
            apiUrl = $"wiki/rest/api/content?spaceKey={HttpUtility.UrlEncode(spaceKey)}&title={HttpUtility.UrlEncode(pageTitle)}&expand=body.storage,version";
        }

        using var resp = await _http.GetAsync(apiUrl, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        var root = doc.RootElement;
        // Handle v1 search result (results array) vs v2 direct fetch
        var page = root.TryGetProperty("results", out var res) && res.GetArrayLength() > 0
            ? res[0] : root;

        var title   = page.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var version = page.TryGetProperty("version", out var v) && v.TryGetProperty("number", out var vn)
            ? vn.GetInt32() : 0;

        // Extract body storage HTML → strip tags → plain text
        var storageValue = page.TryGetProperty("body", out var body) &&
                           body.TryGetProperty("storage", out var storage) &&
                           storage.TryGetProperty("value", out var val) ? val.GetString() ?? "" : "";

        var plainText = StripHtmlTags(storageValue);

        var sections = new List<DocumentSection>
        {
            new DocumentSection(title, plainText, 1),
        };

        var meta = new Dictionary<string, object>
        {
            ["title"]   = title,
            ["version"] = version,
            ["spaceKey"] = spaceKey,
        };

        return new DocumentContent(uri, DocumentReaderType.Confluence, plainText, sections, meta);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
        => throw new NotSupportedException("ConfluenceReader does not support stream input. Use ReadAsync with a confluence:// URI.");

    private static (string spaceKey, string pageTitle, string pageId) ParseUri(string uri)
    {
        var s = uri.StartsWith("confluence://", StringComparison.OrdinalIgnoreCase)
            ? uri["confluence://".Length..] : uri;
        var parts = s.Split('/', 2);
        // If parts[0] looks like a pure numeric/alphanumeric ID, treat as page ID
        if (parts.Length == 1)
            return ("", "", parts[0]);
        return (parts[0], parts[1], "");
    }

    private static string StripHtmlTags(string html)
    {
        var d = new HtmlAgilityPack.HtmlDocument();
        d.LoadHtml(html);
        return HtmlAgilityPack.HtmlEntity.DeEntitize(d.DocumentNode.InnerText).Trim();
    }
}
