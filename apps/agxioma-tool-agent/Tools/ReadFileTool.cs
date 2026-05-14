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

    public override string Name        => "read_file";
    public override string Description =>
        "Read the contents of a local file. Supported types: .txt, .md, .json, .csv, .yaml, .yml, .xml, .log. Max 512 KB.";

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

        var ext = Path.GetExtension(path);
        if (!AllowedExtensions.Contains(ext))
            return Fail($"Extension '{ext}' is not allowed. Supported: {string.Join(", ", AllowedExtensions)}");

        if (!File.Exists(path))
            return Fail($"File not found: {path}");

        var info = new FileInfo(path);
        if (info.Length > MaxFileSizeBytes)
            return Fail($"File too large ({info.Length / 1024} KB). Max allowed: {MaxFileSizeBytes / 1024} KB.");

        // ── Read ───────────────────────────────────────────────────────────────
        string content;
        if (maxLines > 0)
        {
            var lines = await File.ReadAllLinesAsync(path, ct);
            content = string.Join(Environment.NewLine, lines.Take(maxLines));
        }
        else
        {
            content = await File.ReadAllTextAsync(path, ct);
        }

        var data = new
        {
            path      = Path.GetFullPath(path),
            fileName  = Path.GetFileName(path),
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
}
