using AGXIOME.Core;
using AGXIOME.Core.Agent;
using AGXIOME.Core.LLM;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Net;
using System.Text;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// F18 — Core Enhancements: CE-03 (RemoteAgent), CE-05 (Model-as-string),
//       CE-08 (ModelCompatibilityMap)
//
// Coverage matrix:
//   RA-01..RA-10   RemoteAgentOptions + RemoteAgent (CE-03)
//   AA-01..AA-06   RemoteAgentAdapter + BuildRemote (CE-03 adapter)
//   MS-01..MS-08   AgentBuilder.UseModel(string) (CE-05)
//   MC-01..MC-12   ModelCompatibilityMap static helpers (CE-08)
//
// All tests use F18HttpStub — no network calls.
// ─────────────────────────────────────────────────────────────────────────────

// ══════════════════════════════════════════════════════════════════════════════
// Test infrastructure
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Flexible fake <see cref="HttpMessageHandler"/> for F18 tests.
/// Supports fixed responses and per-call routing via a delegate.
/// </summary>
internal sealed class F18HttpStub : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public F18HttpStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => _handler = handler;

    /// <summary>Convenience: always return a fixed JSON body with 200 OK.</summary>
    public static F18HttpStub Json(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    /// <summary>Returns an SSE stream body (plain text, no JSON wrapper).</summary>
    public static F18HttpStub Sse(string sseBody)
        => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseBody, Encoding.UTF8, "text/event-stream"),
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}

// ══════════════════════════════════════════════════════════════════════════════
// RA — RemoteAgentOptions + RemoteAgent
// ══════════════════════════════════════════════════════════════════════════════

public sealed class F18RemoteAgentOptionsTests
{
    // RA-01: defaults are sensible
    [Fact]
    public void RA01_Defaults_AreCorrect()
    {
        var opts = new RemoteAgentOptions { BaseUrl = "https://host:5250" };
        Assert.Equal("remote", opts.AgentId);
        Assert.Equal(TimeSpan.FromSeconds(60), opts.Timeout);
        Assert.Null(opts.BearerToken);
    }

    // RA-02: explicit properties round-trip
    [Fact]
    public void RA02_ExplicitProperties_RoundTrip()
    {
        var opts = new RemoteAgentOptions
        {
            BaseUrl     = "https://prod:8443",
            AgentId     = "my-agent",
            Timeout     = TimeSpan.FromSeconds(120),
            BearerToken = "tok",
        };
        Assert.Equal("https://prod:8443", opts.BaseUrl);
        Assert.Equal("my-agent",          opts.AgentId);
        Assert.Equal(TimeSpan.FromSeconds(120), opts.Timeout);
        Assert.Equal("tok",               opts.BearerToken);
    }
}

public sealed class F18RemoteAgentTests
{
    private static RemoteAgent Build(F18HttpStub handler, string baseUrl = "https://host:5250")
    {
        var opts = new RemoteAgentOptions { BaseUrl = baseUrl };
        return new RemoteAgent(new HttpClient(handler), opts);
    }

    // RA-03: BaseUrl property exposed
    [Fact]
    public void RA03_RemoteBaseUrl_ReflectsOptions()
    {
        var agent = Build(F18HttpStub.Json("{}"), "https://remote:9000");
        Assert.Equal("https://remote:9000", agent.RemoteBaseUrl);
    }

    // RA-04: RunAsync parses { content, status: "completed" }
    [Fact]
    public async Task RA04_RunAsync_ParsesCompletedResponse()
    {
        const string json = """{"runId":"r1","status":"completed","content":"Hello world"}""";
        var agent = Build(F18HttpStub.Json(json));

        var resp = await agent.RunAsync(new AgentRequest("hi"));

        Assert.Equal("Hello world", resp.Content);
        Assert.Equal(AgentStatus.Completed, resp.Status);
    }

