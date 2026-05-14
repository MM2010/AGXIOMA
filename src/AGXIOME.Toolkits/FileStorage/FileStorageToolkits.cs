// T-132..T-141 — File & Storage Toolkits
// ─────────────────────────────────────────────────────────────────────────────
// LocalFileToolkit    (T-132) : 17 BCL actions — read/write/append/copy/move/
//                               list/search/head/tail/hash/mkdir/rmdir/info
// CompressionToolkit  (T-139) : System.IO.Compression — zip/unzip/list_zip/
//                               add_entry/gzip_compress/gzip_decompress
// PdfToolkit          (T-141) : UglyToad.PdfPig — extract_text/page_count/
//                               extract_page/metadata/search_text
// ImageProcessingToolkit(T-140): SixLabors.ImageSharp — resize/convert/info/
//                               crop/grayscale/rotate/flip/thumbnail
// FtpSftpToolkit      (T-133) : requires FluentFTP + SSH.NET packages (stub)
// Cloud toolkits      (T-134..T-138): require vendor SDKs (stubs)
// ─────────────────────────────────────────────────────────────────────────────

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;

namespace AGXIOME.Toolkits.FileStorage;

// ─── T-132: Local File ───────────────────────────────────────────────────────

/// <summary>
/// Full local filesystem toolkit — no API key required.
/// Actions: read, read_bytes, write, append, write_lines, delete, list, exists,
///          copy, move, mkdir, rmdir, info, search, head, tail, hash
/// </summary>
[Toolkit("local_file", "Local File", ToolkitCategory.FileStorage)]
public sealed class LocalFileToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "local_file";
    public override string Name => "Local File";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;
    public override bool IsConfigured => true;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) => action switch
    {
        "read"        => ReadAsync(p, ct),
        "read_bytes"  => Task.FromResult(ReadBytes(p)),
        "write"       => WriteAsync(p, ct),
        "append"      => AppendAsync(p, ct),
        "write_lines" => WriteLinesAsync(p, ct),
        "delete"      => Task.FromResult(DeleteFile(p)),
        "list"        => Task.FromResult(List(p)),
        "exists"      => Task.FromResult(Exists(p)),
        "copy"        => Task.FromResult(CopyFile(p)),
        "move"        => Task.FromResult(MoveFile(p)),
        "mkdir"       => Task.FromResult(MakeDir(p)),
        "rmdir"       => Task.FromResult(RemoveDir(p)),
        "info"        => Task.FromResult(Info(p)),
        "search"      => Task.FromResult(Search(p)),
        "head"        => Task.FromResult(Head(p)),
        "tail"        => Task.FromResult(Tail(p)),
        "hash"        => Task.FromResult(Hash(p)),
        _ => Task.FromResult(ToolkitResult.Fail(
            $"Unknown action '{action}'. Valid: read, read_bytes, write, append, write_lines, " +
            "delete, list, exists, copy, move, mkdir, rmdir, info, search, head, tail, hash"))
    };

    // ── read ──────────────────────────────────────────────────────────────────
    private static async Task<ToolkitResult> ReadAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        var enc = GetParam<string>(p, "encoding") is { } name ? Encoding.GetEncoding(name) : Encoding.UTF8;
        var content = await File.ReadAllTextAsync(path, enc, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { path, content, size = content.Length, encoding = enc.WebName });
    }

    // ── read_bytes ────────────────────────────────────────────────────────────
    private static ToolkitResult ReadBytes(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        var bytes = File.ReadAllBytes(path);
        return ToolkitResult.Success(new { path, base64 = Convert.ToBase64String(bytes), size = bytes.Length });
    }

    // ── write ─────────────────────────────────────────────────────────────────
    private static async Task<ToolkitResult> WriteAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var content = GetParam<string>(p, "content") ?? "";
        var overwrite = GetParam<bool?>(p, "overwrite") ?? true;
        if (!overwrite && File.Exists(path)) return ToolkitResult.Fail($"File exists and overwrite=false: {path}");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, content, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { path, written = true, size = content.Length });
    }

    // ── append ────────────────────────────────────────────────────────────────
    private static async Task<ToolkitResult> AppendAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var content = GetParam<string>(p, "content") ?? "";
        await File.AppendAllTextAsync(path, content, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { path, appended = true, appendedChars = content.Length });
    }

    // ── write_lines ───────────────────────────────────────────────────────────
    private static async Task<ToolkitResult> WriteLinesAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var lines = GetParam<string[]>(p, "lines") ?? [];
        await File.WriteAllLinesAsync(path, lines, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { path, lineCount = lines.Length });
    }

    // ── delete ────────────────────────────────────────────────────────────────
    private static ToolkitResult DeleteFile(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        File.Delete(path);
        return ToolkitResult.Success(new { path, deleted = true });
    }

    // ── list ──────────────────────────────────────────────────────────────────
    private static ToolkitResult List(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!Directory.Exists(path)) return ToolkitResult.Fail($"Directory not found: {path}");
        var pattern = GetParam<string>(p, "pattern") ?? "*";
        var recursive = GetParam<bool?>(p, "recursive") ?? false;
        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var entries = Directory.EnumerateFileSystemEntries(path, pattern, opt)
            .Select(e => new
            {
                path = e,
                isFile = File.Exists(e),
                size = File.Exists(e) ? (long?)new FileInfo(e).Length : null
            }).ToArray();
        return ToolkitResult.Success(new { path, count = entries.Length, entries });
    }

    // ── exists ────────────────────────────────────────────────────────────────
    private static ToolkitResult Exists(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        return ToolkitResult.Success(new
        {
            path,
            exists = File.Exists(path) || Directory.Exists(path),
            isFile = File.Exists(path),
            isDirectory = Directory.Exists(path)
        });
    }

    // ── copy ──────────────────────────────────────────────────────────────────
    private static ToolkitResult CopyFile(Dictionary<string, object?> p)
    {
        var src = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? GetParam<string>(p, "dest") ?? "";
        if (!File.Exists(src)) return ToolkitResult.Fail($"Source not found: {src}");
        if (string.IsNullOrEmpty(dest)) return ToolkitResult.Fail("'destination' parameter required");
        var overwrite = GetParam<bool?>(p, "overwrite") ?? false;
        File.Copy(src, dest, overwrite);
        return ToolkitResult.Success(new { source = src, destination = dest, copied = true });
    }

    // ── move ──────────────────────────────────────────────────────────────────
    private static ToolkitResult MoveFile(Dictionary<string, object?> p)
    {
        var src = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? GetParam<string>(p, "dest") ?? "";
        if (!File.Exists(src)) return ToolkitResult.Fail($"Source not found: {src}");
        if (string.IsNullOrEmpty(dest)) return ToolkitResult.Fail("'destination' parameter required");
        File.Move(src, dest);
        return ToolkitResult.Success(new { source = src, destination = dest, moved = true });
    }

    // ── mkdir ─────────────────────────────────────────────────────────────────
    private static ToolkitResult MakeDir(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        Directory.CreateDirectory(path);
        return ToolkitResult.Success(new { path, created = true });
    }

    // ── rmdir ─────────────────────────────────────────────────────────────────
    private static ToolkitResult RemoveDir(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!Directory.Exists(path)) return ToolkitResult.Fail($"Directory not found: {path}");
        Directory.Delete(path, recursive: true);
        return ToolkitResult.Success(new { path, deleted = true });
    }

    // ── info ──────────────────────────────────────────────────────────────────
    private static ToolkitResult Info(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (File.Exists(path))
        {
            var fi = new FileInfo(path);
            return ToolkitResult.Success(new
            {
                path, type = "file",
                size = fi.Length,
                sizeFormatted = FmtSize(fi.Length),
                created = fi.CreationTimeUtc,
                modified = fi.LastWriteTimeUtc,
                accessed = fi.LastAccessTimeUtc,
                extension = fi.Extension,
                readOnly = fi.IsReadOnly,
                attributes = fi.Attributes.ToString()
            });
        }
        if (Directory.Exists(path))
        {
            var di = new DirectoryInfo(path);
            var files = di.EnumerateFiles("*", SearchOption.AllDirectories).ToArray();
            var totalSize = files.Sum(f => f.Length);
            return ToolkitResult.Success(new
            {
                path, type = "directory",
                fileCount = files.Length,
                totalSize,
                totalSizeFormatted = FmtSize(totalSize),
                created = di.CreationTimeUtc,
                modified = di.LastWriteTimeUtc
            });
        }
        return ToolkitResult.Fail($"Path not found: {path}");
    }

    // ── search ────────────────────────────────────────────────────────────────
    private static ToolkitResult Search(Dictionary<string, object?> p)
    {
        var basePath = GetParam<string>(p, "path") ?? ".";
        if (!Directory.Exists(basePath)) return ToolkitResult.Fail($"Directory not found: {basePath}");
        var pattern = GetParam<string>(p, "pattern") ?? "*";
        var contains = GetParam<string>(p, "contains");
        var maxResults = GetParam<int?>(p, "max_results") ?? 100;

        IEnumerable<string> files = Directory.EnumerateFiles(basePath, pattern, SearchOption.AllDirectories);
        if (contains != null)
            files = files.Where(f =>
            {
                try { return File.ReadAllText(f).Contains(contains, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
        var results = files.Take(maxResults)
            .Select(f => new { path = f, size = new FileInfo(f).Length })
            .ToArray();
        return ToolkitResult.Success(new { basePath, pattern, contains, count = results.Length, results });
    }

    // ── head ──────────────────────────────────────────────────────────────────
    private static ToolkitResult Head(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        var n = GetParam<int?>(p, "lines") ?? 10;
        var lines = File.ReadLines(path).Take(n).ToArray();
        return ToolkitResult.Success(new { path, requestedLines = n, returnedLines = lines.Length, lines });
    }

    // ── tail ──────────────────────────────────────────────────────────────────
    private static ToolkitResult Tail(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        var n = GetParam<int?>(p, "lines") ?? 10;
        var all = File.ReadLines(path).ToArray();
        var tail = all[^Math.Min(n, all.Length)..];
        return ToolkitResult.Success(new { path, requestedLines = n, returnedLines = tail.Length, lines = tail });
    }

    // ── hash ──────────────────────────────────────────────────────────────────
    private static ToolkitResult Hash(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        var algorithm = GetParam<string>(p, "algorithm")?.ToUpperInvariant() ?? "SHA256";
        using var hashAlg = algorithm switch
        {
            "MD5"    => (HashAlgorithm)MD5.Create(),
            "SHA1"   => SHA1.Create(),
            "SHA512" => SHA512.Create(),
            _        => SHA256.Create()
        };
        using var stream = File.OpenRead(path);
        var hex = Convert.ToHexString(hashAlg.ComputeHash(stream)).ToLowerInvariant();
        return ToolkitResult.Success(new { path, algorithm = algorithm.ToUpperInvariant(), hash = hex });
    }

    private static string FmtSize(long b) => b switch
    {
        < 1024            => $"{b} B",
        < 1024 * 1024     => $"{b / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{b / (1024.0 * 1024):F1} MB",
        _                 => $"{b / (1024.0 * 1024 * 1024):F2} GB"
    };
}

// ─── T-133: FTP / SFTP ───────────────────────────────────────────────────────
// Requires: FluentFTP (NuGet) for FTP, Renci.SshNet for SFTP
// Config keys: host, port, username, password, protocol (ftp|sftp)

[Toolkit("ftp_sftp", "FTP/SFTP", ToolkitCategory.FileStorage)]
public sealed class FtpSftpToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "ftp_sftp";
    public override string Name => "FTP/SFTP";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) =>
        Task.FromResult(action switch
        {
            "upload" or "download" or "list" or "delete" or "mkdir" or "exists" =>
                ToolkitResult.Fail($"FTP/SFTP '{action}': add FluentFTP (FTP) or Renci.SshNet (SFTP) packages to implement."),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: upload, download, list, delete, mkdir, exists")
        });
}

// ─── T-134: Google Cloud Storage ─────────────────────────────────────────────
// Requires: Google.Cloud.Storage.V1 (NuGet)
// Config keys: project_id, bucket, credentials_json (service account)

[Toolkit("gcs", "Google Cloud Storage", ToolkitCategory.FileStorage)]
public sealed class GcsToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "gcs";
    public override string Name => "Google Cloud Storage";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) =>
        Task.FromResult(action switch
        {
            "upload" or "download" or "list" or "delete" or "get_metadata" or "generate_signed_url" =>
                ToolkitResult.Fail($"GCS '{action}': add Google.Cloud.Storage.V1 package to implement."),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: upload, download, list, delete, get_metadata, generate_signed_url")
        });
}

