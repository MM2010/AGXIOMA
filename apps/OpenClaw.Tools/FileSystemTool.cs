// OpenClaw.Tools — FileSystemTool
// Provides read, list, and write operations with strict path-traversal protection.
// Depends only on AGXIOME.Shared (no higher-level toolkits required).
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace OpenClaw.Tools;

/// <summary>
/// Reads, lists, and writes files within a sandboxed root directory.
/// Security: all paths are normalized and verified to remain inside AllowedRoot.
/// </summary>
public sealed class FileSystemTool : IAgxiomeExecutableTool
{
    // Maximum file size for read operations — prevents accidental OOM on huge binaries.
    private const long MaxReadBytes = 2 * 1024 * 1024; // 2 MB

    // Maximum number of entries returned by list operations.
    private const int MaxListEntries = 500;

    public string Name => "file_system";
    public string Description => "Read, list, and write files. All paths must be relative to AllowedRoot.";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories => ["filesystem", "codeanalysis"];

    /// <summary>Absolute, normalized root directory. All file operations are confined here.</summary>
    public string AllowedRoot { get; }

    public FileSystemTool(string allowedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedRoot);
        // Strip trailing separator so normalisation is idempotent (Path.GetFullPath preserves it).
        AllowedRoot = Path.GetFullPath(allowedRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p = context.Parameters ?? [];

        var action = p.TryGetValue("action", out var a) ? a?.ToString() ?? "" : "";
        var path   = p.TryGetValue("path",   out var pt) ? pt?.ToString() ?? "" : "";

        try
        {
            var result = action switch
            {
                "read"  => await ReadAsync(path, ct),
                "list"  => List(path),
                "write" => await WriteAsync(path, p, ct),
                "exists"=> Exists(path),
                _       => new ToolResult(false, null, $"Unknown action '{action}'. Supported: read, list, write, exists.")
            };
            return result with { ExecutionTime = sw.Elapsed };
        }
        catch (UnauthorizedAccessException ex)
        {
            // Surfaces path-traversal attempts to caller without leaking internal stack.
            return new ToolResult(false, null, $"Access denied: {ex.Message}", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, null, $"FileSystemTool error: {ex.Message}", sw.Elapsed);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<ToolResult> ReadAsync(string relativePath, CancellationToken ct)
    {
        var full = ResolveSafe(relativePath);
        if (!File.Exists(full))
            return new ToolResult(false, null, $"File not found: {relativePath}");

        var info = new FileInfo(full);
        if (info.Length > MaxReadBytes)
            return new ToolResult(false, null, $"File too large ({info.Length:N0} bytes). Max: {MaxReadBytes:N0}.");

        var content = await File.ReadAllTextAsync(full, ct);
        return new ToolResult(true, new { path = relativePath, content, size = info.Length });
    }

    private ToolResult List(string relativePath)
    {
        var full = ResolveSafe(relativePath);
        if (!Directory.Exists(full))
            return new ToolResult(false, null, $"Directory not found: {relativePath}");

        var entries = Directory
            .EnumerateFileSystemEntries(full)
            .Take(MaxListEntries)
            .Select(e =>
            {
                var rel = Path.GetRelativePath(AllowedRoot, e).Replace('\\', '/');
                return new { path = rel, type = Directory.Exists(e) ? "dir" : "file" };
            })
            .ToArray();

        return new ToolResult(true, new { path = relativePath, entries });
    }

    private async Task<ToolResult> WriteAsync(string relativePath, Dictionary<string, object> p, CancellationToken ct)
    {
        var full    = ResolveSafe(relativePath);
        var content = p.TryGetValue("content", out var c) ? c?.ToString() ?? "" : "";

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, content, ct);
        return new ToolResult(true, new { path = relativePath, written = content.Length });
    }

    private ToolResult Exists(string relativePath)
    {
        var full   = ResolveSafe(relativePath);
        var isFile = File.Exists(full);
        var isDir  = Directory.Exists(full);
        return new ToolResult(true, new { path = relativePath, exists = isFile || isDir, type = isFile ? "file" : isDir ? "dir" : "none" });
    }

    /// <summary>
    /// Resolves a user-supplied relative path inside AllowedRoot.
    /// Throws <see cref="UnauthorizedAccessException"/> on path-traversal attempts.
    /// </summary>
    private string ResolveSafe(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Path must not be empty.");

        // Combine with root, then fully normalize to collapse '..' segments.
        var combined  = Path.Combine(AllowedRoot, relativePath);
        var normalized = Path.GetFullPath(combined);

        // Ensure the resolved path is still within AllowedRoot.
        var rootWithSep = AllowedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!normalized.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalized, AllowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException(
                $"Path traversal attempt detected. Path '{relativePath}' resolves outside the allowed root.");
        }

        return normalized;
    }

    /// <inheritdoc />
    public JsonObject ToJsonSchema()
    {
        return new JsonObject
        {
            ["name"]        = Name,
            ["description"] = Description,
            ["parameters"]  = new JsonObject
            {
                ["type"]       = "object",
                ["required"]   = new JsonArray("action", "path"),
                ["properties"] = new JsonObject
                {
                    ["action"]  = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("read", "list", "write", "exists") },
                    ["path"]    = new JsonObject { ["type"] = "string",  ["description"] = "Relative path within the allowed root." },
                    ["content"] = new JsonObject { ["type"] = "string",  ["description"] = "Content to write (required for 'write' action)." }
                }
            }
        };
    }
}

// Extension on ToolResult for cleaner error construction (avoids repetition).
file static class ToolResultExt
{
    internal static ToolResult Failure(string message) =>
        new(false, null, message);

    internal static ToolResult Success(object data) =>
        new(true, data);
}
