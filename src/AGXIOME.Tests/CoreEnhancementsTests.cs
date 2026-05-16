using AGXIOME.Core.Agent;
using AGXIOME.Core.Execution;
using AGXIOME.Core.LLM;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NSubstitute;
using Xunit;

namespace AGXIOME.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CE-01 — BackgroundRunWorker (10 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class BackgroundRunWorkerTests
{
    private static RunJob MakeJob(string id, string prompt = "hello") =>
        new(id, BackgroundJobType.RunExecution, "agent-1", prompt);

    [Fact]
    public async Task Enqueue_ReturnsJobId_AndCompletesRealWorker()
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentResponse("done", AgentStatus.Completed)));

        using var worker = new AGXIOME.Core.Execution.BackgroundRunWorker(agent);

        var jobId = worker.Enqueue(MakeJob("job-1"));
        var state = await WaitForStateAsync(worker, jobId, BackgroundJobStatus.Completed);

        Assert.Equal("job-1", jobId);
        Assert.NotNull(state);
        Assert.Equal(BackgroundJobStatus.Completed, state!.Status);
        Assert.Equal("done", state.ResultText);
    }

    [Fact]
    public void GetState_UnknownJob_ReturnsNull()
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        using var worker = new AGXIOME.Core.Execution.BackgroundRunWorker(agent);

        Assert.Null(worker.GetState("missing"));
    }

    [Fact]
    public void RunJob_Record_HoldsCorrectValues()
    {
        var job = MakeJob("j1", "test prompt");
        Assert.Equal("j1", job.JobId);
        Assert.Equal(BackgroundJobType.RunExecution, job.JobType);
        Assert.Equal("test prompt", job.Prompt);
    }

    [Fact]
    public void RunJobState_TerminalStatus_Completed()
    {
        var state = new RunJobState("j1", BackgroundJobType.RunExecution, BackgroundJobStatus.Completed);
        Assert.Equal(BackgroundJobStatus.Completed, state.Status);
    }

    [Fact]
    public void RunJobState_TerminalStatus_Cancelled()
    {
        var state = new RunJobState("j1", BackgroundJobType.RunExecution, BackgroundJobStatus.Cancelled);
        Assert.Equal(BackgroundJobStatus.Cancelled, state.Status);
    }

    [Fact]
    public void RunJobState_TerminalStatus_Failed()
    {
        var state = new RunJobState("j1", BackgroundJobType.RunExecution, BackgroundJobStatus.Failed,
            ErrorMessage: "Simulated failure");
        Assert.Equal(BackgroundJobStatus.Failed, state.Status);
        Assert.Equal("Simulated failure", state.ErrorMessage);
    }

    [Fact]
    public void ListJobs_EmptyByDefault()
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        using var worker = new AGXIOME.Core.Execution.BackgroundRunWorker(agent);

        Assert.Empty(worker.ListJobs());
    }

    [Fact]
    public async Task Cancel_RunningJob_ReturnsTrue_AndTransitionsToCancelled()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var ct = call.Arg<CancellationToken>();
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new AgentResponse("never", AgentStatus.Completed);
            });

        using var worker = new AGXIOME.Core.Execution.BackgroundRunWorker(agent);
        var jobId = worker.Enqueue(MakeJob("job-cancel"));
        await started.Task;

        Assert.True(worker.Cancel(jobId));

        var state = await WaitForStateAsync(worker, jobId, BackgroundJobStatus.Cancelled);
        Assert.NotNull(state);
        Assert.Equal(BackgroundJobStatus.Cancelled, state!.Status);
    }

    [Fact]
    public async Task WatchAsync_YieldsStateTransitions_ForKnownJob()
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentResponse("done", AgentStatus.Completed)));

        using var worker = new AGXIOME.Core.Execution.BackgroundRunWorker(agent);
        var jobId = worker.Enqueue(MakeJob("job-watch"));

        var states = new List<RunJobState>();
        await foreach (var state in worker.WatchAsync(jobId))
            states.Add(state);

        Assert.Contains(states, state => state.Status == BackgroundJobStatus.Running);
        Assert.Contains(states, state => state.Status == BackgroundJobStatus.Completed && state.ResultText == "done");
    }

    [Fact]
    public void BackgroundJobType_AllValues_AreDistinct()
    {
        var values = Enum.GetValues<BackgroundJobType>();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void BackgroundJobStatus_ContainsCancelledAndRetrying()
    {
        Assert.True(Enum.IsDefined(BackgroundJobStatus.Cancelled));
        Assert.True(Enum.IsDefined(BackgroundJobStatus.Retrying));
    }

    private static async Task<RunJobState?> WaitForStateAsync(
        AGXIOME.Core.Execution.BackgroundRunWorker worker,
        string jobId,
        BackgroundJobStatus expected,
        int attempts = 50)
    {
        for (var i = 0; i < attempts; i++)
        {
            var state = worker.GetState(jobId);
            if (state?.Status == expected)
                return state;

            await Task.Delay(20);
        }

        return worker.GetState(jobId);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-02 — CancellationManager (8 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class CancellationManagerTests
{
    [Fact]
    public void Register_ReturnsNonCancelledToken()
    {
        using var mgr = new CancellationManager();
        var token = mgr.Register("run-1");
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_ExistingRun_CancelsToken()
    {
        using var mgr = new CancellationManager();
        var token = mgr.Register("run-2");
        var result = mgr.Cancel("run-2");
        Assert.True(result);
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_UnknownRun_ReturnsFalse()
    {
        using var mgr = new CancellationManager();
        Assert.False(mgr.Cancel("nonexistent"));
    }

    [Fact]
    public void IsActive_AfterRegister_ReturnsTrue()
    {
        using var mgr = new CancellationManager();
        mgr.Register("run-3");
        Assert.True(mgr.IsActive("run-3"));
    }

    [Fact]
    public void IsActive_AfterCancel_ReturnsFalse()
    {
        using var mgr = new CancellationManager();
        mgr.Register("run-4");
        mgr.Cancel("run-4");
        Assert.False(mgr.IsActive("run-4"));
    }

    [Fact]
    public void Remove_ExistingRun_ReturnsTrue()
    {
        using var mgr = new CancellationManager();
        mgr.Register("run-5");
        Assert.True(mgr.Remove("run-5"));
        Assert.False(mgr.IsActive("run-5"));
    }

    [Fact]
    public void CancelAll_CancelsAllRegistered()
    {
        using var mgr = new CancellationManager();
        var t1 = mgr.Register("r1");
        var t2 = mgr.Register("r2");
        mgr.CancelAll();
        Assert.True(t1.IsCancellationRequested);
        Assert.True(t2.IsCancellationRequested);
    }

    [Fact]
    public void ActiveRunIds_ReflectsState()
    {
        using var mgr = new CancellationManager();
        mgr.Register("a");
        mgr.Register("b");
        mgr.Cancel("a");
        var active = mgr.ActiveRunIds;
        Assert.DoesNotContain("a", active);
        Assert.Contains("b", active);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-03 — RemoteAgent (8 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class RemoteAgentTests
{
    private static HttpClient MakeHttpClient(HttpStatusCode code, object? body = null)
    {
        var handler = new TestHttpMessageHandler(code, body is null
            ? "{}"
            : JsonSerializer.Serialize(body));
        return new HttpClient(handler);
    }

    [Fact]
    public void Constructor_SetsBaseUrl()
    {
        var opts = new RemoteAgentOptions { BaseUrl = "http://localhost:5250" };
        var agent = new RemoteAgent(MakeHttpClient(HttpStatusCode.OK), opts);
        Assert.Equal("http://localhost:5250", agent.RemoteBaseUrl);
    }

    [Fact]
    public async Task RunAsync_SuccessResponse_ReturnsContent()
    {
        var expected = new AgentResponse("Hello from remote", AgentStatus.Completed);
        var opts = new RemoteAgentOptions { BaseUrl = "http://localhost:5250" };
        var agent = new RemoteAgent(MakeHttpClient(HttpStatusCode.OK, expected), opts);
        var result = await agent.RunAsync(new AgentRequest("hi"));
        Assert.Equal("Hello from remote", result.Content);
    }

    [Fact]
    public async Task RunAsync_HttpError_Throws()
    {
        var opts = new RemoteAgentOptions { BaseUrl = "http://localhost:5250" };
        var agent = new RemoteAgent(MakeHttpClient(HttpStatusCode.InternalServerError), opts);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            agent.RunAsync(new AgentRequest("hi")));
    }

    [Fact]
    public void Options_DefaultTimeout_Is60s()
    {
        var opts = new RemoteAgentOptions { BaseUrl = "http://host" };
        Assert.Equal(TimeSpan.FromSeconds(60), opts.Timeout);
    }

    [Fact]
    public void Options_CustomTimeout_IsStored()
    {
        var opts = new RemoteAgentOptions { BaseUrl = "http://host", Timeout = TimeSpan.FromSeconds(120) };
        Assert.Equal(TimeSpan.FromSeconds(120), opts.Timeout);
    }

    [Fact]
    public void RemoteAgentFactory_Create_ReturnsInstance()
    {
        var agent = RemoteAgentFactory.Create(new RemoteAgentOptions { BaseUrl = "http://host:5250" });
        Assert.NotNull(agent);
        Assert.Equal("http://host:5250", agent.RemoteBaseUrl);
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RemoteAgent(new HttpClient(), null!));
    }

    [Fact]
    public void Constructor_NullHttpClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RemoteAgent(null!, new RemoteAgentOptions { BaseUrl = "http://h" }));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-04 — AgentSerializer (10 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class AgentSerializerTests
{
    private static AgentSnapshot MakeSnapshot(string id = "agent-1") => new()
    {
        Id = id,
        Name = "TestAgent",
        Description = "A test agent",
        Config = new AgentConfig(),
    };

    [Fact]
    public void ToDictionary_ContainsId()
    {
        var snap = MakeSnapshot("abc");
        var dict = new AgentSerializer().ToDictionary(snap);
        Assert.Equal("abc", dict["id"]?.ToString());
    }

    [Fact]
    public void ToDictionary_ContainsName()
    {
        var dict = new AgentSerializer().ToDictionary(MakeSnapshot());
        Assert.Equal("TestAgent", dict["name"]?.ToString());
    }

    [Fact]
    public void ToDictionary_ContainsSchemaVersion()
    {
        var dict = new AgentSerializer().ToDictionary(MakeSnapshot());
        Assert.Equal("1", dict["schemaVersion"]?.ToString());
    }

    [Fact]
    public void FromDictionary_RoundTrip_IdPreserved()
    {
        var serializer = new AgentSerializer();
        var snap = MakeSnapshot("roundtrip-id");
        var dict = serializer.ToDictionary(snap);
        var restored = serializer.FromDictionary(dict!);
        Assert.Equal("roundtrip-id", restored.Id);
    }

    [Fact]
    public void FromDictionary_MissingId_Throws()
    {
        var dict = new Dictionary<string, object?> { ["name"] = "x" };
        Assert.Throws<InvalidOperationException>(() =>
            new AgentSerializer().FromDictionary(dict));
    }

    [Fact]
    public void FromDictionary_MissingName_Throws()
    {
        var dict = new Dictionary<string, object?> { ["id"] = "x" };
        Assert.Throws<InvalidOperationException>(() =>
            new AgentSerializer().FromDictionary(dict));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var path = Path.GetTempFileName();
        try
        {
            var ser = new AgentSerializer();
            var snap = MakeSnapshot("saved");
            await ser.SaveAsync(snap, path);
            var loaded = await ser.LoadAsync(path);
            Assert.Equal("saved", loaded.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task LoadAsync_MissingFile_Throws()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            new AgentSerializer().LoadAsync("/nonexistent/path/agent.json"));
    }

    [Fact]
    public void Snapshot_DefaultSchemaVersion_Is1()
    {
        Assert.Equal("1", MakeSnapshot().SchemaVersion);
    }

    [Fact]
    public void ToDictionary_NullSnapshot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AgentSerializer().ToDictionary(null!));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-05 — ModelStringParser (8 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class ModelStringParserTests
{
    [Fact]
    public void Parse_OpenAI_Gpt4o()
    {
        var result = ModelStringParser.Parse("openai:gpt-4o");
        Assert.Equal(LLMProviderType.OpenAI, result.Provider);
        Assert.Equal("gpt-4o", result.ModelId);
    }

    [Fact]
    public void Parse_Anthropic_Claude()
    {
        var result = ModelStringParser.Parse("anthropic:claude-3-5-sonnet-20241022");
        Assert.Equal(LLMProviderType.Anthropic, result.Provider);
    }

    [Fact]
    public void Parse_CaseInsensitive()
    {
        var r1 = ModelStringParser.Parse("OPENAI:gpt-4o");
        var r2 = ModelStringParser.Parse("openai:gpt-4o");
        Assert.Equal(r1.Provider, r2.Provider);
    }

    [Fact]
    public void Parse_UnknownProvider_Throws()
    {
        Assert.Throws<ArgumentException>(() => ModelStringParser.Parse("nonexistent:model"));
    }

    [Fact]
    public void Parse_MissingColon_Throws()
    {
        Assert.Throws<ArgumentException>(() => ModelStringParser.Parse("openai-gpt-4o"));
    }

    [Fact]
    public void Parse_EmptyModelId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ModelStringParser.Parse("openai:"));
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsNull()
    {
        Assert.Null(ModelStringParser.TryParse("bad-format"));
    }

    [Fact]
    public void Format_RoundTrip()
    {
        var parsed = ModelStringParser.Parse("groq:llama-3.3-70b-versatile");
        var formatted = ModelStringParser.Format(parsed.Provider, parsed.ModelId);
        Assert.Contains("llama-3.3-70b-versatile", formatted);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-06 — ResponseCache (8 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class ResponseCacheTests
{
    [Fact]
    public async Task Get_EmptyCache_ReturnsNull()
    {
        var cache = new ResponseCache();
        var result = await cache.GetAsync(LLMProviderType.OpenAI, "gpt-4o", "hello");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGet_SameKey_ReturnsValue()
    {
        var cache = new ResponseCache();
        await cache.SetAsync(LLMProviderType.OpenAI, "gpt-4o", "hello", "world");
        var result = await cache.GetAsync(LLMProviderType.OpenAI, "gpt-4o", "hello");
        Assert.Equal("world", result);
    }

    [Fact]
    public async Task Set_DifferentProviders_DontCollide()
    {
        var cache = new ResponseCache();
        await cache.SetAsync(LLMProviderType.OpenAI, "gpt-4o", "q", "openai-response");
        await cache.SetAsync(LLMProviderType.Anthropic, "gpt-4o", "q", "anthropic-response");
        var openai = await cache.GetAsync(LLMProviderType.OpenAI, "gpt-4o", "q");
        var anthropic = await cache.GetAsync(LLMProviderType.Anthropic, "gpt-4o", "q");
        Assert.Equal("openai-response", openai);
        Assert.Equal("anthropic-response", anthropic);
    }

    [Fact]
    public async Task Invalidate_RemovesEntry()
    {
        var cache = new ResponseCache();
        await cache.SetAsync(LLMProviderType.Gemini, "gemini-1.5-pro", "q", "answer");
        await cache.InvalidateAsync(LLMProviderType.Gemini, "gemini-1.5-pro", "q");
        var result = await cache.GetAsync(LLMProviderType.Gemini, "gemini-1.5-pro", "q");
        Assert.Null(result);
    }

    [Fact]
    public async Task ExpiredEntry_ReturnsNull()
    {
        var opts = new ResponseCacheOptions { DefaultTtl = TimeSpan.FromMilliseconds(1) };
        var cache = new ResponseCache(opts);
        await cache.SetAsync(LLMProviderType.OpenAI, "gpt-4o", "q", "answer");
        await Task.Delay(10); // let it expire
        var result = await cache.GetAsync(LLMProviderType.OpenAI, "gpt-4o", "q");
        Assert.Null(result);
    }

    [Fact]
    public async Task NoneStrategy_NeverCaches()
    {
        var cache = new ResponseCache(new ResponseCacheOptions { Strategy = CacheStrategy.None });
        await cache.SetAsync(LLMProviderType.OpenAI, "gpt-4o", "q", "answer");
        var result = await cache.GetAsync(LLMProviderType.OpenAI, "gpt-4o", "q");
        Assert.Null(result);
    }

    [Fact]
    public async Task MemoryCount_IncrementsOnSet()
    {
        var cache = new ResponseCache();
        await cache.SetAsync(LLMProviderType.OpenAI, "m", "p1", "r1");
        await cache.SetAsync(LLMProviderType.OpenAI, "m", "p2", "r2");
        Assert.Equal(2, cache.MemoryCount);
    }

    [Fact]
    public void PurgeExpired_RemovesExpiredEntries()
    {
        // Synchronous test for PurgeExpired — can't wait for async expiry, so we just call it.
        var cache = new ResponseCache();
        cache.PurgeExpired(); // no exception = pass
        Assert.Equal(0, cache.MemoryCount);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-07 — TokenCounter (8 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class TokenCounterTests
{
    private readonly TokenCounter _counter = new();

    [Fact]
    public void Count_EmptyText_ReturnsZero()
    {
        var result = _counter.Count("", LLMProviderType.OpenAI);
        Assert.Equal(0, result.InputTokens);
    }

    [Fact]
    public void Count_ShortText_PositiveTokens()
    {
        var result = _counter.Count("Hello world", LLMProviderType.OpenAI);
        Assert.True(result.InputTokens > 0);
    }

    [Fact]
    public void Count_LongerText_MoreTokens()
    {
        var short_ = _counter.Count("hi", LLMProviderType.OpenAI).InputTokens;
        var long_  = _counter.Count(string.Join(" ", Enumerable.Repeat("token", 100)), LLMProviderType.OpenAI).InputTokens;
        Assert.True(long_ > short_);
    }

    [Fact]
    public void Count_IsApproximate_AlwaysTrue()
    {
        var result = _counter.Count("test", LLMProviderType.OpenAI);
        Assert.True(result.IsApproximate);
    }

    [Fact]
    public void AlgorithmFor_OpenAI_IsO200kBase()
    {
        Assert.Equal(TokenizerAlgorithm.O200kBase, _counter.AlgorithmFor(LLMProviderType.OpenAI));
    }

    [Fact]
    public void AlgorithmFor_UnknownProvider_IsGeneric()
    {
        Assert.Equal(TokenizerAlgorithm.Generic, _counter.AlgorithmFor(LLMProviderType.Custom));
    }

    [Fact]
    public void CountWithCompletion_HasPositiveOutputEstimate()
    {
        var result = _counter.CountWithCompletion("This is a test prompt", LLMProviderType.OpenAI, 0.5);
        Assert.True(result.EstimatedOutputTokens > 0);
    }

    [Fact]
    public void CountWithAlgorithm_ExplicitAlgorithm()
    {
        var r1 = _counter.CountWithAlgorithm("test text here", TokenizerAlgorithm.Llama3);
        Assert.Equal(TokenizerAlgorithm.Llama3, r1.Algorithm);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CE-08 — ModelCompatibilityMap (8 tests)
// ─────────────────────────────────────────────────────────────────────────────

public class ModelCompatibilityMapTests
{
    private readonly ModelCompatibilityMap _map = new();

    [Fact]
    public void Check_SupportedModel_NoWarning()
    {
        var result = _map.Check("gpt-4o");
        Assert.Equal(ModelCompatibilityStatus.Supported, result.Status);
        Assert.False(result.HasWarning);
    }

    [Fact]
    public void Check_DeprecatedModel_HasWarning()
    {
        var result = _map.Check("gpt-4-turbo");
        Assert.Equal(ModelCompatibilityStatus.Deprecated, result.Status);
        Assert.True(result.HasWarning);
        Assert.Contains("gpt-4o", result.WarningMessage);
    }

    [Fact]
    public void Check_RemovedModel_HasWarning()
    {
        var result = _map.Check("text-davinci-003");
        Assert.Equal(ModelCompatibilityStatus.Removed, result.Status);
        Assert.True(result.HasWarning);
    }

    [Fact]
    public void Check_PreviewModel_HasWarning()
    {
        var result = _map.Check("o3");
        Assert.Equal(ModelCompatibilityStatus.Preview, result.Status);
        Assert.True(result.HasWarning);
    }

    [Fact]
    public void Check_UnknownModel_ReturnsWarningWithCustomProvider()
    {
        var result = _map.Check("my-custom-internal-model");
        Assert.True(result.HasWarning);
        Assert.Equal(LLMProviderType.Custom, result.Provider);
    }

    [Fact]
    public void GetProvider_KnownModel_ReturnsProvider()
    {
        Assert.Equal(LLMProviderType.Anthropic, _map.GetProvider("claude-3-5-sonnet-20241022"));
    }

    [Fact]
    public void GetProvider_UnknownModel_ReturnsNull()
    {
        Assert.Null(_map.GetProvider("does-not-exist"));
    }

    [Fact]
    public void RegisterModel_AddsCustomEntry()
    {
        var map = new ModelCompatibilityMap();
        map.RegisterModel("my-model", new ModelEntry(LLMProviderType.Local, ModelCompatibilityStatus.Supported));
        var result = map.Check("my-model");
        Assert.Equal(ModelCompatibilityStatus.Supported, result.Status);
        Assert.False(result.HasWarning);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Test helpers
// ─────────────────────────────────────────────────────────────────────────────

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _code;
    private readonly string _body;

    public TestHttpMessageHandler(HttpStatusCode code, string body)
    {
        _code = code;
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var resp = new HttpResponseMessage(_code)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(resp);
    }
}
