extern alias toolagent;
extern alias skilllab;

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;
using AGXIOME.DynamicRuntime.HotFolder;
using AGXIOME.Shared.Models;
using ReadFileTool = toolagent::AgxiomaToolAgent.Tools.ReadFileTool;
using RoslynCompiler = skilllab::AgxiomaDynamicSkillLab.Engine.RoslynCompiler;
using SandboxExecutor = skilllab::AgxiomaDynamicSkillLab.Engine.SandboxExecutor;
using SkillPromotionPolicy = skilllab::AgxiomaDynamicSkillLab.Engine.SkillPromotionPolicy;

namespace AGXIOME.Tests;

public sealed class ReadFileToolSecurityTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "agxiome-security-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }

    [Fact]
    public async Task Rejects_RelativeTraversal_OutsideConfiguredRoot()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_tempPath, "outside")).FullName;
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "top-secret");

        var tool = new ReadFileTool(root);
        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = "..\\outside\\secret.txt"
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("outside the allowed root", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_AbsolutePath_OutsideConfiguredRoot()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_tempPath, "outside")).FullName;
        var outsideFile = Path.Combine(outside, "secret.txt");
        await File.WriteAllTextAsync(outsideFile, "top-secret");

        var tool = new ReadFileTool(root);
        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = outsideFile
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("outside the allowed root", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_File_LargerThanLimit()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var largeFile = Path.Combine(root, "large.txt");
        await File.WriteAllTextAsync(largeFile, new string('a', 513 * 1024));

        var tool = new ReadFileTool(root);
        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = "large.txt"
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("File too large", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_Unc_Path_Input()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var tool = new ReadFileTool(root);

        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = "\\\\server\\share\\secret.txt"
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("UNC paths", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_AlternateDataStream_Path_Input()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var tool = new ReadFileTool(root);
        var candidate = Path.Combine(root, "allowed.txt:secret");

        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = candidate
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Alternate data stream", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_Path_With_Control_Characters()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var tool = new ReadFileTool(root);

        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = "allowed.txt\nsecond-line"
        }), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("control characters", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Allows_File_InsideConfiguredRoot()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        await File.WriteAllTextAsync(Path.Combine(root, "allowed.txt"), "line-1\nline-2");

        var tool = new ReadFileTool(root);
        var result = await tool.ExecuteAsync(CreateContext(new Dictionary<string, object>
        {
            ["file_path"] = "allowed.txt",
            ["max_lines"] = 1
        }), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("line-1", GetAnonymousProperty<string>(result.Data!, "content"));
    }

    private static ToolContext CreateContext(Dictionary<string, object> parameters) =>
        new("agent-security", "run-security", Parameters: parameters);

    private static T GetAnonymousProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found.");
        return (T)property.GetValue(instance)!;
    }
}

public sealed class AgentFileScannerSecurityTests : IDisposable
{
    private readonly string _tempPath = Path.Combine(Path.GetTempPath(), "agxiome-scanner-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
            Directory.Delete(_tempPath, recursive: true);
    }

    [Fact]
    public async Task Ignores_NonCs_Files()
    {
        var filePath = Path.Combine(CreateRoot(), "agent.txt");
        await File.WriteAllTextAsync(filePath, "[AgxiomeAgent(\"demo\", \"desc\")]");

        var result = await AgentFileScanner.ScanAsync(filePath);

        Assert.Null(result);
    }

    [Fact]
    public async Task Rejects_Oversized_SourceFiles()
    {
        var filePath = Path.Combine(CreateRoot(), "agent.cs");
        await File.WriteAllTextAsync(filePath, new string('a', 257 * 1024), Encoding.UTF8);

        var result = await AgentFileScanner.ScanAsync(filePath);

        Assert.Null(result);
    }

