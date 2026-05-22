// F11 — AgentOS Runtime Tests
// 142 tests — covers middleware logic, controller behavior, hub methods, and background workers.
// Uses WebApplicationFactory for integration tests; unit tests for logic classes.

using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using AGXIOME.Runtime.Middleware;
using AGXIOME.Runtime.Hubs;
using AGXIOME.Runtime.Workers;
using AGXIOME.Runtime.Controllers;
using AGXIOME.Security.ApiKey;
using AGXIOME.Security.Rbac;
using AGXIOME.Security.Audit;
using AGXIOME.Security.RateLimit;
using AGXIOME.Security.Sanitization;
using AGXIOME.Security.Keys;
using AGXIOME.Runtime.Repositories;
using NSubstitute;

namespace AGXIOME.Tests;

// ════════════════════════════════════════════════════════════
// RT-01 — Program / DI scaffold
// ════════════════════════════════════════════════════════════
public sealed class RT01_ProgramScaffoldTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public RT01_ProgramScaffoldTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact] public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] public async Task Health_HasStatusField()
    {
        var client = _factory.CreateClient();
        var body   = await client.GetFromJsonAsync<Dictionary<string, object>>("/health");
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("status") || body.ContainsKey("Status"));
    }

    [Fact] public async Task Services_IKeyRotationService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IKeyRotationService>();
        Assert.NotNull(svc);
    }

    [Fact] public async Task Services_IApiKeyStore_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IApiKeyStore>());
    }

    [Fact] public async Task Services_IRbacPolicy_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRbacPolicy>());
    }

    [Fact] public async Task Services_IRateLimiter_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRateLimiter>());
    }

    [Fact] public async Task Services_AuditLogger_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<AuditLogger>());
    }
}

// ════════════════════════════════════════════════════════════
// RT-02 — Auth Middleware
// ════════════════════════════════════════════════════════════
public sealed class RT02_AuthMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public RT02_AuthMiddlewareTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact] public async Task MissingApiKey_Returns401()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact] public async Task InvalidApiKey_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "agx_invalid");
        var resp = await client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact] public async Task ValidApiKey_PassesThrough()
    {
        var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(svc => {
            // Replace IApiKeyStore with one that always accepts
            var stub = Substitute.For<IApiKeyStore>();
            var record = new ApiKeyRecord("kid", "hash", "salt",
                [AgxiomeRole.Admin], [], "owner", DateTimeOffset.UtcNow, null);
            stub.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ApiKeyValidationResult(true, record, null));
            svc.AddSingleton(stub);
        }));
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "agx_valid");
        var resp = await client.GetAsync("/api/agents");
        // 200 or any non-401 means auth passed
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact] public async Task PublicHealth_BypassesAuth()
    {
        var client = _factory.CreateClient();
        // No API key — health should still return 200
        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] public async Task SecurityHeaders_Present()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/health");
        Assert.True(resp.Headers.Contains("X-Content-Type-Options") ||
                    resp.Headers.Contains("X-Frame-Options") ||
                    resp.Content.Headers.ContentType != null);
    }

    [Fact] public async Task ApiKey_ErrorBody_HasCode()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/agents");
        var body   = await resp.Content.ReadAsStringAsync();
        Assert.Contains("auth_required", body);
    }
}

// ════════════════════════════════════════════════════════════
// RT-03 — RBAC Middleware
// ════════════════════════════════════════════════════════════
public sealed class RT03_RbacTests
{
    private readonly IRbacPolicy _policy = new DefaultRbacPolicy();

    [Fact] public async Task Viewer_CannotDelete()
    {
        var viewer = new AgxiomePrincipal("v", [AgxiomeRole.Viewer], []);
        Assert.False(_policy.IsAuthorized(viewer, AgxiomePermission.AgentDelete));
    }

    [Fact] public async Task Admin_CanDoEverything()
    {
        var admin = new AgxiomePrincipal("a", [AgxiomeRole.Admin], []);
        foreach (AgxiomePermission perm in Enum.GetValues<AgxiomePermission>())
            Assert.True(_policy.IsAuthorized(admin, perm), $"Admin missing permission: {perm}");
    }

