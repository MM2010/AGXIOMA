using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tesseract;

namespace AGXIOME.Storage.Readers;

// ─────────────────────────────────────────────────────────────────────────────
// R-24..R-25 — Media Readers
//
// R-24: ImageOcrReader — uses Tesseract OCR (native library required).
// R-25: AudioWhisperReader — uses OpenAI Whisper API via HttpClient.
// ─────────────────────────────────────────────────────────────────────────────

// ─── R-24 — Image OCR ─────────────────────────────────────────────────────────

/// <summary>R-24 — Image OCR reader using Tesseract 5.x.
/// Requires native Tesseract libraries and tessdata language packs at runtime.
/// dataPath: directory containing tessdata/ (e.g., /usr/share/tessdata or "./tessdata").
/// language: Tesseract language code (e.g., "eng", "ita").
/// Tests that instantiate this class with a real dataPath must be marked [Fact(Skip = "requires native Tesseract library")].
/// </summary>
public sealed class ImageOcrReader : IDocumentReader
{
    private readonly string _dataPath;
    private readonly string _language;

    public DocumentReaderType ReaderType => DocumentReaderType.ImageOCR;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp", ".gif" };

    /// <param name="dataPath">Path to the directory containing the tessdata/ language packs folder.</param>
    /// <param name="language">Tesseract language code. Defaults to "eng".</param>
    public ImageOcrReader(string dataPath, string language = "eng")
    {
        if (string.IsNullOrWhiteSpace(dataPath)) throw new ArgumentNullException(nameof(dataPath));
        _dataPath = dataPath;
        _language = language;
    }

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        return await ReadFromStreamAsync(stream, null, ct);
    }

    public Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Copy to MemoryStream since Tesseract requires seekable, in-memory byte arrays
        var ms = new MemoryStream();
        stream.CopyTo(ms);
        var imageBytes = ms.ToArray();

        using var engine = new TesseractEngine(_dataPath, _language, EngineMode.Default);
        using var pix = Pix.LoadFromMemory(imageBytes);
        using var page = engine.Process(pix);

        var text = page.GetText()?.Trim() ?? "";
        var confidence = page.GetMeanConfidence();

        var sections = new List<DocumentSection>
        {
            new DocumentSection(null, text, 0,
                new Dictionary<string, object> { ["ocrConfidence"] = confidence.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) }),
        };

        var meta = new Dictionary<string, object>
        {
            ["language"]      = _language,
            ["ocrConfidence"] = confidence.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            ["imageBytes"]    = imageBytes.Length,
        };

        return Task.FromResult(new DocumentContent("stream://image-ocr", DocumentReaderType.ImageOCR, text, sections, meta));
    }
}

/// <summary>Remote adapter for OCR services exposed over HTTP.
/// This keeps OCR out of process so the caller can target different engines behind one contract.
/// Expected response shape: { "text": "...", "language": "eng", "bytes": 1234, "stderr": "" }.
/// Example engines behind the endpoint: local Tesseract service, PaddleOCR service, EasyOCR,
/// Azure AI Vision OCR, Google Vision OCR, AWS Textract gateway.</summary>
public sealed class RemoteImageOcrReader : IDocumentReader
{
    private readonly HttpClient _http;
    private readonly string _endpointPath;
    private readonly string _defaultLanguage;
    private readonly string _engineName;

    public DocumentReaderType ReaderType => DocumentReaderType.ImageOCR;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".png", ".jpg", ".jpeg", ".tiff", ".tif", ".bmp", ".gif" };

    public RemoteImageOcrReader(
        HttpClient http,
        string endpointPath = "ocr",
        string defaultLanguage = "eng",
        string engineName = "remote-ocr")
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));

        if (string.IsNullOrWhiteSpace(endpointPath)) throw new ArgumentNullException(nameof(endpointPath));
        if (string.IsNullOrWhiteSpace(defaultLanguage)) throw new ArgumentNullException(nameof(defaultLanguage));
        if (string.IsNullOrWhiteSpace(engineName)) throw new ArgumentNullException(nameof(engineName));

        _endpointPath = endpointPath.TrimStart('/');
        _defaultLanguage = defaultLanguage;
        _engineName = engineName;
    }

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        var mimeHint = Path.GetExtension(uri).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };

        return await ReadFromStreamAsync(stream, mimeHint, ct);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        using var form = new MultipartFormDataContent();
        using var imageContent = new StreamContent(ms);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(mimeHint ?? "application/octet-stream");
        form.Add(imageContent, "file", "image.bin");
        form.Add(new StringContent(_defaultLanguage), "language");

        using var resp = await _http.PostAsync(_endpointPath, form, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var text = root.TryGetProperty("text", out var textProp)
            ? textProp.GetString() ?? string.Empty
            : string.Empty;

        var language = root.TryGetProperty("language", out var languageProp)
            ? languageProp.GetString() ?? _defaultLanguage
            : _defaultLanguage;

        var imageBytes = root.TryGetProperty("bytes", out var bytesProp) && bytesProp.TryGetInt64(out var parsedBytes)
            ? parsedBytes
            : ms.Length;

        var meta = new Dictionary<string, object>
        {
            ["language"] = language,
            ["imageBytes"] = imageBytes,
            ["engine"] = _engineName,
            ["endpointPath"] = "/" + _endpointPath,
        };

        if (root.TryGetProperty("stderr", out var stderrProp))
        {
            var stderr = stderrProp.GetString();
            if (!string.IsNullOrWhiteSpace(stderr))
                meta["stderr"] = stderr;
        }

        var sections = new List<DocumentSection>
        {
            new(null, text, 0),
        };

        return new DocumentContent("stream://remote-image-ocr", DocumentReaderType.ImageOCR, text, sections, meta);
    }
}