    [Fact]
    public async Task Ignores_Attributes_NotAppliedToClasses()
    {
        var filePath = Path.Combine(CreateRoot(), "agent.cs");
        await File.WriteAllTextAsync(filePath,
            "public class Demo { [AgxiomeAgent(\"demo\", \"desc\")] public void Run() { } }");

        var result = await AgentFileScanner.ScanAsync(filePath);

        Assert.Null(result);
    }

    [Fact]
    public async Task Accepts_ValidAgentClass_AndExtractsMetadata()
    {
        var filePath = Path.Combine(CreateRoot(), "agent.cs");
        await File.WriteAllTextAsync(filePath,
            "using AGXIOME.DynamicRuntime.Attributes; [AgxiomeAgent(\"demo\", \"desc\")] public sealed class DemoAgent { }");

        var result = await AgentFileScanner.ScanAsync(filePath);

        Assert.NotNull(result);
        Assert.Equal("demo", result!.AgentId);
        Assert.Equal("desc", result.Description);
        Assert.Equal(filePath, result.FilePath);
        Assert.False(string.IsNullOrWhiteSpace(result.SourceHash));
    }

    [Fact]
    public async Task Rejects_File_Outside_AllowedRoot()
    {
        var root = Directory.CreateDirectory(Path.Combine(_tempPath, "root")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_tempPath, "outside")).FullName;
        var filePath = Path.Combine(outside, "agent.cs");
        await File.WriteAllTextAsync(filePath,
            "using AGXIOME.DynamicRuntime.Attributes; [AgxiomeAgent(\"demo\", \"desc\")] public sealed class DemoAgent { }");

        var result = await AgentFileScanner.ScanAsync(filePath, root);

        Assert.Null(result);
    }

    private string CreateRoot() => Directory.CreateDirectory(_tempPath).FullName;
}