// ─── T-135: MinIO ─────────────────────────────────────────────────────────────
// Requires: Minio (NuGet)
// Config keys: endpoint, access_key, secret_key, use_ssl, bucket

[Toolkit("minio", "MinIO", ToolkitCategory.FileStorage)]
public sealed class MinioToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "minio";
    public override string Name => "MinIO";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) =>
        Task.FromResult(action switch
        {
            "upload" or "download" or "list" or "delete" or "get_object_info" or "create_bucket" =>
                ToolkitResult.Fail($"MinIO '{action}': add Minio NuGet package to implement."),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: upload, download, list, delete, get_object_info, create_bucket")
        });
}

// ─── T-136: Dropbox ───────────────────────────────────────────────────────────
// Config keys: access_token
// API: https://api.dropboxapi.com/2/

[Toolkit("dropbox", "Dropbox", ToolkitCategory.FileStorage)]
public sealed class DropboxToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "dropbox";
    public override string Name => "Dropbox";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) =>
        Task.FromResult(action switch
        {
            "upload" or "download" or "list" or "delete" or "move" or "copy" or "get_metadata" or "search" =>
                ToolkitResult.Fail($"Dropbox '{action}': configure access_token and inject HttpClient to implement."),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: upload, download, list, delete, move, copy, get_metadata, search")
        });
}

