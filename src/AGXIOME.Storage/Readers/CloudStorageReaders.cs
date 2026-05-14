using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Google.Cloud.Storage.V1;
using Microsoft.Graph;
using System.Text;

namespace AGXIOME.Storage.Readers;

// ─────────────────────────────────────────────────────────────────────────────
// R-19..R-21, R-23 — Cloud Storage Readers
//
// All readers receive SDK clients via constructor injection.
// ReadFromStreamAsync is supported where stream semantics apply (R-19..R-21).
// ReadAsync parses cloud URIs to locate and download the object.
// ─────────────────────────────────────────────────────────────────────────────

// ─── R-19 — AWS S3 ────────────────────────────────────────────────────────────

/// <summary>R-19 — AWS S3 reader using AWSSDK.S3.
/// URI format: "s3://bucket-name/key/path/to/object" or "https://bucket.s3.region.amazonaws.com/key".
/// Downloads the object and returns its content as a DocumentContent.
/// Metadata (ContentType, ETag, LastModified) included in result metadata.</summary>
public sealed class AWSS3Reader : IDocumentReader
{
    private readonly IAmazonS3 _s3;
    public DocumentReaderType ReaderType => DocumentReaderType.AWSS3;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public AWSS3Reader(IAmazonS3 s3)
        => _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var (bucket, key) = ParseS3Uri(uri);
        var request = new GetObjectRequest { BucketName = bucket, Key = key };
        using var resp = await _s3.GetObjectAsync(request, ct);
        await using var stream = resp.ResponseStream;

        var content = await ReadFromStreamAsync(stream, resp.Headers.ContentType, ct);

        // Enrich metadata from S3 object headers
        var meta = content.Metadata as Dictionary<string, object> ?? new Dictionary<string, object>(content.Metadata ?? new Dictionary<string, object>());
        meta["etag"]         = resp.ETag;
        meta["lastModified"] = $"{resp.LastModified:O}";
        meta["contentType"]  = resp.Headers.ContentType;
        meta["bucket"]       = bucket;
        meta["key"]          = key;

        return content with { SourceUri = uri, Metadata = meta };
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        var sections = new List<DocumentSection>
        {
            new DocumentSection(null, text, 0),
        };
        return new DocumentContent("stream://s3", DocumentReaderType.AWSS3, text, sections);
    }

    private static (string bucket, string key) ParseS3Uri(string uri)
    {
        if (uri.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri["s3://".Length..];
            var slash = path.IndexOf('/');
            return slash < 0 ? (path, "") : (path[..slash], path[(slash + 1)..]);
        }
        // Handle https://bucket.s3.region.amazonaws.com/key
        var u = new Uri(uri);
        var bucket = u.Host.Split('.')[0];
        var key = u.AbsolutePath.TrimStart('/');
        return (bucket, key);
    }
}

// ─── R-20 — Azure Blob ────────────────────────────────────────────────────────

/// <summary>R-20 — Azure Blob Storage reader using Azure.Storage.Blobs.
/// URI format: "azblob://container-name/blob-path" or full Azure Blob URL.
/// Downloads blob content and returns as DocumentContent.
/// BlobServiceClient must be pre-configured (connection string or managed identity).</summary>
public sealed class AzureBlobReader : IDocumentReader
{
    private readonly BlobServiceClient _client;
    public DocumentReaderType ReaderType => DocumentReaderType.AzureBlob;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public AzureBlobReader(BlobServiceClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var (container, blobPath) = ParseAzUri(uri);
        var containerClient = _client.GetBlobContainerClient(container);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var ms = new MemoryStream();
        await blobClient.DownloadToAsync(ms, ct);
        ms.Position = 0;

        var propsResp = await blobClient.GetPropertiesAsync(cancellationToken: ct);
        var props = propsResp.Value;

        var content = await ReadFromStreamAsync(ms, props.ContentType, ct);

        var meta = content.Metadata as Dictionary<string, object> ?? new Dictionary<string, object>(content.Metadata ?? new Dictionary<string, object>());
        meta["container"]    = container;
        meta["blobPath"]     = blobPath;
        meta["contentType"]  = props.ContentType;
        meta["eTag"]         = props.ETag.ToString();
        meta["lastModified"] = props.LastModified.ToString("O");

        return content with { SourceUri = uri, Metadata = meta };
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        var sections = new List<DocumentSection>
        {
            new DocumentSection(null, text, 0),
        };
        return new DocumentContent("stream://azblob", DocumentReaderType.AzureBlob, text, sections);
    }