public sealed class RoslynCompilerSecurityTests
{
    [Fact]
    public async Task Rejects_Compilation_WhenRuntimeMutationDisabled()
    {
        var compiler = new RoslynCompiler(runtimeMutationEnabled: false);

        var result = await compiler.CompileAsync(ValidAgentSource, "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("disabled by runtime policy", string.Join(";", result.Errors ?? []), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_ForbiddenNamespaceImport()
    {
        var compiler = new RoslynCompiler(runtimeMutationEnabled: true);
        const string source = """
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "demo";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult(File.ReadAllText(input));
}
""";

        var result = await compiler.CompileAsync(source, "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Forbidden namespace import", string.Join(";", result.Errors ?? []), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_ForbiddenApiUsage()
    {
        var compiler = new RoslynCompiler(runtimeMutationEnabled: true);
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "demo";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult(Environment.MachineName);
}
""";

        var result = await compiler.CompileAsync(source, "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Forbidden API usage", string.Join(";", result.Errors ?? []), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_ForbiddenNamespaceAliasImport()
    {
        var compiler = new RoslynCompiler(runtimeMutationEnabled: true);
        const string source = """
using IO = System.IO;
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "demo";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult(IO.Path.GetFileName(input));
}
""";

        var result = await compiler.CompileAsync(source, "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Forbidden namespace import", string.Join(";", result.Errors ?? []), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_GlobalReflectionAccess()
    {
        var compiler = new RoslynCompiler(runtimeMutationEnabled: true);
        const string source = """
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "demo";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult(global::System.Reflection.Assembly.GetExecutingAssembly().FullName ?? string.Empty);
}
""";

        var result = await compiler.CompileAsync(source, "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Forbidden API usage", string.Join(";", result.Errors ?? []), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Allows_Minimal_Agent_WhenPolicySatisfied()
    {
        var compiler = new RoslynCompiler(runtimeMutationEnabled: true);

        var result = await compiler.CompileAsync(ValidAgentSource, "demo", CancellationToken.None);

        Assert.True(result.Success, string.Join(" | ", result.Errors ?? []));
        Assert.NotNull(result.Instance);
        Assert.Equal("demo", result.Instance!.AgentId);
        result.Context?.Unload();
    }

    private const string ValidAgentSource = """
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "demo";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult("echo:" + input);
}
""";
}

public sealed class SandboxExecutorSecurityTests
{
    [Fact]
    public async Task Rejects_Execution_WhenRuntimeMutationDisabled()
    {
        var executor = new SandboxExecutor(runtimeMutationEnabled: false);

        var result = await executor.ExecuteAsync(new TestDynamicAgent("demo", _ => Task.FromResult("ok")), "input", "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("disabled by runtime policy", string.Join(";", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_ToolIdentityMismatch()
    {
        var executor = new SandboxExecutor(runtimeMutationEnabled: true);

        var result = await executor.ExecuteAsync(new TestDynamicAgent("other", _ => Task.FromResult("ok")), "input", "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("identity mismatch", string.Join(";", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_Input_AboveConfiguredLimit()
    {
        var executor = new SandboxExecutor(runtimeMutationEnabled: true, maxInputChars: 8);

        var result = await executor.ExecuteAsync(new TestDynamicAgent("demo", _ => Task.FromResult("ok")), "input-too-long", "demo", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("larger than 8 characters", string.Join(";", result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Truncates_Output_AboveConfiguredLimit()
    {
        var executor = new SandboxExecutor(runtimeMutationEnabled: true, maxOutputChars: 5);

        var result = await executor.ExecuteAsync(new TestDynamicAgent("demo", _ => Task.FromResult("123456789")), "input", "demo", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("12345", result.Output);
    }

    [Fact]
    public async Task Serializes_Concurrent_Executions_ToConfiguredLimit()
    {
        var executor = new SandboxExecutor(runtimeMutationEnabled: true, maxConcurrentExecutions: 1);
        var activeCount = 0;
        var maxObserved = 0;

        var agent = new TestDynamicAgent("demo", async _ =>
        {
            var current = Interlocked.Increment(ref activeCount);
            while (true)
            {
                var snapshot = maxObserved;
                if (current <= snapshot || Interlocked.CompareExchange(ref maxObserved, current, snapshot) == snapshot)
                    break;
            }

            await Task.Delay(75);
            Interlocked.Decrement(ref activeCount);
            return "ok";
        });

        var first = executor.ExecuteAsync(agent, "one", "demo", CancellationToken.None);
        var second = executor.ExecuteAsync(agent, "two", "demo", CancellationToken.None);

        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.Success));
        Assert.Equal(1, maxObserved);
    }

    [Fact]
    public async Task Rejects_Oversized_Input_Without_Blocking_Valid_Concurrent_Run()
    {
        var executor = new SandboxExecutor(runtimeMutationEnabled: true, maxInputChars: 4, maxConcurrentExecutions: 1);
        var agent = new TestDynamicAgent("demo", async input =>
        {
            await Task.Delay(50);
            return "ok:" + input;
        });

        var validTask = executor.ExecuteAsync(agent, "ok", "demo", CancellationToken.None);
        var invalidTask = executor.ExecuteAsync(agent, "input-too-long", "demo", CancellationToken.None);

        var validResult = await validTask;
        var invalidResult = await invalidTask;

        Assert.True(validResult.Success);
        Assert.False(invalidResult.Success);
        Assert.Contains("larger than 4 characters", string.Join(";", invalidResult.Errors), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestDynamicAgent(string agentId, Func<string, Task<string>> onRun) : ISimpleDynamicAgent
    {
        public string AgentId => agentId;
        public string Description => "test-agent";

        public Task<string> RunAsync(string input, CancellationToken ct = default) => onRun(input);
    }
}

public sealed class SkillPromotionPolicyTests
{
    [Fact]
    public void Rejects_NonExecuted_Tool_ForPromotion()
    {
        var tool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Compiled, CreateManifestJson());

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Contains(errors, error => error.Contains("Only executed tools can be promoted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Manifest_With_Escalated_Privileges()
    {
        var tool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed, CreateManifestJson(filesystem: "readwrite:any", network: true, tools: ["shell"]));

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Contains(errors, error => error.Contains("Filesystem permission exceeds", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Network access", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("Tool chaining", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Malformed_Manifest_Json()
    {
        var tool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed, "{not-json");

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Contains(errors, error => error.Contains("not valid JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Manifest_Id_Mismatch()
    {
        var tool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed, CreateManifestJson(id: "other"));

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Contains(errors, error => error.Contains("manifest id must match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Execution_Budget_OutsidePolicyBounds()
    {
        var zeroBudgetTool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed, CreateManifestJson(maxExecutionMs: 0, maxMemoryMb: 0));
        var highBudgetTool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed, CreateManifestJson(maxExecutionMs: 10_001, maxMemoryMb: 257));

        var zeroErrors = SkillPromotionPolicy.Validate(zeroBudgetTool);
        var highErrors = SkillPromotionPolicy.Validate(highBudgetTool);

        Assert.Contains(zeroErrors, error => error.Contains("MaxExecutionMs must be between 1 and 10000", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(zeroErrors, error => error.Contains("MaxMemoryMb must be between 1 and 256", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(highErrors, error => error.Contains("MaxExecutionMs must be between 1 and 10000", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(highErrors, error => error.Contains("MaxMemoryMb must be between 1 and 256", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Source_AgentId_Mismatch_EvenWhenManifestMatches()
    {
        var tool = CreateTool(
            skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed,
            CreateManifestJson(),
            sourceCode: CreateValidSource("other"));

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Contains(errors, error => error.Contains("Source AgentId must match the tool id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_Forbidden_Source_EvenWhenManifestLooksSafe()
    {
        var tool = CreateTool(
            skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed,
            CreateManifestJson(),
            sourceCode: """
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "demo";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult(File.ReadAllText(input));
}
""");

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Contains(errors, error => error.Contains("Forbidden namespace import", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Accepts_Executed_Tool_With_Strict_Manifest()
    {
        var tool = CreateTool(skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed, CreateManifestJson());

        var errors = SkillPromotionPolicy.Validate(tool);

        Assert.Empty(errors);
    }

    private static skilllab::AgxiomaDynamicSkillLab.ToolDescriptor CreateTool(
        skilllab::AgxiomaDynamicSkillLab.ToolStatus status,
        string manifestJson,
        string? sourceCode = null) =>
        new(
            Id: "demo",
            Name: "Demo",
            Description: "desc",
            SourceCode: sourceCode ?? CreateValidSource("demo"),
            ManifestJson: manifestJson,
            CreatedAt: DateTime.UtcNow,
            Status: status);

    private static string CreateManifestJson(
        string id = "demo",
        string filesystem = "readonly:generated",
        bool network = false,
        List<string>? tools = null,
        int maxExecutionMs = 10_000,
        int maxMemoryMb = 256) =>
        System.Text.Json.JsonSerializer.Serialize(new skilllab::AgxiomaDynamicSkillLab.PermissionManifest(
            Id: id,
            Version: "1.0.0",
            Permissions: new skilllab::AgxiomaDynamicSkillLab.PermissionRules(
                Filesystem: filesystem,
                Network: network,
                Tools: tools ?? [],
                MaxExecutionMs: maxExecutionMs,
                MaxMemoryMb: maxMemoryMb)));

    private static string CreateValidSource(string agentId) => $$"""
using System.Threading;
using System.Threading.Tasks;
using AGXIOME.DynamicRuntime.Interfaces;

public sealed class DemoAgent : ISimpleDynamicAgent
{
    public string AgentId => "{{agentId}}";
    public string Description => "desc";
    public Task<string> RunAsync(string input, CancellationToken ct = default)
        => Task.FromResult("echo:" + input);
}
""";
}