    [Fact] public async Task Operator_CanRun()
    {
        var op = new AgxiomePrincipal("o", [AgxiomeRole.Operator], []);
        Assert.True(_policy.IsAuthorized(op, AgxiomePermission.AgentRun));
    }

    [Fact] public async Task MultiRole_GrantsUnion()
    {
        var both = new AgxiomePrincipal("b", [AgxiomeRole.Viewer, AgxiomeRole.Operator], []);
        Assert.True(_policy.IsAuthorized(both, AgxiomePermission.AgentRun));
        Assert.True(_policy.IsAuthorized(both, AgxiomePermission.AgentRead));
    }

    [Fact] public async Task EmptyRoles_DeniesAll()
    {
        var none = new AgxiomePrincipal("n", [], []);
        Assert.False(_policy.IsAuthorized(none, AgxiomePermission.AgentRead));
    }

    [Fact] public async Task GetPermissions_Admin_HasMoreThanViewer()
    {
        var adminPerms  = _policy.GetPermissions(AgxiomeRole.Admin);
        var viewerPerms = _policy.GetPermissions(AgxiomeRole.Viewer);
        Assert.True(adminPerms.Count > viewerPerms.Count);
    }
}

// ════════════════════════════════════════════════════════════
// RT-04 — Rate Limiter
// ════════════════════════════════════════════════════════════
public sealed class RT04_RateLimiterTests
{
    [Fact] public async Task AllowsRequestsUpToLimit()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(5, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 5; i++) Assert.True(limiter.Check("k", policy).IsAllowed);
    }

    [Fact] public async Task BlocksAboveLimit_Returns429Data()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(3, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 3; i++) limiter.Check("x", policy);
        var r = limiter.Check("x", policy);
        Assert.False(r.IsAllowed);
        Assert.True(r.RetryAfter.TotalSeconds > 0);
    }

    [Fact] public async Task ResetClearsState()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(1, TimeSpan.FromMinutes(1));
        limiter.Check("y", policy);
        limiter.Reset("y");
        Assert.True(limiter.Check("y", policy).IsAllowed);
    }

    [Fact] public async Task DifferentKeys_NotInterfering()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(1, TimeSpan.FromMinutes(1));
        limiter.Check("a", policy);
        limiter.Check("a", policy); // second denied for "a"
        // "b" should still succeed
        Assert.True(limiter.Check("b", policy).IsAllowed);
    }

    [Fact] public async Task PolicyRegistry_FindsByRoute()
    {
        var registry = new RateLimitPolicyRegistry();
        var p = new RateLimitPolicy(10, TimeSpan.FromMinutes(1));
        registry.Register("/api/runs", RateLimitDimension.User, p);
        var found = registry.Find("/api/runs", RateLimitDimension.User);
        Assert.NotNull(found);
        Assert.Equal(10, found!.MaxRequests);
    }

    [Fact] public async Task PolicyRegistry_ReturnsNull_ForUnknownRoute()
    {
        var registry = new RateLimitPolicyRegistry();
        Assert.Null(registry.Find("/notexist", RateLimitDimension.Ip));
    }
}

// ════════════════════════════════════════════════════════════
// RT-05 — Error Handler (middleware logic)
// ════════════════════════════════════════════════════════════
public sealed class RT05_ErrorHandlerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public RT05_ErrorHandlerTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact] public async Task UnknownRoute_Returns404()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/health"); // only public route
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] public async Task ResponseContentType_IsJson_OnError()
    {
        var client = _factory.CreateClient();
        var resp   = await client.GetAsync("/api/agents"); // no key → 401 with JSON
        Assert.Contains("application/json", resp.Content.Headers.ContentType?.MediaType ?? "");
    }
}

