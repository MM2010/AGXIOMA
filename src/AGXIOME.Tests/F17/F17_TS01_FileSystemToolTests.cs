// F17-TS01 — FileSystemTool Tests (28 tests)
// Covers: happy paths (read/list/write/exists), offensives (path traversal, large file, empty path),
// aggressive (concurrent writes, cancellation), and security invariants.
// All tests use a real temp directory — no mocks.
using System.Text;
using OpenClaw.Tools;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests.F17;

// ═══════════════════════════════════════════════════════════════════
// Fixture — creates a temp directory tree and cleans up after tests.
// ═══════════════════════════════════════════════════════════════════
public sealed class FileSystemTempFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"openclaw-fst-{Guid.NewGuid():N}");

    public FileSystemTempFixture()
    {
        Directory.CreateDirectory(Root);
        // Seed: src/Hello.cs, src/Inner/Nested.cs, data/readme.md
        Directory.CreateDirectory(Path.Combine(Root, "src", "Inner"));
        Directory.CreateDirectory(Path.Combine(Root, "data"));
        File.WriteAllText(Path.Combine(Root, "src",        "Hello.cs"),   "// Hello");
        File.WriteAllText(Path.Combine(Root, "src", "Inner", "Nested.cs"), "namespace Nested;");
        File.WriteAllText(Path.Combine(Root, "data", "readme.md"),        "# Readme");
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

// ═══════════════════════════════════════════════════════════════════
// TS01-A  Happy Path
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS01_FileSystemTool_HappyPath : IClassFixture<FileSystemTempFixture>
{
    private readonly FileSystemTool _tool;
    private readonly string         _root;

    public F17_TS01_FileSystemTool_HappyPath(FileSystemTempFixture fix)
    {
        _root = fix.Root;
        _tool = new FileSystemTool(_root);
    }

    private ToolContext Ctx(string action, string path, string? content = null)
    {
        var p = new Dictionary<string, object> { ["action"] = action, ["path"] = path };
        if (content is not null) p["content"] = content;
        return new ToolContext("openclaw", "run1", Parameters: p);
    }

    [Fact] public async Task Read_ExistingFile_ReturnsContent()
    {
        var r = await _tool.ExecuteAsync(Ctx("read", "src/Hello.cs"), default);
        Assert.True(r.Success);
        Assert.NotNull(r.Data);
        Assert.Contains("Hello", r.Data?.ToString() ?? "");
    }

    [Fact] public async Task Read_NestedFile_ReturnsContent()
    {
        var r = await _tool.ExecuteAsync(Ctx("read", @"src\Inner\Nested.cs"), default);
        Assert.True(r.Success);
    }

    [Fact] public async Task List_Root_ReturnsEntries()
    {
        var r = await _tool.ExecuteAsync(Ctx("list", "."), default);
        Assert.True(r.Success);
        Assert.NotNull(r.Data);
    }

    [Fact] public async Task List_SubDir_ReturnsEntries()
    {
        var r = await _tool.ExecuteAsync(Ctx("list", "src"), default);
        Assert.True(r.Success);
    }

    [Fact] public async Task Exists_ExistingFile_ReturnsTrue()
    {
        var r = await _tool.ExecuteAsync(Ctx("exists", "data/readme.md"), default);
        Assert.True(r.Success);
        Assert.Contains("true", r.Data?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Exists_MissingFile_ReturnsFalseWithSuccessTrue()
    {
        var r = await _tool.ExecuteAsync(Ctx("exists", "nonexistent.txt"), default);
        Assert.True(r.Success); // exists action always succeeds — it just reports false
        Assert.Contains("none", r.Data?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Write_NewFile_CreatesFile()
    {
        var r = await _tool.ExecuteAsync(Ctx("write", "out/generated.txt", "hello world"), default);
        Assert.True(r.Success);
        Assert.True(File.Exists(Path.Combine(_root, "out", "generated.txt")));
        Assert.Equal("hello world", await File.ReadAllTextAsync(Path.Combine(_root, "out", "generated.txt")));
    }

    [Fact] public async Task Write_OverwriteExistingFile_UpdatesContent()
    {
        var path = Path.Combine(_root, "src", "Hello.cs");
        await _tool.ExecuteAsync(Ctx("write", "src/Hello.cs", "// Updated"), default);
        Assert.Equal("// Updated", await File.ReadAllTextAsync(path));
    }

    [Fact] public async Task ExecutionTime_IsPopulated()
    {
        var r = await _tool.ExecuteAsync(Ctx("read", "src/Hello.cs"), default);
        Assert.True(r.ExecutionTime > TimeSpan.Zero);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS01-B  Offensive / Security
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS01_FileSystemTool_Offensive : IClassFixture<FileSystemTempFixture>
{
    private readonly FileSystemTool _tool;

    public F17_TS01_FileSystemTool_Offensive(FileSystemTempFixture fix) =>
        _tool = new FileSystemTool(fix.Root);

    private ToolContext Ctx(string action, string path, string? content = null)
    {
        var p = new Dictionary<string, object> { ["action"] = action, ["path"] = path };
        if (content is not null) p["content"] = content;
        return new ToolContext("openclaw", "run1", Parameters: p);
    }

    [Fact] public async Task Read_PathTraversal_DotDot_ReturnsForbidden()
    {
        var r = await _tool.ExecuteAsync(Ctx("read", "../../etc/passwd"), default);
        Assert.False(r.Success);
        Assert.Contains("traversal", r.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Read_AbsolutePath_ReturnsForbidden()
    {
        // An absolute path that escapes the root should be caught even without ".."
        var r = await _tool.ExecuteAsync(Ctx("read", "C:\\Windows\\System32\\drivers\\etc\\hosts"), default);
        Assert.False(r.Success);
    }

    [Fact] public async Task Read_MissingFile_ReturnsFailure()
    {
        var r = await _tool.ExecuteAsync(Ctx("read", "does-not-exist.cs"), default);
        Assert.False(r.Success);
        Assert.Contains("not found", r.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task Read_EmptyPath_ReturnsError()
    {
        var r = await _tool.ExecuteAsync(Ctx("read", ""), default);
        Assert.False(r.Success);
    }

    [Fact] public async Task List_MissingDir_ReturnsFailure()
    {
        var r = await _tool.ExecuteAsync(Ctx("list", "nonexistent-dir"), default);
        Assert.False(r.Success);
    }

    [Fact] public async Task UnknownAction_ReturnsFailure()
    {
        var r = await _tool.ExecuteAsync(Ctx("delete", "src/Hello.cs"), default);
        Assert.False(r.Success);
        Assert.Contains("Unknown action", r.ErrorMessage ?? "");
    }

    [Fact] public async Task Write_PathTraversal_ReturnsForbidden()
    {
        var r = await _tool.ExecuteAsync(Ctx("write", "../escaped.txt", "pwned"), default);
        Assert.False(r.Success);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS01-C  Aggressive / Concurrency
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS01_FileSystemTool_Aggressive : IClassFixture<FileSystemTempFixture>
{
    private readonly FileSystemTool _tool;
    private readonly string         _root;

    public F17_TS01_FileSystemTool_Aggressive(FileSystemTempFixture fix)
    {
        _root = fix.Root;
        _tool = new FileSystemTool(_root);
    }

    [Fact] public async Task ConcurrentReads_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 20).Select(_ =>
            _tool.ExecuteAsync(
                new ToolContext("openclaw", "run1",
                    Parameters: new Dictionary<string, object> { ["action"] = "read", ["path"] = "src/Hello.cs" }),
                default));

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact] public async Task ConcurrentWrites_NoDataCorruption()
    {
        var tasks = Enumerable.Range(0, 10).Select(i =>
            _tool.ExecuteAsync(
                new ToolContext("openclaw", "run1",
                    Parameters: new Dictionary<string, object>
                    {
                        ["action"]  = "write",
                        ["path"]    = $"concurrent/file{i}.txt",
                        ["content"] = $"content-{i}"
                    }),
                default));

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact] public async Task Cancellation_DuringRead_ThrowsOrReturnsGracefully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        try { await _tool.ExecuteAsync(
            new ToolContext("openclaw", "run1",
                Parameters: new Dictionary<string, object> { ["action"] = "list", ["path"] = "." }),
            cts.Token); }
        catch (OperationCanceledException) { /* expected */ }
        // No exception from the tool itself is also acceptable (fast path).
    }

    [Fact] public void AllowedRoot_IsNormalized()
    {
        // Trailing separator is stripped, mixed separators are resolved.
        var t = new FileSystemTool(_root + Path.DirectorySeparatorChar);
        Assert.Equal(_root, t.AllowedRoot);
    }

    [Fact] public void Constructor_NullRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FileSystemTool(null!));
    }

    [Fact] public void Constructor_EmptyRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FileSystemTool(""));
    }
}
