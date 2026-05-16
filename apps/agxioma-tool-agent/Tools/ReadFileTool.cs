using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AgxiomaToolAgent.Tools;

/// <summary>
/// Reads the content of a local file (text, JSON, CSV, Markdown).
/// Enforces size limits and restricts to allowed extensions for safety.
/// </summary>
public sealed class ReadFileTool : AgxiomeFunction
{
    private const long MaxFileSizeBytes = 512 * 1024; // 512 KB
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".json", ".csv", ".yaml", ".yml", ".xml", ".log" };
    private readonly string _allowedRoot;

    public ReadFileTool(string allowedRoot)
    {
        _allowedRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(allowedRoot)
            ? Directory.GetCurrentDirectory()
            : allowedRoot);
    }

    public override string Name        => "read_file";
    public override string Description =>
        "Read the contents of a local file under the configured tool root. Supported types: .txt, .md, .json, .csv, .yaml, .yml, .xml, .log. Max 512 KB.";

    [ToolParameter(Required = true, Description = "Absolute or relative path to the file.")]
    public string FilePath { get; set; } = string.Empty;

    [ToolParameter(Required = false, Description = "Maximum number of lines to return (default: all lines).", Type = "integer")]
    public int MaxLines { get; set; } = 0;

    public override async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var path    = GetParam(context, "file_path", FilePath);
        var maxLines = GetParam(context, "max_lines", MaxLines);

        // ── Safety checks ──────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(path))
            return Fail("file_path is required.");

        if (ContainsUnsafePathCharacters(path))
            return Fail("file_path contains unsupported control characters.");

        if (IsUncPath(path))
            return Fail("UNC paths are not allowed.");

        var fullPath = ResolvePath(path);
        if (IsAlternateDataStreamPath(fullPath))
            return Fail("Alternate data stream paths are not allowed.");

        if (!IsUnderAllowedRoot(fullPath))
            return Fail($"Path '{path}' is outside the allowed root '{_allowedRoot}'.");

        if (ContainsReparsePoint(fullPath))
            return Fail("Paths containing reparse points are not allowed.");

        var ext = Path.GetExtension(fullPath);
        if (!AllowedExtensions.Contains(ext))
            return Fail($"Extension '{ext}' is not allowed. Supported: {string.Join(", ", AllowedExtensions)}");

        if (!File.Exists(fullPath))
            return Fail($"File not found: {path}");

        var info = new FileInfo(fullPath);
        if (info.Length > MaxFileSizeBytes)
            return Fail($"File too large ({info.Length / 1024} KB). Max allowed: {MaxFileSizeBytes / 1024} KB.");

        // ── Read ───────────────────────────────────────────────────────────────
        string content;
        if (maxLines > 0)
        {
            var lines = await File.ReadAllLinesAsync(fullPath, ct);
            content = string.Join(Environment.NewLine, lines.Take(maxLines));
        }
        else
        {
            content = await File.ReadAllTextAsync(fullPath, ct);
        }

        var data = new
        {
            path      = Path.GetRelativePath(_allowedRoot, fullPath).Replace('\\', '/'),
            fileName  = Path.GetFileName(fullPath),
            extension = ext,
            sizeBytes = info.Length,
            lineCount = content.Split('\n').Length,
            content
        };

        return new ToolResult(
            Success: true,
            Data: data,
            Metadata: new ToolMetadata(Name, Version, BytesProcessed: info.Length, ItemsProcessed: 1)
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ToolResult Fail(string msg) =>
        new(Success: false, Data: null, ErrorMessage: msg);

    private static T GetParam<T>(ToolContext ctx, string key, T fallback)
    {
        if (ctx.Parameters != null && ctx.Parameters.TryGetValue(key, out var raw))
        {
            try { return (T)Convert.ChangeType(raw, typeof(T)); }
            catch { return fallback; }
        }
        return fallback;
    }

    private string ResolvePath(string path) =>
        Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(_allowedRoot, path));

    private bool IsUnderAllowedRoot(string fullPath)
    {
        var root = EnsureTrailingSeparator(_allowedRoot);
        var candidate = EnsureTrailingSeparator(Path.GetDirectoryName(fullPath) ?? fullPath);
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, _allowedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static bool ContainsUnsafePathCharacters(string path) =>
        path.IndexOfAny(['\r', '\n', '\0']) >= 0;

    private static bool IsUncPath(string path) =>
        path.StartsWith("\\\\", StringComparison.Ordinal);

    private static bool IsAlternateDataStreamPath(string fullPath)
    {
        var rootLength = Path.GetPathRoot(fullPath)?.Length ?? 0;
        return fullPath.AsSpan(rootLength).Contains(':');
    }

    private static bool ContainsReparsePoint(string fullPath)
    {
        var candidate = File.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        while (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
        {
            var info = new DirectoryInfo(candidate);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;

            candidate = info.Parent?.FullName;
        }

        if (File.Exists(fullPath))
        {
            var info = new FileInfo(fullPath);
            return (info.Attributes & FileAttributes.ReparsePoint) != 0;
        }

        return false;
    }
}