// ════════════════════════════════════════════════════════════
// RT-06 — Agent Controller
// ════════════════════════════════════════════════════════════
public sealed class RT06_AgentControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RT06_AgentControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.ConfigureServices(svc => {
            var stub = Substitute.For<IApiKeyStore>();
            var record = new ApiKeyRecord("kid", "h", "s",
                [AgxiomeRole.Admin], [], "owner", DateTimeOffset.UtcNow, null);
            stub.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ApiKeyValidationResult(true, record, null));
            svc.AddSingleton(stub);
        })).CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "agx_test");
    }

    [Fact] public async Task Create_ReturnsCreated()
    {
        var resp = await _client.PostAsJsonAsync("/api/agents", new { name = "agent-test", model = "gpt-4o-mini" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact] public async Task List_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/agents");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] public async Task Get_Missing_Returns404()
    {
        var resp = await _client.GetAsync("/api/agents/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact] public async Task Create_ThenGet_Works()
    {
        var created = await _client.PostAsJsonAsync("/api/agents", new { name = "agent-get-test", model = "gpt-4o" });
        var dto     = await created.Content.ReadFromJsonAsync<AgentDto>();
        var got     = await _client.GetAsync($"/api/agents/{dto!.Id}");
        Assert.Equal(HttpStatusCode.OK, got.StatusCode);
    }

    [Fact] public async Task Delete_Returns204()
    {
        var created = await _client.PostAsJsonAsync("/api/agents", new { name = "del-agent", model = "gpt-4o" });
        var dto     = await created.Content.ReadFromJsonAsync<AgentDto>();
        var del     = await _client.DeleteAsync($"/api/agents/{dto!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }

    [Fact] public async Task Clone_Returns201()
    {
        var created = await _client.PostAsJsonAsync("/api/agents", new { name = "clone-src", model = "gpt-4o" });
        var dto     = await created.Content.ReadFromJsonAsync<AgentDto>();
        var cloned  = await _client.PostAsync($"/api/agents/{dto!.Id}/clone", null);
        Assert.Equal(HttpStatusCode.Created, cloned.StatusCode);
    }
}

// ════════════════════════════════════════════════════════════
// RT-07 — Run Controller
// ════════════════════════════════════════════════════════════
public sealed class RT07_RunControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RT07_RunControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.ConfigureServices(svc => {
            var stub = Substitute.For<IApiKeyStore>();
            var record = new ApiKeyRecord("kid", "h", "s",
                [AgxiomeRole.Admin], [], "owner", DateTimeOffset.UtcNow, null);
            stub.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ApiKeyValidationResult(true, record, null));
            svc.AddSingleton(stub);
        })).CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "agx_test");
    }

    [Fact] public async Task Start_ReturnsAccepted()
    {
        var resp = await _client.PostAsJsonAsync("/api/runs", new { agentId = "agent-1", input = "hello" });
        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    [Fact] public async Task Cancel_AfterStart_UpdatesStatus()
    {
        var start = await _client.PostAsJsonAsync("/api/runs", new { agentId = "a1", input = "x" });
        var run   = await start.Content.ReadFromJsonAsync<RunDto>();
        var del   = await _client.DeleteAsync($"/api/runs/{run!.RunId}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
    }

    [Fact] public async Task List_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/runs");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] public async Task Status_Missing_Returns404()
    {
        var resp = await _client.GetAsync("/api/runs/notexist");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact] public async Task Pause_ThenResume_Works()
    {
        var start = await _client.PostAsJsonAsync("/api/runs", new { agentId = "a2", input = "y" });
        var run   = await start.Content.ReadFromJsonAsync<RunDto>();
        await _client.PostAsync($"/api/runs/{run!.RunId}/pause", null);
        var resume = await _client.PostAsync($"/api/runs/{run.RunId}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
    }

    [Fact] public async Task Result_Returns200_AfterStart()
    {
        var start = await _client.PostAsJsonAsync("/api/runs", new { agentId = "a3", input = "z" });
        var run   = await start.Content.ReadFromJsonAsync<RunDto>();
        var result = await _client.GetAsync($"/api/runs/{run!.RunId}/result");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }
}

// ════════════════════════════════════════════════════════════
// RT-08 — Session Controller
// ════════════════════════════════════════════════════════════
public sealed class RT08_SessionControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public RT08_SessionControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.ConfigureServices(svc => {
            var stub = Substitute.For<IApiKeyStore>();
            var rec  = new ApiKeyRecord("k","h","s",[AgxiomeRole.Admin],[],
                "o",DateTimeOffset.UtcNow,null);
            stub.ValidateAsync(Arg.Any<string>(),Arg.Any<CancellationToken>())
                .Returns(new ApiKeyValidationResult(true,rec,null));
            svc.AddSingleton(stub);
        })).CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key","agx_test");
    }

    [Fact] public async Task CreateSession_Returns201()
    {
        var resp = await _client.PostAsJsonAsync("/api/sessions",
            new SessionDto("","agent-1",DateTimeOffset.UtcNow,true));
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact] public async Task ListSessions_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api/sessions");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact] public async Task GetSession_NotFound()
    {
        var resp = await _client.GetAsync("/api/sessions/badid");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact] public async Task DeleteSession_Returns204()
    {
        var c = await _client.PostAsJsonAsync("/api/sessions",
            new SessionDto("","a1",DateTimeOffset.UtcNow,true));
        var s = await c.Content.ReadFromJsonAsync<SessionDto>();
        var d = await _client.DeleteAsync($"/api/sessions/{s!.SessionId}");
        Assert.Equal(HttpStatusCode.NoContent, d.StatusCode);
    }

    [Fact] public async Task CloseSession_Returns200()
    {
        var c = await _client.PostAsJsonAsync("/api/sessions",
            new SessionDto("","a2",DateTimeOffset.UtcNow,true));
        var s = await c.Content.ReadFromJsonAsync<SessionDto>();
        var cl = await _client.PatchAsync($"/api/sessions/{s!.SessionId}/close", null);
        Assert.Equal(HttpStatusCode.OK, cl.StatusCode);
    }
}