// ─── T-137: Box ───────────────────────────────────────────────────────────────
// Config keys: client_id, client_secret, access_token
// API: https://api.box.com/2.0/

[Toolkit("box", "Box", ToolkitCategory.FileStorage)]
public sealed class BoxToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "box";
    public override string Name => "Box";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) =>
        Task.FromResult(action switch
        {
            "upload" or "download" or "list" or "delete" or "move" or "copy" or "get_metadata" or "search" =>
                ToolkitResult.Fail($"Box '{action}': configure access_token and inject HttpClient to implement."),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: upload, download, list, delete, move, copy, get_metadata, search")
        });
}

// ─── T-138: SharePoint ────────────────────────────────────────────────────────
// Requires: Microsoft.Graph (NuGet) or plain HttpClient with Graph API
// Config keys: tenant_id, client_id, client_secret, site_url

[Toolkit("sharepoint", "SharePoint", ToolkitCategory.FileStorage)]
public sealed class SharePointToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "sharepoint";
    public override string Name => "SharePoint";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) =>
        Task.FromResult(action switch
        {
            "upload" or "download" or "list" or "delete" or "move" or "search" or "get_metadata" =>
                ToolkitResult.Fail($"SharePoint '{action}': add Microsoft.Graph package and configure tenant_id/client_id/client_secret."),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: upload, download, list, delete, move, search, get_metadata")
        });
}

