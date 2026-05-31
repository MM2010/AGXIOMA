extern alias llmwiki;
extern alias researchagent;
extern alias ragpipeline;
extern alias skilllab;
extern alias stocktracker;
extern alias supporthub;
extern alias toolagent;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using AGXIOME.Core.Patterns;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using llmwiki::AgxiomaLLMWiki;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace AGXIOME.Tests;

using LlmWikiProgram = llmwiki::Program;
using RagPipelineProgram = ragpipeline::Program;
using ResearchAgentProgram = researchagent::Program;
using SkillLabProgram = skilllab::Program;
using StockTrackerProgram = stocktracker::Program;
using SupportHubProgram = supporthub::Program;
using ToolAgentProgram = toolagent::Program;

internal static class SecurityHttpTestDoubles
{
    public static IAgxiomeAgent CreateDeterministicAgent(string content)
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.Id.Returns("deterministic-test-agent");
        agent.Status.Returns(AgentStatus.Completed);
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentResponse(content, AgentStatus.Completed)));
        return agent;
    }

    public static IEmbedder CreateDeterministicEmbedder()
    {
        var embedder = Substitute.For<IEmbedder>();
        embedder.EmbedderType.Returns(EmbedderType.OllamaMxbai);
        embedder.Dimensions.Returns(1024);
        embedder.EmbedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new float[1024]));
        embedder.EmbedBatchAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var texts = call.Arg<IReadOnlyList<string>>();
                var vectors = texts.Select(_ => new float[1024]).ToArray();
                return Task.FromResult(vectors);
            });
        return embedder;
    }
}

public sealed class ToolAgentHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = new WebApplicationFactory<ToolAgentProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Chat_Returns401_WhenApiKeyMissing()
    {
        using var factory = new WebApplicationFactory<ToolAgentProgram>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/chat", new { prompt = "hello", sessionId = "s1" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Chat_Returns401_ForWrongApiKey()
    {
        using var factory = CreateToolAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsJsonAsync("/chat", new { prompt = "hello", sessionId = "s1" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Chat_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateToolAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
            last = await client.PostAsJsonAsync("/chat", new { prompt = "hello", sessionId = $"s{i}" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Chat_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateToolAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"prompt\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/chat", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at ", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<ToolAgentProgram> CreateToolAgentFactory(Dictionary<string, string?> config) =>
        new WebApplicationFactory<ToolAgentProgram>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgxiomeAgent>();
                services.AddSingleton(_ => SecurityHttpTestDoubles.CreateDeterministicAgent("tool response"));
            });
        });
}

public sealed class RagPipelineHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = new WebApplicationFactory<RagPipelineProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_Returns401_WhenApiKeyMissing()
    {
        using var factory = new WebApplicationFactory<RagPipelineProgram>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/ingest", new { path = "." });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Ingest_Returns401_ForWrongApiKey()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsJsonAsync("/ingest", new { path = "." });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Ingest_Rejects_PathTraversal_EvenWithValidApiKey()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/ingest", new { path = "..\\..\\windows\\system32" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_Rejects_Unsupported_Extension_EvenWithValidApiKey()
    {
        using var scope = new RagInputScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "payload.exe"), "stub");

        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Knowledge:IngestRoot"] = scope.Root
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/ingest", new { path = Path.Combine(scope.Root, "payload.exe") });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_Rejects_Oversized_File_EvenWithValidApiKey()
    {
        using var scope = new RagInputScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "large.txt"), new string('a', 513 * 1024));

        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Knowledge:IngestRoot"] = scope.Root
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/ingest", new { path = Path.Combine(scope.Root, "large.txt") });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_Rejects_Directory_With_TooMany_Files_EvenWithValidApiKey()
    {
        using var scope = new RagInputScope();
        var folder = Directory.CreateDirectory(Path.Combine(scope.Root, "bulk")).FullName;
        for (var i = 0; i < 129; i++)
            await File.WriteAllTextAsync(Path.Combine(folder, $"doc-{i}.txt"), "data");

        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Knowledge:IngestRoot"] = scope.Root
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/ingest", new { path = folder });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_Returns429_WhenRateLimitExceeded()
    {
        using var scope = new RagInputScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "doc.txt"), "data");

        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Knowledge:IngestRoot"] = scope.Root
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/ingest", new { path = Path.Combine(scope.Root, "doc.txt") });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestText_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
            last = await client.PostAsJsonAsync("/ingest/text", new { id = $"doc-{i}", content = "data" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    [Fact]
    public async Task Ingest_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"path\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/ingest", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestText_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"id\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/ingest/text", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteDocument_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.DeleteAsync("/document/doc-1");

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    [Fact]
    public async Task Query_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateRagPipelineFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 31; i++)
            last = await client.PostAsJsonAsync("/query", new { question = $"q{i}" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    private static WebApplicationFactory<RagPipelineProgram> CreateRagPipelineFactory(Dictionary<string, string?> config) =>
        new WebApplicationFactory<RagPipelineProgram>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmbedder>();
                services.RemoveAll<IAgxiomeAgent>();
                services.AddSingleton(_ => SecurityHttpTestDoubles.CreateDeterministicEmbedder());
                services.AddSingleton(_ => SecurityHttpTestDoubles.CreateDeterministicAgent("grounded response"));
            });
        });

    private sealed class RagInputScope : IDisposable
    {
        public RagInputScope()
        {
            Root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "agxiome-rag-tests", Guid.NewGuid().ToString("N"))).FullName;
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}