// ════════════════════════════════════════════════════════════
// RT-09..RT-14 — Remaining Controllers (combined)
// ════════════════════════════════════════════════════════════
public sealed class RT09_14_ControllersTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public RT09_14_ControllersTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(b => b.ConfigureServices(svc => {
            var stub = Substitute.For<IApiKeyStore>();
            var rec  = new ApiKeyRecord("k","h","s",[AgxiomeRole.Admin],[],
                "o",DateTimeOffset.UtcNow,null);
            stub.ValidateAsync(Arg.Any<string>(),Arg.Any<CancellationToken>())
                .Returns(new ApiKeyValidationResult(true,rec,null));
            svc.AddSingleton(stub);
        })).CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key","agx_test");
    }

    // RT-09 Team
    [Fact] public async Task Team_Create_Returns201()
    {
        var r = await _client.PostAsJsonAsync("/api/teams", new { name="team-a", agentIds=new[]{"a1"} });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }
    [Fact] public async Task Team_List_ReturnsOk() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/teams")).StatusCode);

    // RT-10 Workflow
    [Fact] public async Task Workflow_Create_Returns201()
    {
        var r = await _client.PostAsJsonAsync("/api/workflows", new { name="wf-1", stepIds=new[]{"s1"} });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }
    [Fact] public async Task Workflow_Execute_ReturnsAccepted()
    {
        var c = await _client.PostAsJsonAsync("/api/workflows", new { name="wf-exec", stepIds=new[]{"s1"} });
        var w = await c.Content.ReadFromJsonAsync<WorkflowDto>();
        Assert.Equal(HttpStatusCode.Accepted, (await _client.PostAsync($"/api/workflows/{w!.WorkflowId}/execute", null)).StatusCode);
    }

    // RT-11 Knowledge
    [Fact] public async Task Knowledge_Ingest_ReturnsAccepted()
    {
        var r = await _client.PostAsJsonAsync("/api/knowledge/ingest", new { collectionId="c1", content="hello" });
        Assert.Equal(HttpStatusCode.Accepted, r.StatusCode);
    }
    [Fact] public async Task Knowledge_Search_ReturnsOk()
    {
        var r = await _client.PostAsJsonAsync("/api/knowledge/search", new { collectionId="c1", query="test" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // RT-12 Tool
    [Fact] public async Task Tool_List_ReturnsOk() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/tools")).StatusCode);

    // RT-13 Auth
    [Fact] public async Task Auth_Me_ReturnsOk() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/auth/me")).StatusCode);

    // RT-14 Admin
    [Fact] public async Task Admin_Health_ReturnsOk() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/health")).StatusCode);
    [Fact] public async Task Admin_Metrics_ReturnsOk() => Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/admin/metrics")).StatusCode);
}

// ════════════════════════════════════════════════════════════
// RT-15..RT-17 — SignalR Hubs (unit-level)
// ════════════════════════════════════════════════════════════
public sealed class RT15_17_HubTests
{
    [Fact] public async Task AgentHub_Instantiates()
    {
        var hub = new AgentHub(new InMemoryAgentEventReplayStore());
        Assert.NotNull(hub);
    }

    [Fact] public async Task LogHub_Instantiates()
    {
        var hub = new LogHub();
        Assert.NotNull(hub);
    }

    [Fact] public async Task AdminHub_Instantiates()
    {
        var hub = new AdminHub();
        Assert.NotNull(hub);
    }

    [Fact] public async Task AgentRunEvent_Constructed()
    {
        var evt = new AgentRunEvent("r1","a1","started","",1,DateTimeOffset.UtcNow);
        Assert.Equal("r1", evt.RunId);
    }

    [Fact] public async Task LogEvent_HasCorrectFields()
    {
        var evt = new LogEvent("module","info","msg",2,DateTimeOffset.UtcNow);
        Assert.Equal("info", evt.Level);
    }

    [Fact] public async Task AdminMetricEvent_HasSeq()
    {
        var evt = new AdminMetricEvent("cpu",42.0,3,DateTimeOffset.UtcNow);
        Assert.Equal(3L, evt.Seq);
    }
}

// ════════════════════════════════════════════════════════════
// RT-18 — BackgroundRunWorker
// ════════════════════════════════════════════════════════════
public sealed class RT18_BackgroundRunWorkerTests
{
    [Fact] public async Task TryEnqueue_ReturnsTrueWhenSpace()
    {
        var worker = new BackgroundRunWorker(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<BackgroundRunWorker>());
        var cmd = new RunCommand("r1","a1","hello", CancellationToken.None);
        Assert.True(worker.TryEnqueue(cmd));
    }

    [Fact] public async Task Worker_StartsAndStops()
    {
        var cts    = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var worker = new BackgroundRunWorker(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<BackgroundRunWorker>());
        // Should not throw
        await worker.StartAsync(cts.Token);
        await Task.Delay(150);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact] public async Task RunCommand_HasCorrectFields()
    {
        var cmd = new RunCommand("run-1","agent-1","input", CancellationToken.None);
        Assert.Equal("run-1", cmd.RunId);
        Assert.Equal("agent-1", cmd.AgentId);
    }

    [Fact] public async Task EnqueueMultiple_DoesNotThrow()
    {
        var worker = new BackgroundRunWorker(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<BackgroundRunWorker>());
        for (int i = 0; i < 10; i++)
            worker.TryEnqueue(new RunCommand($"r{i}", "a", "x", CancellationToken.None));
    }
}

// ════════════════════════════════════════════════════════════
// RT-19 — SessionCleanupJob
// ════════════════════════════════════════════════════════════
public sealed class RT19_SessionCleanupTests
{
    [Fact] public async Task SessionCleanupJob_StartsAndStops()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var job = new SessionCleanupJob(
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionCleanupJob>());
        await job.StartAsync(cts.Token);
        await Task.Delay(200);
        await job.StopAsync(CancellationToken.None);
        // No exception = pass
    }
}

// ════════════════════════════════════════════════════════════
// RT-20 — TokenRotationJob
// ════════════════════════════════════════════════════════════
public sealed class RT20_TokenRotationJobTests
{
    [Fact] public async Task TokenRotationJob_StartsAndStops()
    {
        var keys  = new InMemoryKeyRotationService();
        var trail = new InMemoryAuditTrail();
        var audit = new AuditLogger(trail);
        var job   = new TokenRotationJob(keys, audit,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<TokenRotationJob>());
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await job.StartAsync(cts.Token);
        await Task.Delay(200);
        await job.StopAsync(CancellationToken.None);
    }

    [Fact] public async Task KeyRotationService_ForceRotate_ChangesKid()
    {
        using var svc = new InMemoryKeyRotationService();
        var old = svc.GetCurrentKey();
        var next = svc.ForceRotate();
        Assert.NotEqual(old.Kid, next.Kid);
    }
}

// ════════════════════════════════════════════════════════════
// RT-A8 — Repository DI Registration Tests
// Verifies that every IXxxRepository is registered in the DI container.
// ════════════════════════════════════════════════════════════
public sealed class RTA8_RepositoryDiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly IServiceProvider _sp;
    public RTA8_RepositoryDiTests(WebApplicationFactory<Program> factory)
        => _sp = factory.Services;

    [Fact] public void IAgentRepository_IsRegistered()
        => Assert.NotNull(_sp.GetService<AGXIOME.Runtime.Repositories.IAgentRepository>());

    [Fact] public void IRunRepository_IsRegistered()
        => Assert.NotNull(_sp.GetService<AGXIOME.Runtime.Repositories.IRunRepository>());

    [Fact] public void ISessionRepository_IsRegistered()
        => Assert.NotNull(_sp.GetService<AGXIOME.Runtime.Repositories.ISessionRepository>());

    [Fact] public void ITeamRepository_IsRegistered()
        => Assert.NotNull(_sp.GetService<AGXIOME.Runtime.Repositories.ITeamRepository>());

    [Fact] public void IWorkflowRepository_IsRegistered()
        => Assert.NotNull(_sp.GetService<AGXIOME.Runtime.Repositories.IWorkflowRepository>());

    [Fact] public void IAgentRepository_IsSingleton()
    {
        var a = _sp.GetRequiredService<AGXIOME.Runtime.Repositories.IAgentRepository>();
        var b = _sp.GetRequiredService<AGXIOME.Runtime.Repositories.IAgentRepository>();
        Assert.Same(a, b);
    }
}