// ─── T-139: Compression ───────────────────────────────────────────────────────

/// <summary>
/// Full archive toolkit using System.IO.Compression (BCL — no extra packages).
/// Actions: zip, unzip, list_zip, add_entry, gzip_compress, gzip_decompress
/// </summary>
[Toolkit("compression", "Compression", ToolkitCategory.FileStorage)]
public sealed class CompressionToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "compression";
    public override string Name => "Compression";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;
    public override bool IsConfigured => true;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) => action switch
    {
        "zip"              => ZipAsync(p, ct),
        "unzip"            => UnzipAsync(p, ct),
        "list_zip"         => Task.FromResult(ListZip(p)),
        "add_entry"        => Task.FromResult(AddEntry(p)),
        "gzip_compress"    => GzipCompressAsync(p, ct),
        "gzip_decompress"  => GzipDecompressAsync(p, ct),
        _ => Task.FromResult(ToolkitResult.Fail(
            $"Unknown action '{action}'. Valid: zip, unzip, list_zip, add_entry, gzip_compress, gzip_decompress"))
    };

    private static async Task<ToolkitResult> ZipAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var source = GetParam<string>(p, "source") ?? "";
        var dest = GetParam<string>(p, "destination") ?? source.TrimEnd('/').TrimEnd('\\') + ".zip";
        var level = GetParam<string>(p, "level") switch
        {
            "fastest"        => CompressionLevel.Fastest,
            "no_compression" => CompressionLevel.NoCompression,
            "smallest"       => CompressionLevel.SmallestSize,
            _                => CompressionLevel.Optimal
        };

        if (Directory.Exists(source))
        {
            if (File.Exists(dest)) File.Delete(dest);
            await Task.Run(() => ZipFile.CreateFromDirectory(source, dest, level, false), ct).ConfigureAwait(false);
            return ToolkitResult.Success(new { source, destination = dest, type = "directory", size = new FileInfo(dest).Length });
        }
        if (File.Exists(source))
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(dest, ZipArchiveMode.Create);
                archive.CreateEntryFromFile(source, Path.GetFileName(source), level);
            }, ct).ConfigureAwait(false);
            return ToolkitResult.Success(new { source, destination = dest, type = "file", size = new FileInfo(dest).Length });
        }
        return ToolkitResult.Fail($"Source not found: {source}");
    }

    private static async Task<ToolkitResult> UnzipAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var source = GetParam<string>(p, "source") ?? "";
        var dest = GetParam<string>(p, "destination") ?? Path.GetDirectoryName(source) ?? ".";
        var overwrite = GetParam<bool?>(p, "overwrite") ?? false;
        if (!File.Exists(source)) return ToolkitResult.Fail($"Archive not found: {source}");
        await Task.Run(() => ZipFile.ExtractToDirectory(source, dest, overwrite), ct).ConfigureAwait(false);
        var count = Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories).Count();
        return ToolkitResult.Success(new { source, destination = dest, extractedFiles = count });
    }

    private static ToolkitResult ListZip(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"Archive not found: {path}");
        using var archive = ZipFile.OpenRead(path);
        var entries = archive.Entries.Select(e => new
        {
            name = e.FullName,
            size = e.Length,
            compressedSize = e.CompressedLength,
            ratio = e.Length > 0 ? $"{(double)e.CompressedLength / e.Length:P1}" : "n/a",
            modified = e.LastWriteTime
        }).ToArray();
        return ToolkitResult.Success(new { path, entryCount = entries.Length, entries });
    }

    private static ToolkitResult AddEntry(Dictionary<string, object?> p)
    {
        var archivePath = GetParam<string>(p, "archive") ?? "";
        var file = GetParam<string>(p, "file") ?? "";
        var entryName = GetParam<string>(p, "entry_name") ?? Path.GetFileName(file);
        if (!File.Exists(archivePath)) return ToolkitResult.Fail($"Archive not found: {archivePath}");
        if (!File.Exists(file)) return ToolkitResult.Fail($"File not found: {file}");
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Update);
        archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
        return ToolkitResult.Success(new { archive = archivePath, file, entryName, added = true });
    }

    private static async Task<ToolkitResult> GzipCompressAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var source = GetParam<string>(p, "source") ?? "";
        var dest = GetParam<string>(p, "destination") ?? source + ".gz";
        if (!File.Exists(source)) return ToolkitResult.Fail($"File not found: {source}");
        await using var input = File.OpenRead(source);
        await using var output = File.Create(dest);
        await using var gzip = new GZipStream(output, CompressionLevel.Optimal);
        await input.CopyToAsync(gzip, ct).ConfigureAwait(false);
        var orig = new FileInfo(source).Length;
        var comp = new FileInfo(dest).Length;
        return ToolkitResult.Success(new { source, destination = dest, originalSize = orig, compressedSize = comp, ratio = $"{(double)comp / orig:P1}" });
    }

    private static async Task<ToolkitResult> GzipDecompressAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var source = GetParam<string>(p, "source") ?? "";
        var dest = GetParam<string>(p, "destination") ?? source.Replace(".gz", "", StringComparison.OrdinalIgnoreCase);
        if (!File.Exists(source)) return ToolkitResult.Fail($"File not found: {source}");
        await using var input = File.OpenRead(source);
        await using var gzip = new GZipStream(input, CompressionMode.Decompress);
        await using var output = File.Create(dest);
        await gzip.CopyToAsync(output, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source, destination = dest, size = new FileInfo(dest).Length });
    }
}