    private static (string container, string blobPath) ParseAzUri(string uri)
    {
        if (uri.StartsWith("azblob://", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri["azblob://".Length..];
            var slash = path.IndexOf('/');
            return slash < 0 ? (path, "") : (path[..slash], path[(slash + 1)..]);
        }
        var u = new Uri(uri);
        var parts = u.AbsolutePath.TrimStart('/').Split('/', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "");
    }
}

// ─── R-21 — Google Cloud Storage ──────────────────────────────────────────────

/// <summary>R-21 — Google Cloud Storage reader using Google.Cloud.Storage.V1.
/// URI format: "gcs://bucket-name/object-name" or "gs://bucket-name/object-name".
/// Downloads the object and returns as DocumentContent.
/// StorageClient must be pre-authenticated (ADC or explicit credentials).</summary>
public sealed class GCSReader : IDocumentReader
{
    private readonly StorageClient _client;
    public DocumentReaderType ReaderType => DocumentReaderType.GCS;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public GCSReader(StorageClient client)
        => _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var (bucket, objectName) = ParseGcsUri(uri);
        var ms = new MemoryStream();
        var obj = await _client.DownloadObjectAsync(bucket, objectName, ms, cancellationToken: ct);
        ms.Position = 0;

        var content = await ReadFromStreamAsync(ms, obj.ContentType, ct);

        var meta = content.Metadata as Dictionary<string, object> ?? new Dictionary<string, object>(content.Metadata ?? new Dictionary<string, object>());
        meta["bucket"]      = bucket;
        meta["objectName"]  = objectName;
        meta["contentType"] = obj.ContentType ?? "";
        meta["size"]        = (object)(obj.Size ?? 0UL);
        meta["etag"]        = obj.ETag ?? "";

        return content with { SourceUri = uri, Metadata = meta };
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        var sections = new List<DocumentSection>
        {
            new DocumentSection(null, text, 0),
        };
        return new DocumentContent("stream://gcs", DocumentReaderType.GCS, text, sections);
    }

    private static (string bucket, string objectName) ParseGcsUri(string uri)
    {
        // Accept both "gcs://" and "gs://" schemes
        var scheme = uri.StartsWith("gs://", StringComparison.OrdinalIgnoreCase) ? "gs://" : "gcs://";
        var path = uri[scheme.Length..];
        var slash = path.IndexOf('/');
        return slash < 0 ? (path, "") : (path[..slash], path[(slash + 1)..]);
    }
}

// ─── R-23 — SharePoint / Microsoft Graph ──────────────────────────────────────

/// <summary>R-23 — SharePoint Online reader using Microsoft.Graph.
/// URI format: "sharepoint://SITE_ID/DRIVE_ID/ITEM_ID" or "sharepoint://site/path".
/// Downloads file content via the Graph Drive API.
/// GraphServiceClient must be pre-configured with authentication provider.</summary>
public sealed class SharePointReader : IDocumentReader
{
    private readonly GraphServiceClient _graph;
    public DocumentReaderType ReaderType => DocumentReaderType.SharePoint;
    public IReadOnlyList<string> SupportedExtensions { get; } = Array.Empty<string>();

    public SharePointReader(GraphServiceClient graph)
        => _graph = graph ?? throw new ArgumentNullException(nameof(graph));

    public async Task<DocumentContent> ReadAsync(string uri, CancellationToken ct = default)
    {
        var (siteId, driveId, itemId) = ParseUri(uri);

        // Download content stream via Microsoft Graph Drive API
        Stream? contentStream;
        string? name = null;
        string? eTag = null;

        if (!string.IsNullOrEmpty(driveId) && !string.IsNullOrEmpty(itemId))
        {
            // Use drive-scoped items API: /drives/{drive-id}/items/{item-id}
            var item = await _graph.Drives[driveId].Items[itemId].GetAsync(cancellationToken: ct);
            name = item?.Name;
            eTag = item?.ETag;
            contentStream = await _graph.Drives[driveId].Items[itemId].Content.GetAsync(cancellationToken: ct);
        }
        else
        {
            // driveId + itemId are required; without them we cannot resolve a SharePoint file
            return new DocumentContent(uri, DocumentReaderType.SharePoint, "", Array.Empty<DocumentSection>());
        }

        if (contentStream is null)
            return new DocumentContent(uri, DocumentReaderType.SharePoint, "", Array.Empty<DocumentSection>());

        var content = await ReadFromStreamAsync(contentStream, null, ct);

        var meta = content.Metadata as Dictionary<string, object> ?? new Dictionary<string, object>(content.Metadata ?? new Dictionary<string, object>());
        meta["siteId"]  = siteId;
        meta["driveId"] = driveId;
        meta["itemId"]  = itemId;
        if (name is not null) meta["name"] = name;
        if (eTag is not null) meta["eTag"] = eTag;

        return content with { SourceUri = uri, Metadata = meta };
    }

    public async Task<DocumentContent> ReadFromStreamAsync(Stream stream, string? mimeHint = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        var sections = new List<DocumentSection>
        {
            new DocumentSection(null, text, 0),
        };
        return new DocumentContent("stream://sharepoint", DocumentReaderType.SharePoint, text, sections);
    }

    private static (string siteId, string driveId, string itemId) ParseUri(string uri)
    {
        var s = uri.StartsWith("sharepoint://", StringComparison.OrdinalIgnoreCase)
            ? uri["sharepoint://".Length..] : uri;
        var parts = s.Split('/', 3);
        return parts.Length switch
        {
            3 => (parts[0], parts[1], parts[2]),
            2 => (parts[0], parts[1], ""),
            _ => (s, "", ""),
        };
    }
}