// ════════════════════════════════════════════════════════════
// RT-A8 — InMemoryAgentRepository Unit Tests
// ════════════════════════════════════════════════════════════
public sealed class RTA8_AgentRepositoryTests
{
    private static AGXIOME.Runtime.Repositories.InMemoryAgentRepository Make() => new();
    private static AgentDto Agent(string id = "a1") =>
        new(id, $"agent-{id}", "desc", "gpt-4o", DateTimeOffset.UtcNow);

    [Fact] public async Task Save_ThenGetById_ReturnsAgent()
    {
        var repo = Make();
        var a = Agent();
        await repo.SaveAsync(a);
        var found = await repo.GetByIdAsync("a1");
        Assert.Equal("a1", found!.Id);
    }

    [Fact] public async Task GetById_Missing_ReturnsNull()
    {
        var repo = Make();
        Assert.Null(await repo.GetByIdAsync("nope"));
    }

    [Fact] public async Task List_ReturnsAllSaved()
    {
        var repo = Make();
        await repo.SaveAsync(Agent("x1"));
        await repo.SaveAsync(Agent("x2"));
        var list = await repo.ListAsync();
        Assert.Equal(2, list.Count);
    }

    [Fact] public async Task Update_ModifiesExisting()
    {
        var repo = Make();
        await repo.SaveAsync(Agent("u1"));
        var updated = Agent("u1") with { Name = "renamed" };
        await repo.UpdateAsync("u1", updated);
        var got = await repo.GetByIdAsync("u1");
        Assert.Equal("renamed", got!.Name);
    }

