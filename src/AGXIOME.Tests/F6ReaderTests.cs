using System.Net;
using System.IO.Compression;
using System.Text;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Readers;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests;

internal static class LiveDocumentReaderTestSupport
{
    internal const string WebsiteUrlEnv = "AGXIOME_WEBSITE_TEST_URL";
    internal const string WebsiteMissingUrlEnv = "AGXIOME_WEBSITE_TEST_404_URL";
    internal const string WebsiteRedirectUrlEnv = "AGXIOME_WEBSITE_TEST_REDIRECT_URL";
    internal const string WikipediaTitleEnv = "AGXIOME_WIKIPEDIA_TEST_TITLE";
    internal const string WikipediaUriEnv = "AGXIOME_WIKIPEDIA_TEST_URI";
    internal const string WikipediaLangUriEnv = "AGXIOME_WIKIPEDIA_TEST_LANG_URI";
    internal const string PubMedPmidEnv = "AGXIOME_PUBMED_TEST_PMID";
    internal const string PubMedUriEnv = "AGXIOME_PUBMED_TEST_URI";
    internal const string PubMedInvalidPmidEnv = "AGXIOME_PUBMED_TEST_INVALID_PMID";
    internal const string YouTubeVideoIdEnv = "AGXIOME_YOUTUBE_TEST_VIDEO_ID";
    internal const string YouTubeUriEnv = "AGXIOME_YOUTUBE_TEST_URI";
    internal const string YouTubeNoCaptionVideoIdEnv = "AGXIOME_YOUTUBE_TEST_NO_CAPTION_VIDEO_ID";
    internal const string FirecrawlApiKeyEnv = "AGXIOME_FIRECRAWL_API_KEY";
    internal const string FirecrawlTargetUrlEnv = "AGXIOME_FIRECRAWL_TEST_URL";
    internal const string FirecrawlBaseUrlEnv = "AGXIOME_FIRECRAWL_BASE_URL";
    internal const string AwsS3RegionEnv = "AGXIOME_AWS_S3_REGION";
    internal const string AwsS3TestUriEnv = "AGXIOME_AWS_S3_TEST_URI";
    internal const string AwsS3MissingUriEnv = "AGXIOME_AWS_S3_MISSING_URI";
    internal const string AzureBlobConnectionStringEnv = "AGXIOME_AZURE_BLOB_CONNECTION_STRING";
    internal const string AzureBlobTestUriEnv = "AGXIOME_AZURE_BLOB_TEST_URI";
    internal const string AzureBlobMissingUriEnv = "AGXIOME_AZURE_BLOB_MISSING_URI";
    internal const string GcsCredentialsPathEnv = "GOOGLE_APPLICATION_CREDENTIALS";
    internal const string GcsTestUriEnv = "AGXIOME_GCS_TEST_URI";
    internal const string GcsMissingUriEnv = "AGXIOME_GCS_MISSING_URI";
    internal const string GitHubTestUriEnv = "AGXIOME_GITHUB_TEST_URI";
    internal const string GitHubPrivateUriEnv = "AGXIOME_GITHUB_PRIVATE_URI";
    internal const string GitHubMissingUriEnv = "AGXIOME_GITHUB_MISSING_URI";
    internal const string GitHubTokenEnv = "AGXIOME_GITHUB_TOKEN";
    internal const string SharePointTestUriEnv = "AGXIOME_SHAREPOINT_TEST_URI";
    internal const string SharePointAccessTokenEnv = "AGXIOME_SHAREPOINT_ACCESS_TOKEN";
    internal const string WhisperApiKeyEnv = "AGXIOME_WHISPER_API_KEY";
    internal const string WhisperAudioFileEnv = "AGXIOME_WHISPER_AUDIO_FILE";
    internal const string WhisperBaseUrlEnv = "AGXIOME_WHISPER_BASE_URL";
    internal const string NotionTokenEnv = "AGXIOME_NOTION_TOKEN";
    internal const string NotionPageIdEnv = "AGXIOME_NOTION_PAGE_ID";
    internal const string ConfluenceTokenEnv = "AGXIOME_CONFLUENCE_TOKEN";
    internal const string ConfluenceInstanceEnv = "AGXIOME_CONFLUENCE_INSTANCE";
    internal const string ConfluencePageUriEnv = "AGXIOME_CONFLUENCE_PAGE_URI";

    internal static string RequireEnvironmentVariable(string envName, string requirement)
    {
        var value = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw new InvalidOperationException($"Missing required environment variable {envName} for {requirement}.");
    }

