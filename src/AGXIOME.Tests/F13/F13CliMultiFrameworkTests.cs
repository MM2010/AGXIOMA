// F13 — CLI & Multi-Framework Tests
// 43 tests covering MU-01..MU-10:
//   MU-01 RunCommand     (5 tests)
//   MU-02 ListCommand    (4 tests)
//   MU-03 SessionCommand (4 tests)
//   MU-04 AdminCommand   (4 tests)
//   MU-05 ConfigCommand  (4 tests)
//   MU-06 ControlPlane   (5 tests)
//   MU-07 ApprovalUI     (5 tests)
//   MU-08 LangChain      (4 tests)
//   MU-09 SemanticKernel (4 tests)
//   MU-10 Claude         (4 tests)

using AGXIOME.CLI.Commands;
using AGXIOME.Runtime.UI;
using AGXIOME.XLink.Bridges;
using NSubstitute;

namespace AGXIOME.Tests.F13;

// ═══════════════════════════════════════════════════════════════════════════════
// MU-01 — RunCommand
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class RunCommandTests
{
    private static (RunCommand cmd, IAgxiomeApiClient client, StringCliOutput output) Build(
        bool reachable = true, string apiResponse = "{\"runId\":\"r1\"}")
    {
        var client = Substitute.For<IAgxiomeApiClient>();
        client.IsReachable.Returns(reachable);
        client.PostAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
              .Returns(ValueTask.FromResult(apiResponse));
        var output = new StringCliOutput();
        return (new RunCommand(client, output), client, output);
    }

    [Fact]
    public async Task Execute_AgentRun_ReturnsSuccess()
    {
        var (cmd, _, out_) = Build();
        var result = await cmd.ExecuteAsync(["--type", "agent", "--id", "ag1", "--input", "hello"]);
        Assert.True(result.Success);
        Assert.Contains("runId", out_.OutText);
    }

    [Fact]
    public async Task Execute_MissingId_ReturnsFail()
    {
        var (cmd, _, _) = Build();
        var result = await cmd.ExecuteAsync(["--type", "agent"]);
        Assert.False(result.Success);
        Assert.Contains("--id", result.Error);
    }

    [Fact]
    public async Task Execute_MissingType_ReturnsFail()
    {
        var (cmd, _, _) = Build();
        var result = await cmd.ExecuteAsync(["--id", "ag1"]);
        Assert.False(result.Success);
        Assert.Contains("--type", result.Error);
    }

    [Fact]
    public async Task Execute_ServerUnreachable_ReturnsFail()
    {
        var (cmd, _, _) = Build(reachable: false);
        var result = await cmd.ExecuteAsync(["--type", "agent", "--id", "ag1", "--input", "x"]);
        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public async Task Execute_TeamRun_PostsToTeamCoordinate()
    {
        var (cmd, client, _) = Build();
        await cmd.ExecuteAsync(["--type", "team", "--id", "team-1", "--input", "go"]);
        await client.Received(1).PostAsync(Arg.Is<string>(p => p.Contains("team-1")), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-02 — ListCommand
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class ListCommandTests
{
    private static (ListCommand cmd, IAgxiomeApiClient client, StringCliOutput output) Build(
        bool reachable = true, string response = "[]")
    {
        var client = Substitute.For<IAgxiomeApiClient>();
        client.IsReachable.Returns(reachable);
        client.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns(ValueTask.FromResult(response));
        var output = new StringCliOutput();
        return (new ListCommand(client, output), client, output);
    }

    [Fact]
    public async Task Execute_DefaultType_ListsAgents()
    {
        var (cmd, client, _) = Build(response: "[{\"id\":\"ag1\"}]");
        var result = await cmd.ExecuteAsync([]);
        Assert.True(result.Success);
        await client.Received(1).GetAsync("api/agents", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_TypeTeams_ListsTeams()
    {
        var (cmd, client, _) = Build();
        await cmd.ExecuteAsync(["teams"]);
        await client.Received(1).GetAsync("api/teams", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UnknownType_ReturnsFail()
    {
        var (cmd, _, _) = Build();
        var result = await cmd.ExecuteAsync(["unknown_entity"]);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_ServerUnreachable_ReturnsFail()
    {
        var (cmd, _, _) = Build(reachable: false);
        var result = await cmd.ExecuteAsync(["agents"]);
        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-03 — SessionCommand
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class SessionCommandTests
{
    private static (SessionCommand cmd, IAgxiomeApiClient client, StringCliOutput output) Build(
        bool reachable = true)
    {
        var client = Substitute.For<IAgxiomeApiClient>();
        client.IsReachable.Returns(reachable);
        client.GetAsync(Arg.Any<string>(),    Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult("{\"sessions\":[]}"));
        client.PostAsync(Arg.Any<string>(),   Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult("{\"sessionId\":\"s1\"}"));
        client.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult("{\"deleted\":true}"));
        var output = new StringCliOutput();
        return (new SessionCommand(client, output), client, output);
    }

    [Fact]
    public async Task Execute_CreateSession_ReturnsSuccess()
    {
        var (cmd, _, _) = Build();
        var result = await cmd.ExecuteAsync(["--action", "create", "--agent", "ag1"]);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Execute_DeleteSession_CallsDelete()
    {
        var (cmd, client, _) = Build();
        await cmd.ExecuteAsync(["--action", "delete", "--id", "s1"]);
        await client.Received(1).DeleteAsync(Arg.Is<string>(p => p.Contains("s1")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_InspectSession_CallsGet()
    {
        var (cmd, client, _) = Build();
        await cmd.ExecuteAsync(["--action", "inspect", "--id", "s1"]);
        await client.Received(1).GetAsync(Arg.Is<string>(p => p.Contains("s1")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ServerUnreachable_ReturnsFail()
    {
        var (cmd, _, _) = Build(reachable: false);
        var result = await cmd.ExecuteAsync(["--action", "list"]);
        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-04 — AdminCommand
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class AdminCommandTests
{
    private static (AdminCommand cmd, IAgxiomeApiClient client) Build(bool reachable = true)
    {
        var client = Substitute.For<IAgxiomeApiClient>();
        client.IsReachable.Returns(reachable);
        client.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult("{\"status\":\"ok\"}"));
        return (new AdminCommand(client, new StringCliOutput()), client);
    }

    [Fact]
    public async Task Execute_HealthAction_GetsHealthEndpoint()
    {
        var (cmd, client) = Build();
        var result = await cmd.ExecuteAsync(["--action", "health"]);
        Assert.True(result.Success);
        await client.Received(1).GetAsync("api/admin/health", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MetricsAction_GetsMetricsEndpoint()
    {
        var (cmd, client) = Build();
        await cmd.ExecuteAsync(["--action", "metrics"]);
        await client.Received(1).GetAsync("api/admin/metrics", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_UnknownAction_ReturnsFail()
    {
        var (cmd, _) = Build();
        var result = await cmd.ExecuteAsync(["--action", "nope"]);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Execute_DefaultAction_Health()
    {
        var (cmd, client) = Build();
        await cmd.ExecuteAsync([]);
        await client.Received(1).GetAsync("api/admin/health", Arg.Any<CancellationToken>());
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-05 — ConfigCommand
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class ConfigCommandTests
{
    private static (ConfigCommand cmd, IAgxiomeApiClient client) Build(bool reachable = true)
    {
        var client = Substitute.For<IAgxiomeApiClient>();
        client.IsReachable.Returns(reachable);
        client.GetAsync(Arg.Any<string>(),  Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult("{\"log_level\":\"Info\"}"));
        client.PostAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult("{\"updated\":true}"));
        return (new ConfigCommand(client, new StringCliOutput()), client);
    }

    [Fact]
    public async Task Execute_GetAll_ReturnsConfig()
    {
        var (cmd, client) = Build();
        var result = await cmd.ExecuteAsync(["--action", "get"]);
        Assert.True(result.Success);
        await client.Received(1).GetAsync("api/admin/config", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GetKey_GetsSpecificKey()
    {
        var (cmd, client) = Build();
        await cmd.ExecuteAsync(["--action", "get", "--key", "log_level"]);
        await client.Received(1).GetAsync(Arg.Is<string>(p => p.Contains("log_level")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_SetKey_PostsConfig()
    {
        var (cmd, client) = Build();
        var result = await cmd.ExecuteAsync(["--action", "set", "--key", "log_level", "--value", "Warning"]);
        Assert.True(result.Success);
        await client.Received(1).PostAsync("api/admin/config", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ServerUnreachable_ReturnsFail()
    {
        var (cmd, _) = Build(reachable: false);
        var result = await cmd.ExecuteAsync(["--action", "get"]);
        Assert.False(result.Success);
        Assert.Equal(2, result.ExitCode);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-06 — ControlPlaneService
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class ControlPlaneServiceTests
{
    [Fact]
    public async Task GetDashboardSummary_EmptyStore_ReturnsZeroCounts()
    {
        var svc     = new ControlPlaneService();
        var summary = await svc.GetDashboardSummaryAsync();
        Assert.Equal(0, summary.TotalAgents);
        Assert.Equal(0, summary.ActiveRuns);
        Assert.NotEqual(default, summary.GeneratedAt);
    }

    [Fact]
    public async Task GetAgentStatuses_SeededAgents_ReturnsAll()
    {
        var svc = new ControlPlaneService();
        svc.SeedAgent(new AgentStatus("ag1", "Alpha", "Idle", 0, DateTimeOffset.UtcNow));
        svc.SeedAgent(new AgentStatus("ag2", "Beta",  "Running", 1, DateTimeOffset.UtcNow));
        var list = await svc.GetAgentStatusesAsync();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetActiveRuns_OnlyRunningOrPending_Filtered()
    {
        var svc = new ControlPlaneService();
        svc.SeedRun(new RunSummary("r1", "ag1", "Running",   DateTimeOffset.UtcNow, null));
        svc.SeedRun(new RunSummary("r2", "ag1", "Completed", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        svc.SeedRun(new RunSummary("r3", "ag2", "Pending",   DateTimeOffset.UtcNow, null));
        var active = await svc.GetActiveRunsAsync();
        Assert.Equal(2, active.Count);
        Assert.All(active, r => Assert.True(r.Status is "Running" or "Pending"));
    }

    [Fact]
    public async Task GetTeamStatuses_SeededTeam_ReturnsIt()
    {
        var svc = new ControlPlaneService();
        svc.SeedTeam(new TeamStatus("t1", "Core Team", ["ag1", "ag2"], 0));
        var teams = await svc.GetTeamStatusesAsync();
        Assert.Single(teams);
        Assert.Equal("Core Team", teams[0].Name);
    }

    [Fact]
    public async Task GetKnowledgeSummary_ReturnsSeededValues()
    {
        var svc = new ControlPlaneService();
        svc.SeedKnowledge(new KnowledgeSummary(3, 1500, 8000));
        var k = await svc.GetKnowledgeSummaryAsync();
        Assert.Equal(3,    k.CollectionCount);
        Assert.Equal(1500, k.TotalDocuments);
        Assert.Equal(8000, k.TotalChunks);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-07 — ApprovalService (HITL)
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class ApprovalServiceTests
{
    [Fact]
    public async Task GetPendingApprovals_NewRequest_IsPending()
    {
        var svc = new ApprovalService();
        svc.CreateRequest("ag1", "run-1", "delete_file", "Context: /tmp/x.txt");
        var pending = await svc.GetPendingApprovalsAsync();
        Assert.Single(pending);
        Assert.Equal(ApprovalStatus.Pending, pending[0].Status);
    }

    [Fact]
    public async Task Approve_ValidRequest_SetsApproved()
    {
        var svc = new ApprovalService();
        var req = svc.CreateRequest("ag1", "run-1", "deploy", "staging");
        var res = await svc.ApproveAsync(req.RequestId, "LGTM");
        Assert.True(res.Success);
        Assert.Equal(ApprovalStatus.Approved, res.NewStatus);
    }

    [Fact]
    public async Task Reject_ValidRequest_SetsRejected()
    {
        var svc = new ApprovalService();
        var req = svc.CreateRequest("ag2", "run-2", "drop_table", "prod db");
        var res = await svc.RejectAsync(req.RequestId, "Too risky");
        Assert.True(res.Success);
        Assert.Equal(ApprovalStatus.Rejected, res.NewStatus);
    }

    [Fact]
    public async Task Approve_NonExistentId_ReturnsFail()
    {
        var svc = new ApprovalService();
        var res = await svc.ApproveAsync("nonexistent");
        Assert.False(res.Success);
    }

    [Fact]
    public async Task GetApprovalHistory_AfterApprove_ExcludesPending()
    {
        var svc = new ApprovalService();
        var req = svc.CreateRequest("ag1", "run-1", "read", "safe");
        await svc.ApproveAsync(req.RequestId);

        var history = await svc.GetApprovalHistoryAsync();
        var pending = await svc.GetPendingApprovalsAsync();
        Assert.Single(history);
        Assert.Empty(pending);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-08 — LangChainBridge
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class LangChainBridgeTests
{
    [Fact]
    public void IsAvailable_NoEndpoint_False()
    {
        // Ensure env variable not set in test isolation
        Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", null);
        var bridge = new LangChainBridge(endpointUrl: null);
        Assert.False(bridge.IsAvailable);
    }

    [Fact]
    public void IsAvailable_WithEndpoint_True()
    {
        var bridge = new LangChainBridge(endpointUrl: "http://localhost:8001");
        Assert.True(bridge.IsAvailable);
    }

    [Fact]
    public async Task RunChain_NotAvailable_ReturnsFail()
    {
        Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", null);
        var bridge = new LangChainBridge(endpointUrl: null);
        var result = await bridge.RunChainAsync("my-chain", new() { ["question"] = "hi" });
        Assert.False(result.Success);
        Assert.Equal("LangChain", result.FrameworkName);
    }

    [Fact]
    public async Task ListChains_NotAvailable_ReturnsEmpty()
    {
        Environment.SetEnvironmentVariable("LANGCHAIN_ENDPOINT", null);
        var bridge = new LangChainBridge(endpointUrl: null);
        var chains = await bridge.ListChainsAsync();
        Assert.Empty(chains);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-09 — SemanticKernelBridge
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class SemanticKernelBridgeTests
{
    [Fact]
    public void IsAvailable_NoAdapter_False()
    {
        var bridge = new SemanticKernelBridge(null);
        Assert.False(bridge.IsAvailable);
    }

    [Fact]
    public void IsAvailable_WithAdapter_True()
    {
        var bridge = new SemanticKernelBridge(new NullSemanticKernelAdapter());
        Assert.True(bridge.IsAvailable);
    }

    [Fact]
    public async Task InvokePlugin_WithAdapter_ReturnsSuccess()
    {
        var adapter = new NullSemanticKernelAdapter(new()
        {
            [("WriterPlugin", "Brainstorm")] = "Idea: write a poem"
        });
        var bridge = new SemanticKernelBridge(adapter);
        var result = await bridge.InvokePluginAsync("WriterPlugin", "Brainstorm", new() { ["topic"] = "AI" });
        Assert.True(result.Success);
        Assert.Contains("poem", result.Content);
    }

    [Fact]
    public async Task InvokePlugin_NoAdapter_ReturnsFail()
    {
        var bridge = new SemanticKernelBridge(null);
        var result = await bridge.InvokePluginAsync("P", "F", []);
        Assert.False(result.Success);
        Assert.Equal("SemanticKernel", result.FrameworkName);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MU-10 — ClaudeBridge
// ═══════════════════════════════════════════════════════════════════════════════

public sealed class ClaudeBridgeTests
{
    [Fact]
    public void IsAvailable_NoApiKey_False()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        var bridge = new ClaudeBridge(apiKey: null);
        Assert.False(bridge.IsAvailable);
    }

    [Fact]
    public void IsAvailable_WithApiKey_True()
    {
        // API key provided explicitly (env cleared)
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        var bridge = new ClaudeBridge(apiKey: "sk-ant-test-key");
        Assert.True(bridge.IsAvailable);
    }

    [Fact]
    public async Task Complete_NoApiKey_ReturnsFail()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        var bridge = new ClaudeBridge(apiKey: null);
        var result = await bridge.CompleteAsync("Hello");
        Assert.False(result.Success);
        Assert.Equal("Claude", result.FrameworkName);
    }

    [Fact]
    public void SupportedModels_ContainsKnownModels()
    {
        var bridge = new ClaudeBridge(apiKey: "sk-ant-x");
        Assert.Contains("claude-3-haiku-20240307", bridge.SupportedModels);
        Assert.Contains("claude-3-opus-20240229",  bridge.SupportedModels);
    }
}