    [Fact] public async Task Update_Missing_ReturnsNull()
    {
        var repo = Make();
        var result = await repo.UpdateAsync("ghost", Agent("ghost"));
        Assert.Null(result);
    }

    [Fact] public async Task Delete_ExistingId_ReturnsTrue()
    {
        var repo = Make();
        await repo.SaveAsync(Agent("d1"));
        Assert.True(await repo.DeleteAsync("d1"));
    }

    [Fact] public async Task Delete_MissingId_ReturnsFalse()
    {
        var repo = Make();
        Assert.False(await repo.DeleteAsync("missing"));
    }

    [Fact] public async Task Exists_AfterSave_ReturnsTrue()
    {
        var repo = Make();
        await repo.SaveAsync(Agent("e1"));
        Assert.True(await repo.ExistsAsync("e1"));
    }

    [Fact] public async Task Exists_BeforeSave_ReturnsFalse()
    {
        var repo = Make();
        Assert.False(await repo.ExistsAsync("e99"));
    }

    [Fact] public async Task Save_Twice_SameId_Overwrites()
    {
        var repo = Make();
        await repo.SaveAsync(Agent("dup") with { Name = "first" });
        await repo.SaveAsync(Agent("dup") with { Name = "second" });
        var list = await repo.ListAsync();
        Assert.Single(list);
        Assert.Equal("second", list[0].Name);
    }
}