// ─── T-140: Image Processing ──────────────────────────────────────────────────

/// <summary>
/// Image processing via SixLabors.ImageSharp (cross-platform, no GDI+).
/// Actions: info, resize, crop, convert, grayscale, rotate, flip, thumbnail
/// </summary>
[Toolkit("image_processing", "Image Processing", ToolkitCategory.FileStorage)]
public sealed class ImageProcessingToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "image_processing";
    public override string Name => "Image Processing";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;
    public override bool IsConfigured => true;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct) => action switch
    {
        "info"       => Task.FromResult(ImageInfo(p)),
        "resize"     => ResizeAsync(p, ct),
        "crop"       => CropAsync(p, ct),
        "convert"    => ConvertAsync(p, ct),
        "grayscale"  => GrayscaleAsync(p, ct),
        "rotate"     => RotateAsync(p, ct),
        "flip"       => FlipAsync(p, ct),
        "thumbnail"  => ThumbnailAsync(p, ct),
        _ => Task.FromResult(ToolkitResult.Fail(
            $"Unknown action '{action}'. Valid: info, resize, crop, convert, grayscale, rotate, flip, thumbnail"))
    };

    private static ToolkitResult ImageInfo(Dictionary<string, object?> p)
    {
        var path = GetParam<string>(p, "path") ?? "";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        var info = SixLabors.ImageSharp.Image.Identify(path);
        if (info is null) return ToolkitResult.Fail($"Cannot identify image: {path}");
        return ToolkitResult.Success(new
        {
            path,
            width = info.Width,
            height = info.Height,
            format = info.Metadata.DecodedImageFormat?.Name ?? "unknown",
            frameCount = info.FrameMetadataCollection.Count,
            fileSize = new FileInfo(path).Length
        });
    }

    private static async Task<ToolkitResult> ResizeAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? path;
        var width = GetParam<int?>(p, "width") ?? 0;
        var height = GetParam<int?>(p, "height") ?? 0;
        if (width == 0 && height == 0) return ToolkitResult.Fail("'width' or 'height' required");
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        var options = new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(width > 0 ? width : img.Width, height > 0 ? height : img.Height) };
        img.Mutate(x => x.Resize(options));
        await img.SaveAsync(dest, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, width = img.Width, height = img.Height });
    }

    private static async Task<ToolkitResult> CropAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? path;
        var x = GetParam<int?>(p, "x") ?? 0;
        var y = GetParam<int?>(p, "y") ?? 0;
        var w = GetParam<int?>(p, "width") ?? 0;
        var h = GetParam<int?>(p, "height") ?? 0;
        if (w == 0 || h == 0) return ToolkitResult.Fail("'width' and 'height' required for crop");
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        img.Mutate(i => i.Crop(new Rectangle(x, y, w, h)));
        await img.SaveAsync(dest, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, cropX = x, cropY = y, width = w, height = h });
    }

    private static async Task<ToolkitResult> ConvertAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? "";
        var format = GetParam<string>(p, "format")?.ToLowerInvariant() ?? "jpeg";
        if (string.IsNullOrEmpty(dest)) return ToolkitResult.Fail("'destination' required");
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        IImageEncoder encoder = format switch
        {
            "png"  => new PngEncoder(),
            "webp" => new WebpEncoder(),
            _      => new JpegEncoder { Quality = GetParam<int?>(p, "quality") ?? 85 }
        };
        await img.SaveAsync(dest, encoder, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, format, size = new FileInfo(dest).Length });
    }

    private static async Task<ToolkitResult> GrayscaleAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? path;
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        img.Mutate(x => x.Grayscale());
        await img.SaveAsync(dest, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, grayscale = true });
    }

    private static async Task<ToolkitResult> RotateAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? path;
        var degrees = GetParam<float?>(p, "degrees") ?? 90f;
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        img.Mutate(x => x.Rotate(degrees));
        await img.SaveAsync(dest, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, degrees });
    }

    private static async Task<ToolkitResult> FlipAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? path;
        var mode = GetParam<string>(p, "mode")?.ToLowerInvariant() ?? "horizontal";
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        var flipMode = mode == "vertical" ? FlipMode.Vertical : FlipMode.Horizontal;
        img.Mutate(x => x.Flip(flipMode));
        await img.SaveAsync(dest, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, mode });
    }

    private static async Task<ToolkitResult> ThumbnailAsync(Dictionary<string, object?> p, CancellationToken ct)
    {
        var path = GetParam<string>(p, "path") ?? "";
        var dest = GetParam<string>(p, "destination") ?? Path.Combine(Path.GetDirectoryName(path) ?? ".", "thumb_" + Path.GetFileName(path));
        var size = GetParam<int?>(p, "size") ?? 128;
        if (!File.Exists(path)) return ToolkitResult.Fail($"File not found: {path}");
        using var img = await SixLabors.ImageSharp.Image.LoadAsync(path, ct).ConfigureAwait(false);
        img.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(size, size), Mode = ResizeMode.Crop }));
        await img.SaveAsync(dest, ct).ConfigureAwait(false);
        return ToolkitResult.Success(new { source = path, destination = dest, size, width = img.Width, height = img.Height });
    }
}