// ─── R-25 — Audio Whisper ─────────────────────────────────────────────────────

/// <summary>R-25 — Audio transcription reader using the OpenAI Whisper API.
/// Uploads audio file as multipart/form-data to the transcriptions endpoint.
/// HttpClient.BaseAddress must be set to "https://api.openai.com/" and
/// Authorization header must carry a valid "Bearer {api_key}" token.
/// Returns transcript text with optional segment timestamps as sections.
/// Supported formats: mp3, mp4, mpeg, mpga, m4a, wav, webm (per OpenAI Whisper spec).</summary>
public sealed class AudioWhisperReader : IDocumentReader
{
    private readonly HttpClient _http;
    private readonly string _model;

    public DocumentReaderType ReaderType => DocumentReaderType.AudioWhisper;
    public IReadOnlyList<string> SupportedExtensions { get; } = new[] { ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".wav", ".webm" };

    /// <param name="http">HttpClient pre-configured with base URL and Authorization header.</param>
    /// <param name="model">Whisper model identifier. Defaults to "whisper-1".</param>
    public AudioWhisperReader(HttpClient http, string model = "whisper-1")
    {
        _http  = http  ?? throw new ArgumentNullException(nameof(http));
        _model = model;
    }

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(uri);
        var mimeHint = Path.GetExtension(uri).ToLowerInvariant() switch
        {
            ".mp3"  => "audio/mpeg",
            ".mp4"  => "audio/mp4",
            ".wav"  => "audio/wav",
            ".webm" => "audio/webm",
            ".m4a"  => "audio/mp4",
            _       => "application/octet-stream",
        };
        return await ReadFromStreamAsync(stream, mimeHint, ct);
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        // Read entire audio into memory (Whisper API requires the full file in one call)
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;

        // Build multipart form data
        using var form = new MultipartFormDataContent();
        var audioContent = new StreamContent(ms);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(mimeHint ?? "application/octet-stream");
        form.Add(audioContent, "file", "audio.bin");
        form.Add(new StringContent(_model), "model");
        form.Add(new StringContent("verbose_json"), "response_format");
        form.Add(new StringContent("en"), "language");

        using var resp = await _http.PostAsync("v1/audio/transcriptions", form, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        var fullText = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
        var sections = new List<DocumentSection>();

        // Verbose JSON includes "segments" with start/end timestamps
        if (root.TryGetProperty("segments", out var segs))
        {
            foreach (var seg in segs.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                var segText  = seg.TryGetProperty("text",  out var st) ? st.GetString() ?? "" : "";
                var start    = seg.TryGetProperty("start", out var ss) ? ss.GetDouble() : 0.0;
                var end      = seg.TryGetProperty("end",   out var se) ? se.GetDouble() : 0.0;
                sections.Add(new DocumentSection(
                    Heading: $"{start:F1}s - {end:F1}s",
                    Content: segText.Trim(),
                    Level: 1,
                    Metadata: new Dictionary<string, object>
                    {
                        ["startSec"] = start.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        ["endSec"]   = end.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    }));
            }
        }

        if (sections.Count == 0)
            sections.Add(new DocumentSection(null, fullText, 0));

        var meta = new Dictionary<string, object>
        {
            ["model"]          = _model,
            ["segmentCount"]   = sections.Count,
        };

        if (root.TryGetProperty("language", out var lang))
            meta["detectedLanguage"] = lang.GetString() ?? "";
        if (root.TryGetProperty("duration", out var dur))
            meta["durationSec"] = dur.GetDouble().ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        return new DocumentContent("stream://whisper", DocumentReaderType.AudioWhisper, fullText, sections, meta);
    }
}