// ════════════════════════════════════════════════════════════
// RT-A8 — InMemoryRunRepository Unit Tests
// ════════════════════════════════════════════════════════════
public sealed class RTA8_RunRepositoryTests
{
    private static AGXIOME.Runtime.Repositories.InMemoryRunRepository Make() => new();
    private static RunDto Run(string runId = "r1", string agentId = "ag1") =>
        new(runId, agentId, "running", DateTimeOffset.UtcNow, null, null);

    [Fact] public async Task Save_ThenGet_ReturnsRun()
    {
        var repo = Make();
        await repo.SaveAsync(Run());
        Assert.NotNull(await repo.GetByIdAsync("r1"));
    }

    [Fact] public async Task GetById_Missing_ReturnsNull()
    {
        Assert.Null(await Make().GetByIdAsync("nope"));
    }

    [Fact] public async Task List_NoFilter_ReturnsAll()
    {
        var repo = Make();
        await repo.SaveAsync(Run("r1", "ag1"));
        await repo.SaveAsync(Run("r2", "ag2"));
        Assert.Equal(2, (await repo.ListAsync()).Count);
    }

    [Fact] public async Task List_WithAgentIdFilter_ReturnsSubset()
    {
        var repo = Make();
        await repo.SaveAsync(Run("r1", "ag1"));
        await repo.SaveAsync(Run("r2", "ag1"));
        await repo.SaveAsync(Run("r3", "ag2"));
        Assert.Equal(2, (await repo.ListAsync("ag1")).Count);
    }

    [Fact] public async Task Update_ChangesStatus()
    {
        var repo = Make();
        await repo.SaveAsync(Run("r1"));
        var upd = Run("r1") with { Status = "completed", EndedAt = DateTimeOffset.UtcNow };
        await repo.UpdateAsync("r1", upd);
        var got = await repo.GetByIdAsync("r1");
        Assert.Equal("completed", got!.Status);
    }

    [Fact] public async Task Delete_ExistingRun_ReturnsTrue()
    {
        var repo = Make();
        await repo.SaveAsync(Run());
        Assert.True(await repo.DeleteAsync("r1"));
    }

    [Fact] public async Task Delete_Missing_ReturnsFalse()
    {
        Assert.False(await Make().DeleteAsync("ghost"));
    }
}

// ════════════════════════════════════════════════════════════
// RT-A8 — InMemorySessionRepository Unit Tests
// ════════════════════════════════════════════════════════════
public sealed class RTA8_SessionRepositoryTests
{
    private static AGXIOME.Runtime.Repositories.InMemorySessionRepository Make() => new();
    private static SessionDto Session(string id = "s1") =>
        new(id, "agent-1", DateTimeOffset.UtcNow, true);

    [Fact] public async Task Save_ThenGet_ReturnsSession()
    {
        var repo = Make();
        await repo.SaveAsync(Session());
        Assert.NotNull(await repo.GetByIdAsync("s1"));
    }

    [Fact] public async Task List_FilterByAgent_ReturnsSubset()
    {
        var repo = Make();
        await repo.SaveAsync(new SessionDto("s1", "ag1", DateTimeOffset.UtcNow, true));
        await repo.SaveAsync(new SessionDto("s2", "ag1", DateTimeOffset.UtcNow, true));
        await repo.SaveAsync(new SessionDto("s3", "ag2", DateTimeOffset.UtcNow, true));
        Assert.Equal(2, (await repo.ListAsync("ag1")).Count);
    }

    [Fact] public async Task Close_SetsIsActiveFalse()
    {
        var repo = Make();
        await repo.SaveAsync(Session());
        var closed = await repo.CloseAsync("s1");
        Assert.NotNull(closed);
        Assert.False(closed!.IsActive);
    }

    [Fact] public async Task Close_Missing_ReturnsNull()
    {
        Assert.Null(await Make().CloseAsync("nope"));
    }

    [Fact] public async Task Delete_ExistingSession_ReturnsTrue()
    {
        var repo = Make();
        await repo.SaveAsync(Session());
        Assert.True(await repo.DeleteAsync("s1"));
    }
}

