// OpenClaw.Tools — CodeSearchTool
// Performs regex-based code search across files within a sandboxed root.
// Security: regex timeout prevents ReDoS; path traversal protection via ResolveSafe pattern.
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace OpenClaw.Tools;

/// <summary>
/// Searches source files in a directory tree using a regex pattern.
/// Returns file path, line number, and matched line content for each hit.
/// All path operations are confined to AllowedRoot.
/// </summary>
public sealed class CodeSearchTool : IAgxiomeExecutableTool
{
    // ReDoS mitigation: hard cap on regex evaluation per-line.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    // Sanity limits to avoid returning huge payloads.
    private const int MaxHits        = 200;
    private const int MaxFilesScanned = 1_000;

    // File extensions treated as text/source code (binary files are skipped).
    private static readonly IReadOnlySet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".fs", ".vb", ".ts", ".tsx", ".js", ".jsx",
        ".py", ".rb", ".go", ".java", ".kt", ".rs",
        ".json", ".yaml", ".yml", ".xml", ".toml", ".md",
        ".txt", ".sh", ".ps1", ".bat", ".sql",
    };

    public string Name => "code_search";
    public string Description => "Search source files with a regex pattern. Returns matching lines with file and line number.";
    public string? Version => "1.0";
    public IReadOnlyList<string> Categories => ["search", "codeanalysis"];

    /// <summary>Absolute, normalized root directory. All searches are confined here.</summary>
    public string AllowedRoot { get; }

    public CodeSearchTool(string allowedRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedRoot);
        AllowedRoot = Path.GetFullPath(allowedRoot);
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(ToolContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var p  = context.Parameters ?? [];

        var pattern    = p.TryGetValue("pattern",    out var pat) ? pat?.ToString() ?? "" : "";
        var startPath  = p.TryGetValue("path",       out var pt)  ? pt?.ToString()  ?? "." : ".";
        var extensions = p.TryGetValue("extensions", out var ext) ? ParseExtensions(ext?.ToString()) : null;
        var ignoreCase = p.TryGetValue("ignore_case", out var ic) && ic is true or "true" or "True";

        if (string.IsNullOrWhiteSpace(pattern))
            return new ToolResult(false, null, "Parameter 'pattern' is required.", sw.Elapsed);

        Regex regex;
        try
        {
            // Compile with timeout to mitigate ReDoS.
            var options = RegexOptions.Compiled | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            regex = new Regex(pattern, options, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            return new ToolResult(false, null, $"Invalid regex: {ex.Message}", sw.Elapsed);
        }

        var hits         = new List<object>();
        var filesScanned = 0;

        string searchRoot;
        try
        {
            // ResolveSafe throws UnauthorizedAccessException on path-traversal attempts.
            searchRoot = ResolveSafe(startPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ToolResult(false, null, $"Access denied (path traversal): {ex.Message}", sw.Elapsed);
        }

        try
        {
            var enumOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible    = true
            };

            foreach (var file in Directory.EnumerateFiles(searchRoot, "*", enumOptions))
            {
                ct.ThrowIfCancellationRequested();

                if (filesScanned++ >= MaxFilesScanned || hits.Count >= MaxHits)
                    break;

                var fileExt = Path.GetExtension(file);
                if (!IsTextFile(fileExt, extensions))
                    continue;

                await SearchFileAsync(file, regex, hits, ct);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ToolResult(false, null, $"Access denied: {ex.Message}", sw.Elapsed);
        }

        var relRoot = Path.GetRelativePath(AllowedRoot, searchRoot).Replace('\\', '/');
        return new ToolResult(true,
            new { root = relRoot, pattern, hits_found = hits.Count, files_scanned = filesScanned, hits },
            null, sw.Elapsed);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task SearchFileAsync(string filePath, Regex regex, List<object> hits, CancellationToken ct)
    {
        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(filePath, ct);
        }
        catch (IOException)
        {
            return; // skip unreadable files silently
        }

        var relPath = Path.GetRelativePath(AllowedRoot, filePath).Replace('\\', '/');

        for (var i = 0; i < lines.Length && hits.Count < MaxHits; i++)
        {
            bool matched;
            try
            {
                matched = regex.IsMatch(lines[i]);
            }
            catch (RegexMatchTimeoutException)
            {
                // Skip this line — the pattern timed out (ReDoS protection).
                continue;
            }

            if (matched)
            {
                hits.Add(new
                {
                    file    = relPath,
                    line    = i + 1,
                    content = lines[i].Trim()
                });
            }
        }
    }

    private bool IsTextFile(string extension, IReadOnlySet<string>? extensions)
    {
        if (extensions is { Count: > 0 })
            return extensions.Contains(extension);
        return TextExtensions.Contains(extension);
    }

    private static IReadOnlySet<string>? ParseExtensions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(e => e.StartsWith('.') ? e : '.' + e)
                  .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a user-supplied relative path inside AllowedRoot.
    /// Throws <see cref="UnauthorizedAccessException"/> on path-traversal attempts.
    /// </summary>
    private string ResolveSafe(string relativePath)
    {
        var combined   = Path.Combine(AllowedRoot, relativePath);
        var normalized = Path.GetFullPath(combined);
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
                ["required"]   = new JsonArray("pattern"),
                ["properties"] = new JsonObject
                {
                    ["pattern"]    = new JsonObject { ["type"] = "string", ["description"] = "Regex pattern to search for." },
                    ["path"]       = new JsonObject { ["type"] = "string", ["description"] = "Relative directory to search in. Defaults to root." },
                    ["extensions"] = new JsonObject { ["type"] = "string", ["description"] = "Comma-separated file extensions to include (e.g. '.cs,.ts')." },
                    ["ignore_case"] = new JsonObject { ["type"] = "boolean", ["description"] = "Case-insensitive match. Default: false." }
                }
            }
        };
    }
}