// ─── T-141: PDF ───────────────────────────────────────────────────────────────

/// <summary>
/// PDF toolkit via UglyToad.PdfPig.
/// Actions: extract_text, page_count, extract_page, metadata, search_text
/// </summary>
[Toolkit("pdf", "PDF", ToolkitCategory.FileStorage)]
public sealed class PdfToolkit(ToolkitConfig? cfg = null) : ToolkitBase(cfg)
{
    public override string Id => "pdf";
    public override string Name => "PDF";
    public override ToolkitCategory Category => ToolkitCategory.FileStorage;
    public override bool IsConfigured => true;

    protected override Task<ToolkitResult> ExecuteCoreAsync(
        string action, Dictionary<string, object?> p, CancellationToken ct)
    {
        var file = GetParam<string>(p, "file") ?? GetParam<string>(p, "path") ?? "";
        if (!File.Exists(file)) return Task.FromResult(ToolkitResult.Fail($"PDF not found: {file}"));
        return Task.Run(() => action switch
        {
            "extract_text" => ExtractText(file),
            "page_count"   => PageCount(file),
            "extract_page" => ExtractPage(file, GetParam<int?>(p, "page") ?? 1),
            "metadata"     => GetMetadata(file),
            "search_text"  => SearchText(file, GetParam<string>(p, "query") ?? ""),
            _ => ToolkitResult.Fail($"Unknown action '{action}'. Valid: extract_text, page_count, extract_page, metadata, search_text")
        }, ct);
    }