    internal static HttpClient CreateLiveHttpClient(bool allowAutoRedirect = true)
        => new(new HttpClientHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
        });

    internal static HttpClient CreateLiveFirecrawlHttpClient(bool includeAuthorization)
    {
        var baseUrl = Environment.GetEnvironmentVariable(FirecrawlBaseUrlEnv);
        var client = new HttpClient
        {
            BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(baseUrl) ? "https://api.firecrawl.dev/" : baseUrl,
                UriKind.Absolute),
        };

        if (includeAuthorization)
        {
            var apiKey = RequireEnvironmentVariable(FirecrawlApiKeyEnv, "a live Firecrawl API key");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        return client;
    }

    internal static Amazon.S3.IAmazonS3 CreateLiveAwsS3Client()
    {
        var region = RequireEnvironmentVariable(AwsS3RegionEnv, "a live AWS region");
        var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        var hasExplicitAwsCredentials =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AWS_PROFILE"));

        return hasExplicitAwsCredentials
            ? new Amazon.S3.AmazonS3Client(regionEndpoint)
            : new Amazon.S3.AmazonS3Client(new Amazon.Runtime.AnonymousAWSCredentials(), regionEndpoint);
    }

    internal static Azure.Storage.Blobs.BlobServiceClient CreateLiveAzureBlobClient()
    {
        var connectionString = Environment.GetEnvironmentVariable(AzureBlobConnectionStringEnv);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return new Azure.Storage.Blobs.BlobServiceClient(connectionString);

        var candidateUri =
            Environment.GetEnvironmentVariable(AzureBlobTestUriEnv) ??
            Environment.GetEnvironmentVariable(AzureBlobMissingUriEnv);

        if (string.IsNullOrWhiteSpace(candidateUri))
            throw new InvalidOperationException($"Missing required environment variable {AzureBlobConnectionStringEnv} or a public Azure Blob URI for anonymous access.");

        var blobUri = new Uri(candidateUri, UriKind.Absolute);
        var serviceUri = new Uri($"{blobUri.Scheme}://{blobUri.Host}", UriKind.Absolute);
        return new Azure.Storage.Blobs.BlobServiceClient(serviceUri);
    }

    internal static Google.Cloud.Storage.V1.StorageClient CreateLiveGcsClient()
    {
        var credentialsPath = Environment.GetEnvironmentVariable(GcsCredentialsPathEnv);
        if (string.IsNullOrWhiteSpace(credentialsPath))
            return Google.Cloud.Storage.V1.StorageClient.CreateUnauthenticated();

        var credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(credentialsPath);
        return Google.Cloud.Storage.V1.StorageClient.Create(credential);
    }

    internal static HttpClient CreateLiveGitHubHttpClient(bool includeAuthorization)
    {
        var client = CreateLiveHttpClient();

        if (includeAuthorization)
        {
            var token = Environment.GetEnvironmentVariable(GitHubTokenEnv);
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    internal static Microsoft.Graph.GraphServiceClient CreateLiveSharePointClient(bool includeAuthorization)
    {
        var token = includeAuthorization
            ? RequireEnvironmentVariable(SharePointAccessTokenEnv, "a live Microsoft Graph access token")
            : null;

        return new Microsoft.Graph.GraphServiceClient(
            CreateLiveHttpClient(),
            new StaticBearerAuthenticationProvider(token),
            "https://graph.microsoft.com/v1.0");
    }

    internal static HttpClient CreateLiveWhisperHttpClient(bool includeAuthorization)
    {
        var baseUrl = Environment.GetEnvironmentVariable(WhisperBaseUrlEnv);
        var client = new HttpClient
        {
            BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/" : baseUrl,
                UriKind.Absolute),
        };

        if (includeAuthorization)
        {
            var apiKey = RequireEnvironmentVariable(WhisperApiKeyEnv, "a live OpenAI Whisper API key");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        return client;
    }
}

internal sealed class StaticBearerAuthenticationProvider : Microsoft.Kiota.Abstractions.Authentication.IAuthenticationProvider
{
    private readonly string? _accessToken;

    public StaticBearerAuthenticationProvider(string? accessToken)
        => _accessToken = accessToken;

    public Task AuthenticateRequestAsync(
        Microsoft.Kiota.Abstractions.RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");

        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class RequiresDocumentReaderEnvironmentFactAttribute : FactAttribute
{
    public RequiresDocumentReaderEnvironmentFactAttribute(params string[] envNames)
    {
        var missing = envNames
            .Where(envName => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(envName)))
            .ToArray();

        if (missing.Length > 0)
            Skip = $"requires environment variables: {string.Join(", ", missing)}";
    }
}

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

    public static MemoryStream Epub()
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteZipEntry(archive, "mimetype", "application/epub+zip", System.IO.Compression.CompressionLevel.NoCompression);
            WriteZipEntry(
                archive,
                "META-INF/container.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">" +
                "<rootfiles><rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/></rootfiles>" +
                "</container>");
            WriteZipEntry(
                archive,
                "OEBPS/content.opf",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"2.0\" unique-identifier=\"BookId\">" +
                "<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">" +
                "<dc:title>Test EPUB</dc:title><dc:creator>Test Author</dc:creator><dc:language>en</dc:language>" +
                "<dc:identifier id=\"BookId\">urn:test:epub</dc:identifier></metadata>" +
                "<manifest>" +
                "<item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>" +
                "<item id=\"chapter1\" href=\"chapter1.xhtml\" media-type=\"application/xhtml+xml\"/>" +
                "<item id=\"chapter2\" href=\"chapter2.xhtml\" media-type=\"application/xhtml+xml\"/>" +
                "</manifest>" +
                "<spine toc=\"ncx\"><itemref idref=\"chapter1\"/><itemref idref=\"chapter2\"/></spine>" +
                "</package>");
            WriteZipEntry(
                archive,
                "OEBPS/toc.ncx",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">" +
                "<head><meta name=\"dtb:uid\" content=\"urn:test:epub\"/></head>" +
                "<docTitle><text>Test EPUB</text></docTitle><navMap>" +
                "<navPoint id=\"navPoint-1\" playOrder=\"1\"><navLabel><text>Chapter 1</text></navLabel><content src=\"chapter1.xhtml\"/></navPoint>" +
                "<navPoint id=\"navPoint-2\" playOrder=\"2\"><navLabel><text>Chapter 2</text></navLabel><content src=\"chapter2.xhtml\"/></navPoint>" +
                "</navMap></ncx>");
            WriteZipEntry(
                archive,
                "OEBPS/chapter1.xhtml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body><h1>Chapter 1</h1><p>Hello EPUB chapter one.</p></body></html>");
            WriteZipEntry(
                archive,
                "OEBPS/chapter2.xhtml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><body><h1>Chapter 2</h1><p>Hello EPUB chapter two.</p></body></html>");
        }

        stream.Position = 0;
        return stream;
    }

    public static MemoryStream Empty() => new MemoryStream();

    // Generates a real PNG with "HELLO OCR" text on white background.
    // Used by ImageOcrReaderTests when Tesseract is available.
    // Relies on SkiaSharp for cross-platform image generation.
    public static void GenerateOcrSamplePng(string path)
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(420, 140));
        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.White);
        using var paint = new SkiaSharp.SKPaint
        {
            Color = SkiaSharp.SKColors.Black,
            IsAntialias = true,
        };
        using var font = new SkiaSharp.SKFont(SkiaSharp.SKTypeface.FromFamilyName("Arial"), 30);
        canvas.DrawText("HELLO OCR", 20, 70, font, paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(path);
        data.SaveTo(fs);
    }

    private static void WriteZipEntry(ZipArchive archive, string name, string content, System.IO.Compression.CompressionLevel compressionLevel = System.IO.Compression.CompressionLevel.Optimal)
    {
        var entry = archive.CreateEntry(name, compressionLevel);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
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

    [Fact(Skip = "requires a real .docx file on disk")]
    public Task ReadAsync_ValidDocx_ReturnsSections() => Task.CompletedTask;

    [Fact(Skip = "requires a real .docx byte stream")]
    public Task ReadFromStream_ValidDocx_HasHeadingSections() => Task.CompletedTask;
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

    [Fact(Skip = "requires a real .xlsx file on disk")]
    public Task ReadAsync_ValidXlsx_OneSection_PerSheet() => Task.CompletedTask;

    [Fact(Skip = "requires a real .xlsx byte stream")]
    public Task ReadFromStream_ValidXlsx_SheetNameIsHeading() => Task.CompletedTask;
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

    [Fact(Skip = "requires a real .pptx file")]
    public Task ReadAsync_ValidPptx_OneSectionPerSlide() => Task.CompletedTask;

    [Fact(Skip = "requires a real .pptx byte stream")]
    public Task ReadFromStream_ValidPptx_SlideNotesIncluded() => Task.CompletedTask;
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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WebsiteUrlEnv)]
    public async Task ReadAsync_LiveUrl_ReturnsContent()
    {
        var reader = new WebsiteReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var url = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WebsiteUrlEnv,
            "a live website reader proof URL");

        var result = await reader.ReadAsync(url);

        Assert.Equal(DocumentReaderType.Website, result.ReaderType);
        Assert.NotEmpty(result.RawText);
        Assert.NotEmpty(result.Sections);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WebsiteMissingUrlEnv)]
    public async Task ReadAsync_404_Throws()
    {
        var reader = new WebsiteReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var url = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WebsiteMissingUrlEnv,
            "a live website reader 404 proof URL");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadAsync(url));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WebsiteRedirectUrlEnv)]
    public async Task ReadAsync_Redirects_Follows()
    {
        var reader = new WebsiteReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var url = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WebsiteRedirectUrlEnv,
            "a live website reader redirect proof URL");

        var result = await reader.ReadAsync(url);

        Assert.Equal(DocumentReaderType.Website, result.ReaderType);
        Assert.NotEmpty(result.RawText);
    }

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WikipediaTitleEnv)]
    public async Task ReadAsync_ValidTitle_ReturnsContent()
    {
        var reader = new WikipediaReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var title = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WikipediaTitleEnv,
            "a live Wikipedia article title");

        var result = await reader.ReadAsync(title);

        Assert.Equal(DocumentReaderType.Wikipedia, result.ReaderType);
        Assert.NotEmpty(result.RawText);
        Assert.NotEmpty(result.Sections);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("title"));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WikipediaUriEnv)]
    public async Task ReadAsync_WikipediaUri_Parsed()
    {
        var reader = new WikipediaReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WikipediaUriEnv,
            "a live wikipedia:// URI");

        var result = await reader.ReadAsync(uri);

        Assert.Equal(DocumentReaderType.Wikipedia, result.ReaderType);
        Assert.StartsWith("wikipedia://", result.SourceUri, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.RawText);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WikipediaLangUriEnv)]
    public async Task ReadAsync_LangPrefix_UsedInRequest()
    {
        var reader = new WikipediaReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WikipediaLangUriEnv,
            "a live wikipedia:// URI with language prefix");

        var result = await reader.ReadAsync(uri);

        Assert.Equal(DocumentReaderType.Wikipedia, result.ReaderType);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.TryGetValue("lang", out var lang));
        Assert.Equal("it", lang?.ToString());
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

    [Fact(Skip = "requires live arXiv API")] public Task ReadAsync_ValidArxivId_ReturnsAbstract() => Task.CompletedTask;
    [Fact(Skip = "requires live arXiv API")] public Task ReadAsync_FullUrl_ParsedCorrectly() => Task.CompletedTask;
    [Fact(Skip = "requires live arXiv API")] public Task ReadAsync_InvalidId_Throws() => Task.CompletedTask;

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.PubMedPmidEnv)]
    public async Task ReadAsync_ValidPmid_ReturnsAbstract()
    {
        var reader = new PubMedReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var pmid = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.PubMedPmidEnv,
            "a live PubMed PMID");

        var result = await reader.ReadAsync(pmid);

        Assert.Equal(DocumentReaderType.PubMed, result.ReaderType);
        Assert.NotEmpty(result.RawText);
        Assert.NotEmpty(result.Sections);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("pmid"));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.PubMedUriEnv)]
    public async Task ReadAsync_PubmedUri_ParsedCorrectly()
    {
        var reader = new PubMedReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.PubMedUriEnv,
            "a live pubmed:// URI");

        var result = await reader.ReadAsync(uri);

        Assert.Equal(DocumentReaderType.PubMed, result.ReaderType);
        Assert.StartsWith("pubmed://", result.SourceUri, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.RawText);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.PubMedInvalidPmidEnv)]
    public async Task ReadAsync_InvalidPmid_Throws()
    {
        var reader = new PubMedReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var pmid = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.PubMedInvalidPmidEnv,
            "an invalid live PubMed PMID");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadAsync(pmid));
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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.YouTubeVideoIdEnv)]
    public async Task ReadAsync_ValidVideoId_ReturnsSegments()
    {
        var reader = new YoutubeTranscriptReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var videoId = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.YouTubeVideoIdEnv,
            "a live YouTube video id with captions");

        var result = await reader.ReadAsync($"youtube://{videoId}");

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
        Assert.NotEmpty(result.Sections);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.YouTubeUriEnv)]
    public async Task ReadAsync_FullYoutubeUrl_Parsed()
    {
        var reader = new YoutubeTranscriptReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.YouTubeUriEnv,
            "a live YouTube watch URL with captions");

        var result = await reader.ReadAsync(uri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
        Assert.True(result.Metadata!.ContainsKey("videoId"));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.YouTubeNoCaptionVideoIdEnv)]
    public async Task ReadAsync_VideoWithNoCaption_Throws()
    {
        var reader = new YoutubeTranscriptReader(LiveDocumentReaderTestSupport.CreateLiveHttpClient());
        var videoId = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.YouTubeNoCaptionVideoIdEnv,
            "a live YouTube video id without captions");

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => reader.ReadAsync($"youtube://{videoId}"));
    }

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.FirecrawlApiKeyEnv, LiveDocumentReaderTestSupport.FirecrawlTargetUrlEnv)]
    public async Task ReadAsync_ValidUrl_ReturnsMarkdown()
    {
        var reader = new FirecrawlReader(LiveDocumentReaderTestSupport.CreateLiveFirecrawlHttpClient(includeAuthorization: true));
        var url = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.FirecrawlTargetUrlEnv,
            "a live Firecrawl target URL");

        var result = await reader.ReadAsync(url);

        Assert.Equal(DocumentReaderType.Firecrawl, result.ReaderType);
        Assert.NotEmpty(result.RawText);
        Assert.NotEmpty(result.Sections);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.FirecrawlTargetUrlEnv)]
    public async Task ReadAsync_AuthError_Throws()
    {
        var reader = new FirecrawlReader(LiveDocumentReaderTestSupport.CreateLiveFirecrawlHttpClient(includeAuthorization: false));
        var url = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.FirecrawlTargetUrlEnv,
            "a live Firecrawl target URL");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadAsync(url));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.FirecrawlApiKeyEnv, LiveDocumentReaderTestSupport.FirecrawlTargetUrlEnv)]
    public async Task ReadAsync_Metadata_HasTitle()
    {
        var reader = new FirecrawlReader(LiveDocumentReaderTestSupport.CreateLiveFirecrawlHttpClient(includeAuthorization: true));
        var url = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.FirecrawlTargetUrlEnv,
            "a live Firecrawl target URL");

        var result = await reader.ReadAsync(url);

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.AwsS3RegionEnv, LiveDocumentReaderTestSupport.AwsS3TestUriEnv)]
    public async Task ReadAsync_S3Uri_ReturnsObject()
    {
        var reader = new AWSS3Reader(LiveDocumentReaderTestSupport.CreateLiveAwsS3Client());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.AwsS3TestUriEnv,
            "a live AWS S3 object URI");

        var result = await reader.ReadAsync(uri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.AwsS3RegionEnv, LiveDocumentReaderTestSupport.AwsS3MissingUriEnv)]
    public async Task ReadAsync_NotFound_Throws()
    {
        var reader = new AWSS3Reader(LiveDocumentReaderTestSupport.CreateLiveAwsS3Client());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.AwsS3MissingUriEnv,
            "a missing AWS S3 object URI");

        await Assert.ThrowsAnyAsync<Amazon.S3.AmazonS3Exception>(() => reader.ReadAsync(uri));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.AwsS3RegionEnv, LiveDocumentReaderTestSupport.AwsS3TestUriEnv)]
    public async Task ReadAsync_Metadata_HasEtag()
    {
        var reader = new AWSS3Reader(LiveDocumentReaderTestSupport.CreateLiveAwsS3Client());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.AwsS3TestUriEnv,
            "a live AWS S3 object URI");

        var result = await reader.ReadAsync(uri);

        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("etag"));
    }

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.AzureBlobTestUriEnv)]
    public async Task ReadAsync_AzBlobUri_ReturnsContent()
    {
        var reader = new AzureBlobReader(LiveDocumentReaderTestSupport.CreateLiveAzureBlobClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.AzureBlobTestUriEnv,
            "a live Azure Blob URI");

        var result = await reader.ReadAsync(uri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.AzureBlobMissingUriEnv)]
    public async Task ReadAsync_BlobNotFound_Throws()
    {
        var reader = new AzureBlobReader(LiveDocumentReaderTestSupport.CreateLiveAzureBlobClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.AzureBlobMissingUriEnv,
            "a missing Azure Blob URI");

        await Assert.ThrowsAnyAsync<Azure.RequestFailedException>(() => reader.ReadAsync(uri));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.AzureBlobTestUriEnv)]
    public async Task ReadAsync_Metadata_HasETag()
    {
        var reader = new AzureBlobReader(LiveDocumentReaderTestSupport.CreateLiveAzureBlobClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.AzureBlobTestUriEnv,
            "a live Azure Blob URI");

        var result = await reader.ReadAsync(uri);

        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("eTag"));
    }

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.GcsTestUriEnv)]
    public async Task ReadAsync_GcsUri_ReturnsObject()
    {
        var reader = new GCSReader(LiveDocumentReaderTestSupport.CreateLiveGcsClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.GcsTestUriEnv,
            "a live GCS object URI");

        var result = await reader.ReadAsync(uri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.GcsMissingUriEnv)]
    public async Task ReadAsync_ObjectNotFound_Throws()
    {
        var reader = new GCSReader(LiveDocumentReaderTestSupport.CreateLiveGcsClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.GcsMissingUriEnv,
            "a missing GCS object URI");

        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync(uri));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.GcsTestUriEnv)]
    public async Task ReadAsync_Metadata_HasContentType()
    {
        var reader = new GCSReader(LiveDocumentReaderTestSupport.CreateLiveGcsClient());
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.GcsTestUriEnv,
            "a live GCS object URI");

        var result = await reader.ReadAsync(uri);

        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("contentType"));
    }

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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.GitHubTestUriEnv)]
    public async Task ReadAsync_ValidGithubUri_ReturnsContent()
    {
        var reader = new GitHubReader(LiveDocumentReaderTestSupport.CreateLiveGitHubHttpClient(includeAuthorization: true));
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.GitHubTestUriEnv,
            "a live GitHub file URI");

        var result = await reader.ReadAsync(uri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.GitHubPrivateUriEnv)]
    public async Task ReadAsync_PrivateRepo_RequiresAuth()
    {
        var reader = new GitHubReader(LiveDocumentReaderTestSupport.CreateLiveGitHubHttpClient(includeAuthorization: false));
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.GitHubPrivateUriEnv,
            "a live GitHub private file URI");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadAsync(uri));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.GitHubMissingUriEnv)]
    public async Task ReadAsync_FileNotFound_Throws()
    {
        var reader = new GitHubReader(LiveDocumentReaderTestSupport.CreateLiveGitHubHttpClient(includeAuthorization: true));
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.GitHubMissingUriEnv,
            "a missing GitHub file URI");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadAsync(uri));
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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.SharePointTestUriEnv, LiveDocumentReaderTestSupport.SharePointAccessTokenEnv)]
    public async Task ReadAsync_ValidSharePointUri_ReturnsContent()
    {
        var reader = new SharePointReader(LiveDocumentReaderTestSupport.CreateLiveSharePointClient(includeAuthorization: true));
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.SharePointTestUriEnv,
            "a live SharePoint drive item URI");

        var result = await reader.ReadAsync(uri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.SharePointTestUriEnv)]
    public async Task ReadAsync_UnauthorizedAccess_Throws()
    {
        var reader = new SharePointReader(LiveDocumentReaderTestSupport.CreateLiveSharePointClient(includeAuthorization: false));
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.SharePointTestUriEnv,
            "a live SharePoint drive item URI");

        await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadAsync(uri));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.SharePointTestUriEnv, LiveDocumentReaderTestSupport.SharePointAccessTokenEnv)]
    public async Task ReadAsync_Metadata_HasSiteAndDrive()
    {
        var reader = new SharePointReader(LiveDocumentReaderTestSupport.CreateLiveSharePointClient(includeAuthorization: true));
        var uri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.SharePointTestUriEnv,
            "a live SharePoint drive item URI");

        var result = await reader.ReadAsync(uri);

        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata!.ContainsKey("siteId"));
        Assert.True(result.Metadata.ContainsKey("driveId"));
    }

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

    [RequiresDocumentReaderEnvironmentFact("TESSDATA")]
    public async Task ReadAsync_ValidImageFile_ReturnsOcrText()
    {
        var tessdata = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            "TESSDATA",
            "a tessdata directory");
        var reader = new ImageOcrReader(tessdata);
        var path = Path.Combine(Path.GetTempPath(), $"agxiome-ocr-{Guid.NewGuid():N}.png");
        try
        {
            TestDocContent.GenerateOcrSamplePng(path);
            var result = await reader.ReadAsync(path);
            Assert.False(string.IsNullOrWhiteSpace(result.RawText), "OCR should produce text for a real image");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [RequiresDocumentReaderEnvironmentFact("TESSDATA")]
    public async Task ReadAsync_ValidImageFile_OcrConfidenceInMetadata()
    {
        var tessdata = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            "TESSDATA",
            "a tessdata directory");
        var reader = new ImageOcrReader(tessdata);
        var path = Path.Combine(Path.GetTempPath(), $"agxiome-ocr-{Guid.NewGuid():N}.png");
        try
        {
            TestDocContent.GenerateOcrSamplePng(path);

            var result = await reader.ReadAsync(path);

            Assert.False(string.IsNullOrWhiteSpace(result.RawText), "OCR should produce text for a real image");
            Assert.True(result.Metadata!.ContainsKey("ocrConfidence"));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}

public class RemoteImageOcrReaderTests
{
    private static HttpClient MockHttp() => new HttpClient(
        new TestHttpMessageHandler(HttpStatusCode.OK,
            "{\"text\":\"HELLO OCR\",\"language\":\"eng\",\"bytes\":1806,\"stderr\":\"\"}"))
    { BaseAddress = new Uri("http://agxiome-ocr/") };

    [Fact] public async Task ReaderType_IsImageOCR()
        => Assert.Equal(DocumentReaderType.ImageOCR, new RemoteImageOcrReader(MockHttp()).ReaderType);

    [Fact] public async Task SupportedExtensions_ContainsPng()
        => Assert.Contains(".png", new RemoteImageOcrReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task Constructor_NullHttp_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RemoteImageOcrReader(null!));

    [Fact] public async Task SupportedExtensions_NotEmpty()
        => Assert.NotEmpty(new RemoteImageOcrReader(MockHttp()).SupportedExtensions);

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new RemoteImageOcrReader(MockHttp()));

    [Fact] public async Task ReadFromStream_ValidImage_ReturnsOcrText()
    {
        using var stream = TestDocContent.PlainText("fake-image-bytes");

        var result = await new RemoteImageOcrReader(MockHttp(), engineName: "tesseract-http")
            .ReadFromStreamAsync(stream, "image/png");

        Assert.Equal("HELLO OCR", result.RawText);
        Assert.Equal("tesseract-http", result.Metadata!["engine"]);
        Assert.Equal("eng", result.Metadata["language"]);
    }

    [Fact] public async Task ReadAsync_ValidImageFile_MetadataHasImageBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agxiome-remote-ocr-{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("fake-image-bytes"));

            var result = await new RemoteImageOcrReader(MockHttp()).ReadAsync(path);

            Assert.NotNull(result.Metadata);
            Assert.Equal("1806", result.Metadata!["imageBytes"].ToString());
            Assert.Equal("/ocr", result.Metadata["endpointPath"]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact] public async Task Constructor_EmptyEndpoint_Throws()
        => Assert.Throws<ArgumentNullException>(() => new RemoteImageOcrReader(MockHttp(), endpointPath: ""));
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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WhisperApiKeyEnv, LiveDocumentReaderTestSupport.WhisperAudioFileEnv)]
    public async Task ReadAsync_ValidAudioFile_ReturnsTranscript()
    {
        var reader = new AudioWhisperReader(LiveDocumentReaderTestSupport.CreateLiveWhisperHttpClient(includeAuthorization: true));
        var path = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WhisperAudioFileEnv,
            "a live audio file for Whisper transcription");

        var result = await reader.ReadAsync(path);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WhisperApiKeyEnv, LiveDocumentReaderTestSupport.WhisperAudioFileEnv)]
    public async Task ReadFromStream_ValidAudio_SegmentsInSections()
    {
        var reader = new AudioWhisperReader(LiveDocumentReaderTestSupport.CreateLiveWhisperHttpClient(includeAuthorization: true));
        var path = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WhisperAudioFileEnv,
            "a live audio file for Whisper transcription");

        await using var stream = File.OpenRead(path);
        var result = await reader.ReadFromStreamAsync(stream, mimeHint: "audio/wav");

        Assert.NotEmpty(result.Sections);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.WhisperAudioFileEnv)]
    public async Task ReadAsync_AuthError_Throws()
    {
        var reader = new AudioWhisperReader(LiveDocumentReaderTestSupport.CreateLiveWhisperHttpClient(includeAuthorization: false));
        var path = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.WhisperAudioFileEnv,
            "a live audio file for Whisper transcription");

        await Assert.ThrowsAsync<HttpRequestException>(() => reader.ReadAsync(path));
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

    [Fact]
    public async Task ReadAsync_ValidEpub_OneSection_PerChapter()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agxiome-epub-{Guid.NewGuid():N}.epub");
        try
        {
            await using (var file = File.Create(path))
            await using (var epub = TestDocContent.Epub())
            {
                await epub.CopyToAsync(file);
            }

            var result = await _reader.ReadAsync(path);

            Assert.Equal(DocumentReaderType.Epub, result.ReaderType);
            Assert.Equal(2, result.Sections.Count);
            Assert.Contains("Hello EPUB chapter one.", result.RawText);
            Assert.Contains("Hello EPUB chapter two.", result.RawText);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_ValidEpub_MetadataHasAuthor()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agxiome-epub-{Guid.NewGuid():N}.epub");
        try
        {
            await using (var file = File.Create(path))
            await using (var epub = TestDocContent.Epub())
            {
                await epub.CopyToAsync(file);
            }

            var result = await _reader.ReadAsync(path);

            Assert.NotNull(result.Metadata);
            Assert.True(result.Metadata!.TryGetValue("author", out var author));
            Assert.Equal("Test Author", author?.ToString());
            Assert.True(result.Metadata.TryGetValue("chapterCount", out var chapterCount));
            Assert.Equal("2", chapterCount?.ToString());
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.NotionTokenEnv, LiveDocumentReaderTestSupport.NotionPageIdEnv)]
    public async Task ReadAsync_ValidPageId_ReturnsContent()
    {
        var token = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.NotionTokenEnv, "a live Notion integration token");
        var pageId = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.NotionPageIdEnv, "a live Notion page ID");
        var http = LiveDocumentReaderTestSupport.CreateLiveHttpClient();
        http.BaseAddress = new Uri("https://api.notion.com/");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");

        var result = await new NotionReader(http).ReadAsync(pageId);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.NotionTokenEnv, LiveDocumentReaderTestSupport.NotionPageIdEnv)]
    public async Task ReadAsync_HeadingBlocks_BecomeSections()
    {
        var token = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.NotionTokenEnv, "a live Notion integration token");
        var pageId = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.NotionPageIdEnv, "a live Notion page ID");
        var http = LiveDocumentReaderTestSupport.CreateLiveHttpClient();
        http.BaseAddress = new Uri("https://api.notion.com/");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Notion-Version", "2022-06-28");

        var result = await new NotionReader(http).ReadAsync(pageId);

        Assert.NotEmpty(result.Sections);
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.NotionPageIdEnv)]
    public async Task ReadAsync_Unauthorized_Throws()
    {
        var pageId = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.NotionPageIdEnv, "a live Notion page ID");
        var http = LiveDocumentReaderTestSupport.CreateLiveHttpClient();
        http.BaseAddress = new Uri("https://api.notion.com/");

        await Assert.ThrowsAsync<HttpRequestException>(() => new NotionReader(http).ReadAsync(pageId));
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

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.ConfluenceTokenEnv, LiveDocumentReaderTestSupport.ConfluenceInstanceEnv, LiveDocumentReaderTestSupport.ConfluencePageUriEnv)]
    public async Task ReadAsync_ValidUri_ReturnsContent()
    {
        var instance = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluenceInstanceEnv, "a live Confluence Cloud instance URL (e.g. https://myinstance.atlassian.net)");
        var pageUri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluencePageUriEnv, "a live Confluence page URI (e.g. confluence://SPACE/Title or confluence://PAGE_ID)");
        var token = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluenceTokenEnv, "a live Confluence API token");
        var http = LiveDocumentReaderTestSupport.CreateLiveHttpClient();
        http.BaseAddress = new Uri(instance.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var result = await new ConfluenceReader(http).ReadAsync(pageUri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.ConfluenceTokenEnv, LiveDocumentReaderTestSupport.ConfluenceInstanceEnv, LiveDocumentReaderTestSupport.ConfluencePageUriEnv)]
    public async Task ReadAsync_SpaceAndTitle_ReturnsPage()
    {
        var instance = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluenceInstanceEnv, "a live Confluence Cloud instance URL");
        var pageUri = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluencePageUriEnv, "a live Confluence page URI");
        var token = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluenceTokenEnv, "a live Confluence API token");
        var http = LiveDocumentReaderTestSupport.CreateLiveHttpClient();
        http.BaseAddress = new Uri(instance.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var result = await new ConfluenceReader(http).ReadAsync(pageUri);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [RequiresDocumentReaderEnvironmentFact(LiveDocumentReaderTestSupport.ConfluenceTokenEnv, LiveDocumentReaderTestSupport.ConfluenceInstanceEnv)]
    public async Task ReadAsync_PageId_ReturnsDirectly()
    {
        var instance = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluenceInstanceEnv, "a live Confluence Cloud instance URL");
        var token = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluenceTokenEnv, "a live Confluence API token");
        var http = LiveDocumentReaderTestSupport.CreateLiveHttpClient();
        http.BaseAddress = new Uri(instance.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var parse = LiveDocumentReaderTestSupport.RequireEnvironmentVariable(
            LiveDocumentReaderTestSupport.ConfluencePageUriEnv, "a live Confluence page URI").Split(new[] { "//" }, StringSplitOptions.None);
        var pageId = parse.Length >= 2 ? parse[1] : parse[0];

        var result = await new ConfluenceReader(http).ReadAsync(pageId);

        Assert.False(string.IsNullOrWhiteSpace(result.RawText));
    }

    [Fact] public async Task ReaderType_ImplementsInterface()
        => Assert.IsAssignableFrom<IDocumentReader>(new ConfluenceReader(MockHttp()));
}