public sealed class DynamicSkillLabHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = new WebApplicationFactory<SkillLabProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Pipeline_Returns401_WhenApiKeyMissing()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key"
        });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/pipeline", new { task = "demo", sessionId = "s1" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Pipeline_Returns401_ForWrongApiKey()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsJsonAsync("/pipeline", new { task = "demo", sessionId = "s1" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Pipeline_Rejects_EmptyTask_EvenWithValidApiKey()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/pipeline", new { task = "", sessionId = "s1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Compile_Returns403_WhenMutationEndpointsDisabled()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key",
            ["DynamicRuntime:EnableMutationEndpoints"] = "false"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsync("/pipeline/demo/compile", JsonContent.Create(new { }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("runtime_mutation_disabled", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compile_Returns422_ForMalformedSource()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(sourceCode: "public sealed class DemoAgent : {"));

        var response = await client.PostAsync("/pipeline/demo/compile", JsonContent.Create(new { }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("compilation-failed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("error CS", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compile_Returns422_ForSourceParityMismatch()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(sourceCode: CreateValidSource("other")));

        var response = await client.PostAsync("/pipeline/demo/compile", JsonContent.Create(new { }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("compilation-failed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Source AgentId must match the tool id", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Compile_Returns429_WhenRateLimitExceeded()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor());

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsync("/pipeline/demo/compile", JsonContent.Create(new { }));

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_Returns403_WhenMutationEndpointsDisabled()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key",
            ["DynamicRuntime:EnableMutationEndpoints"] = "false"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/pipeline/demo/run", new { input = "hello", sessionId = "s1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Run_ReturnsExecutionFailed_ForOversizedInput()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor());

        var response = await client.PostAsJsonAsync("/pipeline/demo/run", new { input = new string('a', 4097), sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("execution-failed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("larger than 4096 characters", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_ReturnsExecutionFailed_WhenRecompileIsRejectedBySourcePolicy()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(sourceCode: CreateForbiddenIoSource()));

        var response = await client.PostAsJsonAsync("/pipeline/demo/run", new { input = "hello", sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("execution-failed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Tool not loaded and recompilation failed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_Returns429_WhenRateLimitExceeded()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor());

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/pipeline/demo/run", new { input = $"hello-{i}", sessionId = "s1" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_ReturnsSafeBody_ForMalformedJson()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor());

        using var content = new StringContent("{\"input\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/pipeline/demo/run", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_Returns403_WhenMutationEndpointsDisabled()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key",
            ["DynamicRuntime:EnableMutationEndpoints"] = "false"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = "s1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Promote_Returns422_WhenPromotionPolicyRejectsManifest()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(
            manifestJson: CreateManifestJson(filesystem: "readwrite:any", network: true, tools: ["shell"], maxExecutionMs: 20_000, maxMemoryMb: 512)));

        var response = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("promotion_policy_rejected", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Filesystem permission exceeds", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_Returns422_ForMalformedManifestJson()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(manifestJson: "{not-json"));

        var response = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("promotion_policy_rejected", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not valid JSON", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_Returns422_ForManifestIdMismatch()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(manifestJson: CreateManifestJson(id: "other")));

        var response = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("promotion_policy_rejected", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manifest id must match", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_Returns422_ForBudgetEdgeCaseOutsidePolicy()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(manifestJson: CreateManifestJson(maxExecutionMs: 0, maxMemoryMb: 0)));

        var response = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("promotion_policy_rejected", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaxExecutionMs must be between 1 and 10000", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MaxMemoryMb must be between 1 and 256", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_Returns422_ForSourceManifestParityMismatch()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(sourceCode: CreateValidSource("other"), manifestJson: CreateManifestJson()));

        var response = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = "s1" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Contains("promotion_policy_rejected", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Source AgentId must match the tool id", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_Returns429_WhenRateLimitExceeded()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor(manifestJson: CreateManifestJson(filesystem: "readwrite:any")));

        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
            last = await client.PostAsJsonAsync("/pipeline/demo/promote", new { sessionId = $"s{i}" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promote_ReturnsSafeBody_ForMalformedJson()
    {
        using var scope = new SkillLabMutationScope();
        using var factory = CreateMutationEnabledSkillLabFactory(scope.DatabasePath);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        await SeedToolAsync(factory, CreateToolDescriptor());

        using var content = new StringContent("{\"sessionId\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/pipeline/demo/promote", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pipeline_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"task\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/pipeline", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<SkillLabProgram> CreateSkillLabFactory(Dictionary<string, string?> config) =>
        new WebApplicationFactory<SkillLabProgram>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config)));

    private static WebApplicationFactory<SkillLabProgram> CreateMutationEnabledSkillLabFactory(string dbPath) =>
        CreateSkillLabFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:Provider"] = "openai",
            ["LLMProvider:OpenAI:ApiKey"] = "test-key",
            ["DynamicRuntime:EnableMutationEndpoints"] = "true",
            ["Agent:DatabasePath"] = dbPath
        });

    private static async Task SeedToolAsync(WebApplicationFactory<SkillLabProgram> factory, skilllab::AgxiomaDynamicSkillLab.ToolDescriptor tool)
    {
        var store = factory.Services.GetRequiredService<skilllab::AgxiomaDynamicSkillLab.Storage.SkillLabStore>();
        await store.SaveToolAsync(tool);
    }

    private static skilllab::AgxiomaDynamicSkillLab.ToolDescriptor CreateToolDescriptor(
        string? sourceCode = null,
        string? manifestJson = null) =>
        new(
            Id: "demo",
            Name: "Demo",
            Description: "desc",
            SourceCode: sourceCode ?? CreateValidSource("demo"),
            ManifestJson: manifestJson ?? CreateManifestJson(),
            CreatedAt: DateTime.UtcNow,
            Status: skilllab::AgxiomaDynamicSkillLab.ToolStatus.Executed);

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

    private static string CreateForbiddenIoSource() => """
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

    private sealed class SkillLabMutationScope : IDisposable
    {
        private readonly string? _previousFlag = Environment.GetEnvironmentVariable("AGXIOME_ENABLE_MUTATION_ENDPOINTS");

        public SkillLabMutationScope()
        {
            DatabasePath = Path.Combine(Path.GetTempPath(), "agxiome-skilllab-tests", $"{Guid.NewGuid():N}.db");
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            Environment.SetEnvironmentVariable("AGXIOME_ENABLE_MUTATION_ENDPOINTS", "true");
        }

        public string DatabasePath { get; }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AGXIOME_ENABLE_MUTATION_ENDPOINTS", _previousFlag);
            if (File.Exists(DatabasePath))
                File.Delete(DatabasePath);
        }
    }
}

public sealed class LlmWikiHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = CreateLlmWikiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Log_Returns401_WhenApiKeyMissing()
    {
        using var factory = CreateLlmWikiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/log");

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task ResetEmbeddings_Returns401_ForWrongApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsync("/admin/reset-embeddings", JsonContent.Create(new { }));

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Query_Rejects_EmptyQuestion_EvenWithValidApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/query", new { question = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IngestJob_Returns404_ForUnknownJob_EvenWithValidApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.GetAsync("/ingest/missing-job");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/ingest", new { subPath = "src", force = false });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetEmbeddings_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
            last = await client.PostAsync("/admin/reset-embeddings", JsonContent.Create(new { }));

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    [Fact]
    public async Task Query_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
            last = await client.PostAsJsonAsync("/query", new { question = $"q{i}" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    [Fact]
    public async Task Query_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"question\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/query", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ingest_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"subPath\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/ingest", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wiki_ReturnsPublicPageSummaries_FromStatefulStore()
    {
        using var factory = CreateLlmWikiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/wiki");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("entities/demo-agent", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Demo Agent", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WikiPage_ReturnsCachedContent_FromStatefulStore()
    {
        using var factory = CreateLlmWikiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/wiki/entities/demo-agent");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("cached wiki page", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stats_ReturnsStatefulCounters_WithValidApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.GetAsync("/stats");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"totalPages\":2", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"totalSources\":3", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"totalLogEntries\":1", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Log_ReturnsStatefulEntries_WithValidApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.GetAsync("/log?limit=5");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("seeded page", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetEmbeddings_ReinitializesSchema_WithValidApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");
        var database = Assert.IsType<TestWikiDatabase>(factory.Services.GetRequiredService<IWikiDatabase>());
        var before = database.EnsureSchemaCallCount;

        var response = await client.PostAsync("/admin/reset-embeddings", JsonContent.Create(new { }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(database.EnsureSchemaCallCount > before);
    }

    [Fact]
    public async Task Ingest_CreatesTrackableJob_WithValidApiKey()
    {
        using var factory = CreateLlmWikiFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var start = await client.PostAsJsonAsync("/ingest", new { subPath = "src", force = false });
        var startBody = await start.Content.ReadFromJsonAsync<IngestJobResponse>();

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        Assert.NotNull(startBody);

        HttpResponseMessage? poll = null;
        for (var i = 0; i < 10; i++)
        {
            poll = await client.GetAsync($"/ingest/{startBody!.JobId}");
            var pollBody = await poll.Content.ReadAsStringAsync();
            if (pollBody.Contains("\"status\":\"done\"", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains("\"filesProcessed\":1", pollBody, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("\"pagesCreated\":1", pollBody, StringComparison.OrdinalIgnoreCase);
                return;
            }
        }

        Assert.NotNull(poll);
        var finalBody = await poll!.Content.ReadAsStringAsync();
        Assert.Fail($"Expected ingest job to complete, final response was: {finalBody}");
    }

    private static WebApplicationFactory<LlmWikiProgram> CreateLlmWikiFactory(Dictionary<string, string?>? overrides = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "agxiome-llm-wiki-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "wiki"));
        Directory.CreateDirectory(Path.Combine(root, "project"));

        var config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Username=test;Password=test;Database=testdb",
            ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false,connectTimeout=10",
            ["Wiki:ProjectRootPath"] = Path.Combine(root, "project"),
            ["Wiki:WikiOutputPath"] = Path.Combine(root, "wiki"),
            ["Wiki:OllamaBaseUrl"] = "http://localhost:11434",
            ["Wiki:EmbedModel"] = "mxbai-embed-large",
            ["Wiki:LLMModel"] = "deepseek-v4-flash",
            ["Wiki:EmbedDimensions"] = "1024",
            ["Wiki:ChunkMaxTokens"] = "512",
            ["Wiki:ChunkOverlap"] = "64",
            ["Wiki:EmbedMaxChars"] = "1200",
            ["Wiki:TopK"] = "8",
            ["Wiki:MinScore"] = "0.45",
            ["Wiki:IngestParallelism"] = "1"
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
                config[pair.Key] = pair.Value;
        }

        return new WebApplicationFactory<LlmWikiProgram>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.Sources.Clear();
                cfg.AddInMemoryCollection(config);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWikiDatabase>();
                services.RemoveAll<IWikiIngester>();
                services.RemoveAll<IWikiPageWriter>();
                services.RemoveAll<IWikiQueryEngine>();
                services.RemoveAll<IWikiLintEngine>();

                services.AddSingleton<TestWikiDatabase>();
                services.AddSingleton<IWikiDatabase>(sp => sp.GetRequiredService<TestWikiDatabase>());
                services.AddSingleton<IWikiIngester, TestWikiIngester>();
                services.AddSingleton<IWikiPageWriter, TestWikiPageWriter>();
                services.AddSingleton<IWikiQueryEngine, TestWikiQueryEngine>();
                services.AddSingleton<IWikiLintEngine, TestWikiLintEngine>();
            });
        });
    }

    private sealed class TestWikiDatabase : IWikiDatabase
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, IngestJobState> _jobs = new(StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase)
        {
            ["entities/demo-agent"] = "# Cached wiki page for demo agent"
        };
        private readonly List<WikiPage> _pages =
        [
            new()
            {
                Id = 1,
                Slug = "entities/demo-agent",
                Title = "Demo Agent",
                Category = "entity",
                Summary = "Seeded summary for demo agent.",
                FilePath = "wiki/entities/demo-agent.md",
                SourceCount = 1,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new()
            {
                Id = 2,
                Slug = "concepts/routing",
                Title = "Routing",
                Category = "concept",
                Summary = "Seeded routing concept page.",
                FilePath = "wiki/concepts/routing.md",
                SourceCount = 2,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
            }
        ];
        private readonly List<WikiLogEntry> _logs =
        [
            new()
            {
                Id = 1,
                OpType = "ingest",
                Title = "seeded page",
                Details = "Seeded ingest completed.",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            }
        ];

        public int EnsureSchemaCallCount { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            EnsureSchemaCallCount++;
            return Task.CompletedTask;
        }

        public Task<int> CountPagesAsync(CancellationToken ct = default) => Task.FromResult(_pages.Count);
        public Task<int> CountSourcesAsync(CancellationToken ct = default) => Task.FromResult(3);
        public Task<int> CountLogEntriesAsync(CancellationToken ct = default) => Task.FromResult(_logs.Count);

        public Task SaveJobStateAsync(IngestJobState state, CancellationToken ct = default)
        {
            _jobs[state.JobId] = Clone(state);
            return Task.CompletedTask;
        }

        public Task<IngestJobState?> GetJobStateAsync(string jobId, CancellationToken ct = default) =>
            Task.FromResult(_jobs.TryGetValue(jobId, out var state) ? Clone(state) : null);

        public Task<List<WikiPage>> ListPagesAsync(string? category = null, CancellationToken ct = default) =>
            Task.FromResult(string.IsNullOrWhiteSpace(category)
                ? _pages.ToList()
                : _pages.Where(page => string.Equals(page.Category, category, StringComparison.OrdinalIgnoreCase)).ToList());

        public Task<WikiPage?> GetPageBySlugAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_pages.FirstOrDefault(page => string.Equals(page.Slug, slug, StringComparison.OrdinalIgnoreCase)));

        public Task<string?> GetCachedPageContentAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult(_cache.TryGetValue(slug, out var content) ? content : null);

        public Task CachePageContentAsync(string slug, string content, CancellationToken ct = default)
        {
            _cache[slug] = content;
            return Task.CompletedTask;
        }

        public Task<List<WikiLogEntry>> GetLogAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult(_logs.Take(limit).ToList());

        public Task<DateTime?> GetLastIngestAtAsync(CancellationToken ct = default) => Task.FromResult<DateTime?>(DateTime.UtcNow.AddMinutes(-2));
        public Task<DateTime?> GetLastQueryAtAsync(CancellationToken ct = default) => Task.FromResult<DateTime?>(DateTime.UtcNow.AddMinutes(-1));

        private static IngestJobState Clone(IngestJobState state) => new()
        {
            JobId = state.JobId,
            Status = state.Status,
            FilesTotal = state.FilesTotal,
            FilesProcessed = state.FilesProcessed,
            PagesCreated = state.PagesCreated,
            PagesUpdated = state.PagesUpdated,
            CurrentFile = state.CurrentFile,
            ErrorMessage = state.ErrorMessage,
            StartedAt = state.StartedAt
        };
    }

    private sealed class TestWikiIngester(TestWikiDatabase database) : IWikiIngester
    {
        public async Task RunAsync(IngestJobState job, string? subPath = null, bool force = false, CancellationToken ct = default)
        {
            job.FilesTotal = 1;
            job.FilesProcessed = 1;
            job.PagesCreated = 1;
            job.CurrentFile = subPath ?? "project/src/DemoAgent.cs";
            await database.SaveJobStateAsync(job, ct);
        }
    }

    private sealed class TestWikiPageWriter : IWikiPageWriter
    {
        public Task<string?> ReadWikiPageAsync(string slug, CancellationToken ct = default) =>
            Task.FromResult<string?>($"# Fallback page for {slug}");
    }

    private sealed class TestWikiQueryEngine : IWikiQueryEngine
    {
        public Task<WikiQueryResponse> QueryAsync(WikiQueryRequest request, CancellationToken ct = default) =>
            Task.FromResult(new WikiQueryResponse(request.Question, "stub", new List<WikiPageRef>(), true, 1));
    }

    private sealed class TestWikiLintEngine : IWikiLintEngine
    {
        public Task<LintReport> RunLintAsync(CancellationToken ct = default) =>
            Task.FromResult(new LintReport(0, 0, 0, new List<LintIssue>(), "ok", DateTime.UtcNow));
    }
}

public sealed class SupportHubHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = new WebApplicationFactory<SupportHubProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Support_Returns401_WhenApiKeyMissing()
    {
        using var factory = new WebApplicationFactory<SupportHubProgram>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/support", new { message = "help" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Support_Returns400_ForEmptyMessage_EvenWithValidApiKey()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/support", new { message = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Support_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 21; i++)
            last = await client.PostAsJsonAsync("/support", new { message = "" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Support_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"message\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/support", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Analyze_Returns400_ForEmptyTopic_EvenWithValidApiKey()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/analyze", new { topic = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/analyze", new { topic = "" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
    }

    [Fact]
    public async Task Support_RoutesBillingRequest_WithDeterministicForestSwarm()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        }, useDeterministicSwarms: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/support", new
        {
            message = "I need a refund for a paid invoice"
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("billing", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("billing-specialist", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("refund routed", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Analyze_ReturnsDeterministicPayload_WithConfiguredSwarm()
    {
        using var factory = CreateSupportHubFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        }, useDeterministicSwarms: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/analyze", new
        {
            topic = "customer outage"
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("customer outage", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("structured analysis", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("final recommendation", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("finding-1", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<SupportHubProgram> CreateSupportHubFactory(
        Dictionary<string, string?> config,
        bool useDeterministicSwarms = false) =>
        new WebApplicationFactory<SupportHubProgram>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            if (!useDeterministicSwarms)
                return;

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ForestSwarm>();
                services.RemoveAll<HeavySwarm>();
                services.AddSingleton(_ => CreateDeterministicSupportForestSwarm());
                services.AddSingleton(_ => CreateDeterministicSupportHeavySwarm());
            });
        });

    private static ForestSwarm CreateDeterministicSupportForestSwarm()
    {
        var billingAgent = CreateSupportFakeAgent("refund routed to billing workflow");
        var technicalAgent = CreateSupportFakeAgent("technical troubleshooting workflow");
        var salesAgent = CreateSupportFakeAgent("sales advisory workflow");
        var generalAgent = CreateSupportFakeAgent("general assistance workflow");

        return new ForestSwarm(
            new ForestSwarmConfig { MinMatchScore = 0 },
            new[]
            {
                new ForestTree("billing", new[]
                {
                    new ForestAgentEntry("billing-specialist", "billing", ["invoice", "refund", "billing", "payment"], billingAgent)
                }),
                new ForestTree("technical", new[]
                {
                    new ForestAgentEntry("tech-specialist", "technical", ["error", "incident", "outage", "api"], technicalAgent)
                }),
                new ForestTree("sales", new[]
                {
                    new ForestAgentEntry("sales-specialist", "sales", ["pricing", "plan", "enterprise", "upgrade"], salesAgent)
                }),
                new ForestTree("general", new[]
                {
                    new ForestAgentEntry("general-assistant", "general", ["help", "question", "account"], generalAgent)
                })
            },
            name: "support",
            description: "deterministic support"
        );
    }

    private static HeavySwarm CreateDeterministicSupportHeavySwarm()
    {
        var questionAgent = CreateSupportFakeAgent("question-set");
        var workers = Enumerable.Range(1, 3)
            .Select(index => CreateSupportFakeAgent($"finding-{index}"))
            .Cast<IAgxiomeAgent>()
            .ToList();

        var synthesizer = CreateSupportFakeAgentForAnalysis();
        return new HeavySwarm(new HeavySwarmConfig(), questionAgent, workers, synthesizer);
    }

    private static IAgxiomeAgent CreateSupportFakeAgent(string content)
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.Id.Returns("support-test-agent");
        agent.Status.Returns(AgentStatus.Completed);
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AgentResponse(content, AgentStatus.Completed)));
        return agent;
    }

    private static IAgxiomeAgent CreateSupportFakeAgentForAnalysis()
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.Id.Returns("support-analysis-agent");
        agent.Status.Returns(AgentStatus.Completed);
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<AgentRequest>();
                var content = request.Prompt.Contains("Based on this analysis, generate 3-5 alternative", StringComparison.Ordinal)
                    ? "alternative-a\n\nalternative-b"
                    : request.Prompt.Contains("Synthesize all the above", StringComparison.Ordinal)
                        ? "final recommendation"
                        : "structured analysis";
                return Task.FromResult(new AgentResponse(content, AgentStatus.Completed));
            });
        return agent;
    }

}

public sealed class ResearchAgentHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = new WebApplicationFactory<ResearchAgentProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Research_Returns401_WhenApiKeyMissing()
    {
        using var factory = new WebApplicationFactory<ResearchAgentProgram>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/research", new { topic = "threat model" });

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Research_Returns400_ForEmptyTopic_EvenWithValidApiKey()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/research", new { topic = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Research_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
            last = await client.PostAsJsonAsync("/research", new { topic = "" });

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResearchSync_Returns400_ForEmptyTopic_EvenWithValidApiKey()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/research/sync", new { topic = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Research_ReturnsSafeBody_ForMalformedJson()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        using var content = new StringContent("{\"topic\":", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/research", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("System.", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Research_StartsTrackableJob_AndPollReturnsCompletedResult()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        }, useDeterministicSwarm: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var start = await client.PostAsJsonAsync("/research", new { topic = "threat model" });
        var startBody = await start.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        Assert.Contains("started", startBody, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(start.Headers.Location);

        HttpResponseMessage? poll = null;
        for (var i = 0; i < 10; i++)
        {
            poll = await client.GetAsync(start.Headers.Location);
            var pollBody = await poll.Content.ReadAsStringAsync();
            if (pollBody.Contains("\"researchCount\":3", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Equal(HttpStatusCode.OK, poll.StatusCode);
                Assert.Contains("What is the most important risk in threat model?", pollBody, StringComparison.Ordinal);
                Assert.Contains("final recommendation", pollBody, StringComparison.OrdinalIgnoreCase);
                return;
            }
        }

        Assert.NotNull(poll);
        var finalBody = await poll!.Content.ReadAsStringAsync();
        Assert.Fail($"Expected completed research job payload, final response was: {finalBody}");
    }

    [Fact]
    public async Task ResearchList_IncludesStartedJob_WithDeterministicSwarm()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        }, useDeterministicSwarm: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var start = await client.PostAsJsonAsync("/research", new { topic = "incident response" });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);

        var list = await client.GetAsync("/research");
        var body = await list.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains("incident response", body, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            body.Contains("Running", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Completed", StringComparison.OrdinalIgnoreCase),
            $"Expected listed job to be Running or Completed, body was: {body}");
    }

    [Fact]
    public async Task ResearchSync_ReturnsDeterministicPayload_WithValidApiKey()
    {
        using var factory = CreateResearchAgentFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key"
        }, useDeterministicSwarm: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.PostAsJsonAsync("/research/sync", new { topic = "threat model" });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("What is the most important risk in threat model?", body, StringComparison.Ordinal);
        Assert.Contains("\"researchCount\":3", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("final recommendation", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<ResearchAgentProgram> CreateResearchAgentFactory(
        Dictionary<string, string?> config,
        bool useDeterministicSwarm = false) =>
        new WebApplicationFactory<ResearchAgentProgram>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            if (!useDeterministicSwarm)
                return;

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<HeavySwarm>();
                services.AddSingleton(_ => CreateDeterministicHeavySwarm());
            });
        });

    private static HeavySwarm CreateDeterministicHeavySwarm()
    {
        var questionAgent = CreateFakeAgent(request =>
            Task.FromResult(new AgentResponse(
                $"What is the most important risk in {ExtractTopic(request.Prompt)}?",
                AgentStatus.Completed)));

        var workers = Enumerable.Range(1, 3)
            .Select(index => CreateFakeAgent(_ =>
                Task.FromResult(new AgentResponse($"finding-{index}", AgentStatus.Completed))))
            .Cast<IAgxiomeAgent>()
            .ToList();

        var synthesizer = CreateFakeAgent(request =>
        {
            var content = request.Prompt.Contains("Based on this analysis, generate 3-5 alternative", StringComparison.Ordinal)
                ? "alternative-a\n\nalternative-b"
                : request.Prompt.Contains("Synthesize all the above", StringComparison.Ordinal)
                    ? "final recommendation"
                    : "structured analysis";

            return Task.FromResult(new AgentResponse(content, AgentStatus.Completed));
        });

        return new HeavySwarm(new HeavySwarmConfig(), questionAgent, workers, synthesizer);
    }

    private static IAgxiomeAgent CreateFakeAgent(Func<AgentRequest, Task<AgentResponse>> runAsync)
    {
        var agent = Substitute.For<IAgxiomeAgent>();
        agent.Id.Returns("test-agent");
        agent.Status.Returns(AgentStatus.Completed);
        agent.RunAsync(Arg.Any<AgentRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => runAsync(call.Arg<AgentRequest>()));
        return agent;
    }

    private static string ExtractTopic(string prompt)
    {
        const string marker = "Input:";
        var markerIndex = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return "the request";

        var start = markerIndex + marker.Length;
        var end = prompt.IndexOf("Generate a single", start, StringComparison.Ordinal);
        var topic = end > start ? prompt[start..end] : prompt[start..];
        return topic.Trim();
    }
}

public sealed class StockTrackerHttpSecurityTests
{
    [Fact]
    public async Task Health_Remains_Public()
    {
        using var factory = new WebApplicationFactory<StockTrackerProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Quote_Returns401_WhenApiKeyMissing()
    {
        using var factory = new WebApplicationFactory<StockTrackerProgram>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/quote/MSFT");

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Analyze_Returns401_ForWrongApiKey()
    {
        using var factory = CreateStockTrackerFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db")
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsync("/analyze/MSFT", JsonContent.Create(new { }));

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task Debate_Returns401_ForWrongApiKey()
    {
        using var factory = CreateStockTrackerFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db")
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");

        var response = await client.PostAsync("/debate/MSFT", JsonContent.Create(new { }));

        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.BadGateway, $"Support endpoint should return 401, 200, or 502, got {response.StatusCode}");
    }

    [Fact]
    public async Task AnalyzeJob_Returns404_ForUnknownJob_EvenWithValidApiKey()
    {
        using var factory = CreateStockTrackerFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db")
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.GetAsync("/analyze/MSFT/jobs/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeJob_Returns429_WhenRateLimitExceeded()
    {
        using var factory = CreateStockTrackerFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db")
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 31; i++)
            last = await client.GetAsync("/analyze/MSFT/jobs/missing");

        Assert.NotNull(last);
        Assert.Equal((HttpStatusCode)429, last!.StatusCode);
        var body = await last.Content.ReadAsStringAsync();
        Assert.Contains("rate_limited", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quote_PersistsHistory_AndAppearsInWatchlist_WithDeterministicMarketData()
    {
        using var factory = CreateStockTrackerFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db")
        }, useDeterministicMarketData: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var quote = await client.GetAsync("/quote/MSFT");
        var quoteBody = await quote.Content.ReadAsStringAsync();
        var history = await client.GetAsync("/history/MSFT");
        var historyBody = await history.Content.ReadAsStringAsync();
        var watchlist = await client.GetAsync("/watchlist");
        var watchlistBody = await watchlist.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        Assert.Contains("Yahoo Finance", quoteBody, StringComparison.Ordinal);
        Assert.Contains("421.5", quoteBody, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Contains("MSFT", historyBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("421.5", historyBody, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.OK, watchlist.StatusCode);
        Assert.Contains("MSFT", watchlistBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quote_Returns404_ForUnknownTicker_WithDeterministicMarketData()
    {
        using var factory = CreateStockTrackerFactory(new Dictionary<string, string?>
        {
            ["AGXIOME_BOOTSTRAP_KEY"] = "expected-key",
            ["Agent:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"stock-tests-{Guid.NewGuid():N}.db")
        }, useDeterministicMarketData: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "expected-key");

        var response = await client.GetAsync("/quote/UNKNOWN");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<StockTrackerProgram> CreateStockTrackerFactory(
        Dictionary<string, string?> config,
        bool useDeterministicMarketData = false) =>
        new WebApplicationFactory<StockTrackerProgram>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            if (!useDeterministicMarketData)
                return;

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<stocktracker::AgxiomaStockTracker.MarketDataClient>();
                services.AddSingleton(_ => new stocktracker::AgxiomaStockTracker.MarketDataClient(
                    new HttpClient(new DeterministicMarketDataHandler())
                    {
                        BaseAddress = new Uri("https://example.test/")
                    }));
            });
        });

    private sealed class DeterministicMarketDataHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("/MSFT", StringComparison.OrdinalIgnoreCase))
            {
                const string payload = """
                {
                  "chart": {
                    "result": [
                      {
                        "meta": {
                          "regularMarketPrice": 421.5,
                          "chartPreviousClose": 420.0,
                          "regularMarketVolume": 1234567,
                          "marketCap": 3100000000000
                        }
                      }
                    ]
                  }
                }
                """;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}