    private static ToolkitResult ExtractText(string file)
    {
        using var doc = PdfDocument.Open(file);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            foreach (var word in page.GetWords()) { sb.Append(word.Text); sb.Append(' '); }
            sb.AppendLine();
        }
        var text = sb.ToString().Trim();
        return ToolkitResult.Success(new { file, pageCount = doc.NumberOfPages, charCount = text.Length, text });
    }

    private static ToolkitResult PageCount(string file)
    {
        using var doc = PdfDocument.Open(file);
        return ToolkitResult.Success(new { file, pageCount = doc.NumberOfPages });
    }

    private static ToolkitResult ExtractPage(string file, int page)
    {
        using var doc = PdfDocument.Open(file);
        if (page < 1 || page > doc.NumberOfPages)
            return ToolkitResult.Fail($"Page {page} out of range (1–{doc.NumberOfPages})");
        var pg = doc.GetPage(page);
        var text = string.Join(" ", pg.GetWords().Select(w => w.Text));
        return ToolkitResult.Success(new { file, page, pageCount = doc.NumberOfPages, charCount = text.Length, text });
    }

    private static ToolkitResult GetMetadata(string file)
    {
        using var doc = PdfDocument.Open(file);
        var info = doc.Information;
        return ToolkitResult.Success(new
        {
            file,
            pageCount = doc.NumberOfPages,
            title     = info.Title,
            author    = info.Author,
            subject   = info.Subject,
            creator   = info.Creator,
            producer  = info.Producer,
            keywords  = info.Keywords,
            created   = info.CreationDate,
            modified  = info.ModifiedDate
        });
    }

    private static ToolkitResult SearchText(string file, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return ToolkitResult.Fail("'query' parameter required");
        using var doc = PdfDocument.Open(file);
        var matches = new List<object>();
        foreach (var page in doc.GetPages())
        {
            var text = string.Join(" ", page.GetWords().Select(w => w.Text));
            if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
                matches.Add(new { page = page.Number, preview = text[..Math.Min(200, text.Length)] });
        }
        return ToolkitResult.Success(new { file, query, matchCount = matches.Count, pages = matches });
    }
}