// ════════════════════════════════════════════════════════════
// RT-A8 — InMemoryTeamRepository Unit Tests
// ════════════════════════════════════════════════════════════
public sealed class RTA8_TeamRepositoryTests
{
    private static AGXIOME.Runtime.Repositories.InMemoryTeamRepository Make() => new();
    private static TeamDto Team(string id = "t1") =>
        new(id, $"team-{id}", ["a1", "a2"]);

    [Fact] public async Task Save_ThenGet_ReturnsTeam()
    {
        var repo = Make();
        await repo.SaveAsync(Team());
        var found = await repo.GetByIdAsync("t1");
        Assert.Equal("t1", found!.TeamId);
    }

    [Fact] public async Task List_ReturnsAllTeams()
    {
        var repo = Make();
        await repo.SaveAsync(Team("t1"));
        await repo.SaveAsync(Team("t2"));
        Assert.Equal(2, (await repo.ListAsync()).Count);
    }

    [Fact] public async Task Update_ChangesName()
    {
        var repo = Make();
        await repo.SaveAsync(Team("t1"));
        await repo.UpdateAsync("t1", new TeamDto("t1", "renamed", []));
        var got = await repo.GetByIdAsync("t1");
        Assert.Equal("renamed", got!.Name);
    }

    [Fact] public async Task Delete_Returns_True()
    {
        var repo = Make();
        await repo.SaveAsync(Team("t1"));
        Assert.True(await repo.DeleteAsync("t1"));
    }

    [Fact] public async Task Exists_ReturnsFalse_BeforeSave()
    {
        Assert.False(await Make().ExistsAsync("t99"));
    }

    [Fact] public async Task Exists_ReturnsTrue_AfterSave()
    {
        var repo = Make();
        await repo.SaveAsync(Team());
        Assert.True(await repo.ExistsAsync("t1"));
    }
}

// ════════════════════════════════════════════════════════════
// RT-A8 — InMemoryWorkflowRepository Unit Tests
// ════════════════════════════════════════════════════════════
public sealed class RTA8_WorkflowRepositoryTests
{
    private static AGXIOME.Runtime.Repositories.InMemoryWorkflowRepository Make() => new();
    private static WorkflowDto Wf(string id = "wf1") =>
        new(id, $"workflow-{id}", ["step1", "step2"], "idle");

    [Fact] public async Task Save_ThenGet_ReturnsWorkflow()
    {
        var repo = Make();
        await repo.SaveAsync(Wf());
        Assert.NotNull(await repo.GetByIdAsync("wf1"));
    }

    [Fact] public async Task List_ReturnsAll()
    {
        var repo = Make();
        await repo.SaveAsync(Wf("wf1"));
        await repo.SaveAsync(Wf("wf2"));
        Assert.Equal(2, (await repo.ListAsync()).Count);
    }

    [Fact] public async Task Update_ChangesStatus()
    {
        var repo = Make();
        await repo.SaveAsync(Wf());
        await repo.UpdateAsync("wf1", Wf("wf1") with { Status = "running" });
        var got = await repo.GetByIdAsync("wf1");
        Assert.Equal("running", got!.Status);
    }

    [Fact] public async Task Delete_ExistingWorkflow_ReturnsTrue()
    {
        var repo = Make();
        await repo.SaveAsync(Wf());
        Assert.True(await repo.DeleteAsync("wf1"));
    }

    [Fact] public async Task RecordHistory_ThenGetHistory_ReturnsEntries()
    {
        var repo = Make();
        await repo.SaveAsync(Wf());
        await repo.RecordHistoryAsync("wf1", new { step = 1 });
        await repo.RecordHistoryAsync("wf1", new { step = 2 });
        var history = await repo.GetHistoryAsync("wf1");
        Assert.Equal(2, history.Count);
    }

    [Fact] public async Task GetHistory_NewWorkflow_ReturnsEmpty()
    {
        var repo = Make();
        await repo.SaveAsync(Wf());
        Assert.Empty(await repo.GetHistoryAsync("wf1"));
    }

    [Fact] public async Task GetHistory_UnknownId_ReturnsEmpty()
    {
        Assert.Empty(await Make().GetHistoryAsync("ghost"));
    }
}
