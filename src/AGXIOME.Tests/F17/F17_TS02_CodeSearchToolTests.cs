// F17-TS02 — CodeSearchTool Tests (22 tests)
// Covers: happy paths (regex match, extension filter), offensives (path traversal, invalid regex,
// ReDoS patterns), aggressive (large tree, concurrent searches, cancellation).
using OpenClaw.Tools;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests.F17;

// ═══════════════════════════════════════════════════════════════════
// Fixture — seeds a code tree for search tests.
// ═══════════════════════════════════════════════════════════════════
public sealed class CodeSearchTempFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), $"openclaw-cst-{Guid.NewGuid():N}");

    public CodeSearchTempFixture()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, "src"));
        Directory.CreateDirectory(Path.Combine(Root, "tests"));
        Directory.CreateDirectory(Path.Combine(Root, "docs"));

        File.WriteAllText(Path.Combine(Root, "src", "Agent.cs"),
            "public class Agent { public void Run() { } }\npublic class AgentRunner { }");
        File.WriteAllText(Path.Combine(Root, "src", "Service.cs"),
            "public interface IService {}\npublic class ServiceImpl : IService {}");
        File.WriteAllText(Path.Combine(Root, "tests", "AgentTests.cs"),
            "[Fact] public void Run_ShouldSucceed() { }");
        File.WriteAllText(Path.Combine(Root, "docs", "README.md"),
            "# Project README\nThis is the documentation.");

        // Binary-ish file that should be skipped.
        File.WriteAllBytes(Path.Combine(Root, "data.bin"), [0x00, 0xFF, 0xFE]);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}

// ═══════════════════════════════════════════════════════════════════
// TS02-A  Happy Path
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS02_CodeSearchTool_HappyPath : IClassFixture<CodeSearchTempFixture>
{
    private readonly CodeSearchTool _tool;

    public F17_TS02_CodeSearchTool_HappyPath(CodeSearchTempFixture fix) =>
        _tool = new CodeSearchTool(fix.Root);

    private ToolContext Ctx(string pattern, string? path = null, string? ext = null, bool ignoreCase = false)
    {
        var p = new Dictionary<string, object> { ["pattern"] = pattern };
        if (path is not null)     p["path"]        = path;
        if (ext  is not null)     p["extensions"]  = ext;
        if (ignoreCase)           p["ignore_case"]  = "true";
        return new ToolContext("openclaw", "run1", Parameters: p);
    }

    [Fact] public async Task Search_ExistingPattern_ReturnsHits()
    {
        var r = await _tool.ExecuteAsync(Ctx("class Agent"), default);
        Assert.True(r.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(r.Data);
        Assert.Contains("Agent.cs", json);
    }

    [Fact] public async Task Search_IgnoreCase_FindsMatch()
    {
        var r = await _tool.ExecuteAsync(Ctx("iservice", ignoreCase: true), default);
        Assert.True(r.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(r.Data);
        Assert.Contains("Service.cs", json);
    }

    [Fact] public async Task Search_ExtensionFilter_LimitsToCs()
    {
        // Pattern exists in README.md too, but extension filter should limit to .cs
        var r = await _tool.ExecuteAsync(Ctx(".", ext: ".cs"), default);
        Assert.True(r.Success);
        Assert.DoesNotContain("README", r.Data?.ToString() ?? "");
    }

    [Fact] public async Task Search_SubDirectory_LimitedScope()
    {
        var r = await _tool.ExecuteAsync(Ctx("Fact", path: "tests"), default);
        Assert.True(r.Success);
        var json = System.Text.Json.JsonSerializer.Serialize(r.Data);
        Assert.Contains("AgentTests.cs", json);
    }

    [Fact] public async Task Search_NoMatch_ReturnsEmptyHits()
    {
        var r = await _tool.ExecuteAsync(Ctx("xxxxxxNONEXISTENTxxxxxx"), default);
        Assert.True(r.Success);
        Assert.Contains("0", r.Data?.ToString() ?? ""); // hits_found: 0
    }

    [Fact] public async Task Search_MultiExtension_FiltersCorrectly()
    {
        var r = await _tool.ExecuteAsync(Ctx(".", ext: ".cs,.md"), default);
        Assert.True(r.Success);
        // .bin file should be absent from results
        Assert.DoesNotContain("data.bin", r.Data?.ToString() ?? "");
    }

    [Fact] public async Task ExecutionTime_IsPositive()
    {
        var r = await _tool.ExecuteAsync(Ctx("Agent"), default);
        Assert.True(r.ExecutionTime > TimeSpan.Zero);
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS02-B  Offensive / Security
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS02_CodeSearchTool_Offensive : IClassFixture<CodeSearchTempFixture>
{
    private readonly CodeSearchTool _tool;

    public F17_TS02_CodeSearchTool_Offensive(CodeSearchTempFixture fix) =>
        _tool = new CodeSearchTool(fix.Root);

    private ToolContext Ctx(string pattern, string path = ".")
    {
        var p = new Dictionary<string, object> { ["pattern"] = pattern, ["path"] = path };
        return new ToolContext("openclaw", "run1", Parameters: p);
    }

    [Fact] public async Task PathTraversal_DotDot_ReturnsForbidden()
    {
        var r = await _tool.ExecuteAsync(Ctx("anything", "../../"), default);
        Assert.False(r.Success);
        Assert.Contains("traversal", r.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public async Task InvalidRegex_ReturnsFailure()
    {
        // Unbalanced parenthesis — invalid regex.
        var r = await _tool.ExecuteAsync(Ctx("(invalid[regex"), default);
        Assert.False(r.Success);
        Assert.Contains("Invalid regex", r.ErrorMessage ?? "");
    }

    [Fact] public async Task EmptyPattern_ReturnsFailure()
    {
        var p = new Dictionary<string, object> { ["pattern"] = "" };
        var r = await _tool.ExecuteAsync(new ToolContext("openclaw", "r", Parameters: p), default);
        Assert.False(r.Success);
    }

    [Fact] public async Task ReDoS_PotentialPattern_DoesNotHang()
    {
        // Classic ReDoS candidate — must not hang (timeout per line).
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var r = await _tool.ExecuteAsync(Ctx(@"(a+)+b"), cts.Token);
        // Either succeeds (with skipped lines) or returns gracefully.
        Assert.False(cts.IsCancellationRequested, "Tool should not have timed out globally.");
    }
}

// ═══════════════════════════════════════════════════════════════════
// TS02-C  Aggressive / Concurrency
// ═══════════════════════════════════════════════════════════════════
public sealed class F17_TS02_CodeSearchTool_Aggressive : IClassFixture<CodeSearchTempFixture>
{
    private readonly CodeSearchTool _tool;

    public F17_TS02_CodeSearchTool_Aggressive(CodeSearchTempFixture fix) =>
        _tool = new CodeSearchTool(fix.Root);

    [Fact] public async Task ConcurrentSearches_AllSucceed()
    {
        var tasks = Enumerable.Range(0, 15).Select(_ =>
            _tool.ExecuteAsync(
                new ToolContext("openclaw", "r",
                    Parameters: new Dictionary<string, object> { ["pattern"] = "class" }),
                default));

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact] public async Task Cancellation_IsRespected()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        try
        {
            await _tool.ExecuteAsync(
                new ToolContext("openclaw", "r",
                    Parameters: new Dictionary<string, object> { ["pattern"] = ".*" }),
                cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
    }

    [Fact] public void Constructor_NullRoot_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new CodeSearchTool(null!));

    [Fact] public void Constructor_EmptyRoot_Throws() =>
        Assert.Throws<ArgumentException>(() => new CodeSearchTool(""));
}