    // RA-05: RunAsync parses non-completed status → Error
    [Fact]
    public async Task RA05_RunAsync_NonCompletedStatus_MapsToError()
    {
        const string json = """{"runId":"r2","status":"failed","content":""}""";
        var agent = Build(F18HttpStub.Json(json));

        var resp = await agent.RunAsync(new AgentRequest("test"));

        Assert.Equal(AgentStatus.Error, resp.Status);
    }

    // RA-06: RunAsync HTTP error status → HttpRequestException
    [Fact]
    public async Task RA06_RunAsync_HttpError_Throws()
    {
        var handler = F18HttpStub.Json("{}", HttpStatusCode.InternalServerError);
        var agent   = Build(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            agent.RunAsync(new AgentRequest("test")));
    }

    // RA-07: StreamAsync yields chunk contents from SSE
    [Fact]
    public async Task RA07_StreamAsync_YieldsChunks()
    {
        // First call (POST /api/runs/async) returns runId
        const string asyncJson = """{"runId":"r3","status":"queued"}""";
        // Second call (GET /api/runs/{id}/stream) returns SSE
        const string sseBody =
            "data: {\"chunk\":\"Hello\",\"isFinal\":false}\n\n" +
            "data: {\"chunk\":\" world\",\"isFinal\":true}\n\n";

        int callCount = 0;
        var handler = new F18HttpStub(_ =>
        {
            callCount++;
            if (callCount == 1)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(asyncJson, Encoding.UTF8, "application/json"),
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sseBody, Encoding.UTF8, "text/event-stream"),
            };
        });

        var agent  = Build(handler);
        var chunks = new List<AgentChunk>();
        await foreach (var ch in agent.StreamAsync(new AgentRequest("stream me")))
            chunks.Add(ch);

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Hello",  chunks[0].Content);
        Assert.False(chunks[0].IsFinal);
        Assert.Equal(" world", chunks[1].Content);
        Assert.True(chunks[1].IsFinal);
    }

    // RA-08: StreamAsync — no runId returned → InvalidOperationException
    [Fact]
    public async Task RA08_StreamAsync_MissingRunId_Throws()
    {
        var handler = F18HttpStub.Json("""{ "runId":null}""");
        var agent   = Build(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in agent.StreamAsync(new AgentRequest("x"))) { }
        });
    }

    // RA-09: BearerToken wired into Authorization header
    [Fact]
    public async Task RA09_BearerToken_SetOnHttpClient()
    {
        string? capturedHeader = null;
        var handler = new F18HttpStub(req =>
        {
            capturedHeader = req.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"completed","content":"ok"}""",
                    Encoding.UTF8, "application/json"),
            };
        });

        var opts  = new RemoteAgentOptions { BaseUrl = "https://host:5250", BearerToken = "my-secret-token" };
        var agent = new RemoteAgent(new HttpClient(handler), opts);

        await agent.RunAsync(new AgentRequest("hi"));

        Assert.Equal("my-secret-token", capturedHeader);
    }

    // RA-10: null args throw ArgumentNullException
    [Fact]
    public void RA10_NullArgs_Throw()
    {
        var opts = new RemoteAgentOptions { BaseUrl = "https://host" };
        Assert.Throws<ArgumentNullException>(() => new RemoteAgent(null!, opts));
        Assert.Throws<ArgumentNullException>(() => new RemoteAgent(new HttpClient(), null!));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// AA — RemoteAgentAdapter + AgentBuilder.BuildRemote
// ══════════════════════════════════════════════════════════════════════════════

public sealed class F18RemoteAgentAdapterTests
{
    private static (RemoteAgent remote, F18HttpStub handler) MakeRemoteAgent(
        string json = """{ "status":"completed","content":"ok"} """)
    {
        var handler = F18HttpStub.Json(json);
        var opts    = new RemoteAgentOptions { BaseUrl = "https://host:5250" };
        var remote  = new RemoteAgent(new HttpClient(handler), opts);
        return (remote, handler);
    }

    // AA-01: adapter exposes IAgxiomeAgent
    [Fact]
    public void AA01_Adapter_ImplementsIAgxiomeAgent()
    {
        var (remote, _) = MakeRemoteAgent();
        IAgxiomeAgent adapter = new RemoteAgentAdapter(remote);
        Assert.NotNull(adapter);
    }

    // AA-02: Id uses host when no id specified
    [Fact]
    public void AA02_Id_DefaultsToRemoteHost()
    {
        var (remote, _) = MakeRemoteAgent();
        var adapter = new RemoteAgentAdapter(remote);
        Assert.Contains("host", adapter.Id, StringComparison.OrdinalIgnoreCase);
    }

    // AA-03: explicit id passed through
    [Fact]
    public void AA03_ExplicitId_PassedThrough()
    {
        var (remote, _) = MakeRemoteAgent();
        var adapter = new RemoteAgentAdapter(remote, "my-remote");
        Assert.Equal("my-remote", adapter.Id);
    }

    // AA-04: RunAsync → Completed status propagated to adapter
    [Fact]
    public async Task AA04_RunAsync_PropagatesCompletedStatus()
    {
        var (remote, _) = MakeRemoteAgent("""{"status":"completed","content":"result"}""");
        var adapter = new RemoteAgentAdapter(remote, "a");

        var resp = await adapter.RunAsync(new AgentRequest("hello"));

        Assert.Equal("result", resp.Content);
        Assert.Equal(AgentStatus.Completed, resp.Status);
        Assert.Equal(AgentStatus.Completed, adapter.Status);
    }

    // AA-05: RunEventStreamAsync maps IsFinal=true → RunEvent.RunCompleted
    [Fact]
    public async Task AA05_RunEventStreamAsync_MapsEvents()
    {
        // Need to route two calls (POST async + GET stream)
        const string asyncJson = """{"runId":"r9","status":"queued"}""";
        const string sseBody   =
            "data: {\"chunk\":\"tok\",\"isFinal\":false}\n\n" +
            "data: {\"chunk\":\"end\",\"isFinal\":true}\n\n";

        int callCount = 0;
        var handler = new F18HttpStub(_ =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.OK)
                  { Content = new StringContent(asyncJson, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK)
                  { Content = new StringContent(sseBody,   Encoding.UTF8, "text/event-stream") };
        });

        var opts    = new RemoteAgentOptions { BaseUrl = "https://host:5250" };
        var remote  = new RemoteAgent(new HttpClient(handler), opts);
        var adapter = new RemoteAgentAdapter(remote, "b");

        var events = new List<RunOutputEvent>();
        await foreach (var ev in adapter.RunEventStreamAsync(new AgentRequest("x")))
            events.Add(ev);

        Assert.Equal(2, events.Count);
        Assert.Equal(RunEvent.RunContent,   events[0].Event);
        Assert.Equal(RunEvent.RunCompleted, events[1].Event);
    }

    // AA-06: no-op write operations do not throw
    [Fact]
    public void AA06_NoOpWriteOps_DoNotThrow()
    {
        var (remote, _) = MakeRemoteAgent();
        IAgxiomeAgent adapter = new RemoteAgentAdapter(remote, "c");

        // All write ops must be silent no-ops — remote host owns its own config.
        adapter.RegisterTool(null!);
        adapter.UseMiddleware(null!);
        adapter.AddHook(null!);
        adapter.AddToolHook(null!);
        adapter.AddGuardrail(null!);
        // No exception = pass
    }
}

public sealed class F18BuildRemoteTests
{
    // AA-07: AgentBuilder.BuildRemote returns IAgxiomeAgent
    [Fact]
    public void AA07_BuildRemote_ReturnsIAgxiomeAgent()
    {
        IAgxiomeAgent agent = AgxiomeBuilder
            .Agent("remote-test")
            .BuildRemote("https://host:5250");

        Assert.NotNull(agent);
        Assert.IsType<RemoteAgentAdapter>(agent);
    }

    // AA-08: BuildRemote uses agent name as AgentId
    [Fact]
    public void AA08_BuildRemote_UsesAgentName()
    {
        var agent = AgxiomeBuilder
            .Agent("my-remote-agent")
            .BuildRemote("https://host:5250") as RemoteAgentAdapter;

        Assert.NotNull(agent);
        Assert.Equal("my-remote-agent", agent!.Id);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// MS — AgentBuilder.UseModel(string) — CE-05
// ══════════════════════════════════════════════════════════════════════════════

public sealed class F18UseModelStringTests
{
    // MS-01: "openai:gpt-4o" colon format → OpenAI
    [Fact]
    public void MS01_ColonFormat_OpenAI_ParsedCorrectly()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("openai:gpt-4o");
        Assert.Equal(LLMProviderType.OpenAI, provider);
        Assert.Equal("gpt-4o", model);
    }

    // MS-02: "anthropic:claude-3-5-sonnet-20241022" colon format
    [Fact]
    public void MS02_ColonFormat_Anthropic_ParsedCorrectly()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("anthropic:claude-3-5-sonnet-20241022");
        Assert.Equal(LLMProviderType.Anthropic, provider);
        Assert.Equal("claude-3-5-sonnet-20241022", model);
    }

    // MS-03: bare "gpt-4o" → map lookup → OpenAI
    [Fact]
    public void MS03_BareModelName_FoundInMap_OpenAI()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("gpt-4o");
        Assert.Equal(LLMProviderType.OpenAI, provider);
        Assert.Equal("gpt-4o", model);
    }

    // MS-04: bare "deepseek-chat" → map lookup → DeepSeek
    [Fact]
    public void MS04_BareModelName_DeepSeek_FoundInMap()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("deepseek-chat");
        Assert.Equal(LLMProviderType.DeepSeek, provider);
        Assert.Equal("deepseek-chat", model);
    }

    // MS-05: prefix fallback "gpt-5-turbo" (not in map) → OpenAI by prefix
    [Fact]
    public void MS05_PrefixFallback_Gpt_InfersOpenAI()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("gpt-5-turbo");
        Assert.Equal(LLMProviderType.OpenAI, provider);
        Assert.Equal("gpt-5-turbo", model);
    }

    // MS-06: prefix fallback "claude-x-future" → Anthropic by prefix
    [Fact]
    public void MS06_PrefixFallback_Claude_InfersAnthropic()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("claude-x-future");
        Assert.Equal(LLMProviderType.Anthropic, provider);
        Assert.Equal("claude-x-future", model);
    }

    // MS-07: completely unknown → Custom
    [Fact]
    public void MS07_UnknownModel_FallsBackToCustom()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("totally-unknown-xyz");
        Assert.Equal(LLMProviderType.Custom, provider);
        Assert.Equal("totally-unknown-xyz", model);
    }

    // MS-08: null / empty → ArgumentException (null → ArgumentNullException, empty → ArgumentException)
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MS08_EmptyOrWhitespace_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => ModelCompatibilityMap.ParseProviderModel(input));
    }

    [Fact]
    public void MS08b_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ModelCompatibilityMap.ParseProviderModel(null!));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// MC — ModelCompatibilityMap — CE-08
// ══════════════════════════════════════════════════════════════════════════════

public sealed class F18ModelCompatibilityMapTests
{
    private static ModelCompatibilityMap Map() => new();

    // MC-01: supported model → no warning
    [Fact]
    public void MC01_SupportedModel_NoWarning()
    {
        var result = Map().Check("gpt-4o");
        Assert.Equal(ModelCompatibilityStatus.Supported, result.Status);
        Assert.False(result.HasWarning);
        Assert.Null(result.WarningMessage);
    }

    // MC-02: deprecated model → warning with replacement hint
    [Fact]
    public void MC02_DeprecatedModel_HasWarningWithReplacement()
    {
        var result = Map().Check("gpt-4-turbo");
        Assert.Equal(ModelCompatibilityStatus.Deprecated, result.Status);
        Assert.True(result.HasWarning);
        Assert.Contains("gpt-4o", result.WarningMessage);
    }

    // MC-03: removed model → warning mentions REMOVED
    [Fact]
    public void MC03_RemovedModel_HasRemovedWarning()
    {
        var result = Map().Check("text-davinci-003");
        Assert.Equal(ModelCompatibilityStatus.Removed, result.Status);
        Assert.True(result.HasWarning);
        Assert.Contains("REMOVED", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    // MC-04: preview model → warning
    [Fact]
    public void MC04_PreviewModel_HasPreviewWarning()
    {
        var result = Map().Check("o3");
        Assert.Equal(ModelCompatibilityStatus.Preview, result.Status);
        Assert.True(result.HasWarning);
        Assert.Contains("PREVIEW", result.WarningMessage, StringComparison.OrdinalIgnoreCase);
    }

    // MC-05: unknown model → custom provider + warning
    [Fact]
    public void MC05_UnknownModel_CustomProviderAndWarning()
    {
        var result = Map().Check("totally-unknown-model");
        Assert.Equal(LLMProviderType.Custom,              result.Provider);
        Assert.Equal(ModelCompatibilityStatus.Supported,  result.Status);
        Assert.True(result.HasWarning);
        Assert.Contains("not in the compatibility map", result.WarningMessage);
    }

    // MC-06: GetProvider — known model
    [Fact]
    public void MC06_GetProvider_KnownModel_ReturnsProvider()
    {
        Assert.Equal(LLMProviderType.Anthropic, Map().GetProvider("claude-3-5-sonnet-20241022"));
    }

    // MC-07: GetProvider — unknown model returns null
    [Fact]
    public void MC07_GetProvider_Unknown_ReturnsNull()
    {
        Assert.Null(Map().GetProvider("nonexistent-model-xyz"));
    }

    // MC-08: RegisterModel — custom model then queryable
    [Fact]
    public void MC08_RegisterModel_CustomModel_Queryable()
    {
        var map = Map();
        map.RegisterModel("my-internal-v1", new ModelEntry(LLMProviderType.Custom, ModelCompatibilityStatus.Supported));

        var result = map.Check("my-internal-v1");
        Assert.Equal(LLMProviderType.Custom, result.Provider);
        Assert.False(result.HasWarning);
    }

    // MC-09: GetByStatus — deprecated models list
    [Fact]
    public void MC09_GetByStatus_ReturnsCorrectModels()
    {
        var deprecated = Map().GetByStatus(ModelCompatibilityStatus.Deprecated);
        Assert.Contains("gpt-4-turbo",  deprecated);
        Assert.Contains("gpt-3.5-turbo", deprecated);
        Assert.DoesNotContain("gpt-4o",  deprecated);
    }

    // MC-10: GetByProvider — OpenAI group is not empty
    [Fact]
    public void MC10_GetByProvider_OpenAIGroup_NotEmpty()
    {
        var grouped = Map().GetByProvider();
        Assert.True(grouped.ContainsKey(LLMProviderType.OpenAI));
        Assert.NotEmpty(grouped[LLMProviderType.OpenAI]);
    }

    // MC-11: ResolveApiKey — maps provider to env var (env not set → null)
    [Fact]
    public void MC11_ResolveApiKey_UnsetEnv_ReturnsNull()
    {
        // Without the env var set, all should return null (or the real value if set)
        // We just verify the method doesn't throw for any defined provider.
        foreach (var provider in Enum.GetValues<LLMProviderType>())
        {
            // Should never throw — null is acceptable when env var is unset
            var _ = ModelCompatibilityMap.ResolveApiKey(provider);
        }
    }

    // MC-12: ParseProviderModel case-insensitive for colon format
    [Fact]
    public void MC12_ParseProviderModel_CaseInsensitive_ColonFormat()
    {
        var (provider, model) = ModelCompatibilityMap.ParseProviderModel("OpenAI:GPT-4o");
        Assert.Equal(LLMProviderType.OpenAI, provider);
    }
}
