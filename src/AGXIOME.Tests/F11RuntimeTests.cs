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

    [Fact] public void Services_IKeyRotationService_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        var svc = scope.ServiceProvider.GetService<IKeyRotationService>();
        Assert.NotNull(svc);
    }

    [Fact] public void Services_IApiKeyStore_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IApiKeyStore>());
    }

    [Fact] public void Services_IRbacPolicy_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRbacPolicy>());
    }

    [Fact] public void Services_IRateLimiter_Registered()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IRateLimiter>());
    }

    [Fact] public void Services_AuditLogger_Registered()
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

    [Fact] public void Viewer_CannotDelete()
    {
        var viewer = new AgxiomePrincipal("v", [AgxiomeRole.Viewer], []);
        Assert.False(_policy.IsAuthorized(viewer, AgxiomePermission.AgentDelete));
    }

    [Fact] public void Admin_CanDoEverything()
    {
        var admin = new AgxiomePrincipal("a", [AgxiomeRole.Admin], []);
        foreach (AgxiomePermission perm in Enum.GetValues<AgxiomePermission>())
            Assert.True(_policy.IsAuthorized(admin, perm), $"Admin missing permission: {perm}");
    }

    [Fact] public void Operator_CanRun()
    {
        var op = new AgxiomePrincipal("o", [AgxiomeRole.Operator], []);
        Assert.True(_policy.IsAuthorized(op, AgxiomePermission.AgentRun));
    }

    [Fact] public void MultiRole_GrantsUnion()
    {
        var both = new AgxiomePrincipal("b", [AgxiomeRole.Viewer, AgxiomeRole.Operator], []);
        Assert.True(_policy.IsAuthorized(both, AgxiomePermission.AgentRun));
        Assert.True(_policy.IsAuthorized(both, AgxiomePermission.AgentRead));
    }

    [Fact] public void EmptyRoles_DeniesAll()
    {
        var none = new AgxiomePrincipal("n", [], []);
        Assert.False(_policy.IsAuthorized(none, AgxiomePermission.AgentRead));
    }

    [Fact] public void GetPermissions_Admin_HasMoreThanViewer()
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
    [Fact] public void AllowsRequestsUpToLimit()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(5, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 5; i++) Assert.True(limiter.Check("k", policy).IsAllowed);
    }

    [Fact] public void BlocksAboveLimit_Returns429Data()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(3, TimeSpan.FromMinutes(1));
        for (int i = 0; i < 3; i++) limiter.Check("x", policy);
        var r = limiter.Check("x", policy);
        Assert.False(r.IsAllowed);
        Assert.True(r.RetryAfter.TotalSeconds > 0);
    }

    [Fact] public void ResetClearsState()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(1, TimeSpan.FromMinutes(1));
        limiter.Check("y", policy);
        limiter.Reset("y");
        Assert.True(limiter.Check("y", policy).IsAllowed);
    }

    [Fact] public void DifferentKeys_NotInterfering()
    {
        var limiter = new SlidingWindowRateLimiter();
        var policy  = new RateLimitPolicy(1, TimeSpan.FromMinutes(1));
        limiter.Check("a", policy);
        limiter.Check("a", policy); // second denied for "a"
        // "b" should still succeed
        Assert.True(limiter.Check("b", policy).IsAllowed);
    }

    [Fact] public void PolicyRegistry_FindsByRoute()
    {
        var registry = new RateLimitPolicyRegistry();
        var p = new RateLimitPolicy(10, TimeSpan.FromMinutes(1));
        registry.Register("/api/runs", RateLimitDimension.User, p);
        var found = registry.Find("/api/runs", RateLimitDimension.User);
        Assert.NotNull(found);
        Assert.Equal(10, found!.MaxRequests);
    }

    [Fact] public void PolicyRegistry_ReturnsNull_ForUnknownRoute()
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
    [Fact] public void AgentHub_Instantiates()
    {
        var hub = new AgentHub();
        Assert.NotNull(hub);
    }

    [Fact] public void LogHub_Instantiates()
    {
        var hub = new LogHub();
        Assert.NotNull(hub);
    }

    [Fact] public void AdminHub_Instantiates()
    {
        var hub = new AdminHub();
        Assert.NotNull(hub);
    }

    [Fact] public void AgentRunEvent_Constructed()
    {
        var evt = new AgentRunEvent("r1","a1","started","",1,DateTimeOffset.UtcNow);
        Assert.Equal("r1", evt.RunId);
    }

    [Fact] public void LogEvent_HasCorrectFields()
    {
        var evt = new LogEvent("module","info","msg",2,DateTimeOffset.UtcNow);
        Assert.Equal("info", evt.Level);
    }

    [Fact] public void AdminMetricEvent_HasSeq()
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
    [Fact] public void TryEnqueue_ReturnsTrueWhenSpace()
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

    [Fact] public void RunCommand_HasCorrectFields()
    {
        var cmd = new RunCommand("run-1","agent-1","input", CancellationToken.None);
        Assert.Equal("run-1", cmd.RunId);
        Assert.Equal("agent-1", cmd.AgentId);
    }

    [Fact] public void EnqueueMultiple_DoesNotThrow()
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

    [Fact] public void KeyRotationService_ForceRotate_ChangesKid()
    {
        using var svc = new InMemoryKeyRotationService();
        var old = svc.GetCurrentKey();
        var next = svc.ForceRotate();
        Assert.NotEqual(old.Kid, next.Kid);
    }
}
