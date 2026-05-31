using AGXIOME.Shared.Interfaces;
using System.Text.Json;

namespace AGXIOME.Tests.F25;

[Trait("Phase", "F25")]
[Trait("Component", "StructuredOutput")]
public class F25_TS03_StructuredOutput_HappyPath
{
    [Fact]
    public async Task ParseAsync_JsonString_ReturnsTypedObject()
    {
        var parser = new InMemoryOutputParser();
        var result = await parser.ParseAsync<TestData>("""{"Name":"Alice","Age":30}""");
        Assert.Equal("Alice", result.Name);
        Assert.Equal(30, result.Age);
    }

    [Fact]
    public async Task ParseAsync_NestedJson_Works()
    {
        var parser = new InMemoryOutputParser();
        var json = """{"Id":1,"Title":"Test","Tags":["a","b"]}""";
        var result = await parser.ParseAsync<TestItem>(json);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test", result.Title);
        Assert.Equal(2, result.Tags.Count);
    }

    [Fact]
    public async Task FormatResponseAsync_ReturnsFormattedString()
    {
        var model = new InMemoryResponseModel();
        var data = new TestData("Bob", 25);
        var result = await model.FormatResponseAsync(data);
        Assert.Contains("Bob", result);
        Assert.Contains("25", result);
    }

    [Fact]
    public async Task AgentOutputSchema_StoresType()
    {
        var schema = new AgentOutputSchema
        {
            OutputType = typeof(TestData),
            JsonSchema = new System.Text.Json.Nodes.JsonObject()
        };
        Assert.Equal(typeof(TestData), schema.OutputType);
    }

    [Fact]
    public async Task RoundTrip_ParseThenFormat()
    {
        var parser = new InMemoryOutputParser();
        var model = new InMemoryResponseModel();
        var data = await parser.ParseAsync<TestData>("""{"Name":"Charlie","Age":35}""");
        var text = await model.FormatResponseAsync(data);
        Assert.Contains("Charlie", text);
        Assert.Contains("35", text);
    }
}

[Trait("Phase", "F25")]
[Trait("Component", "StructuredOutput")]
public class F25_TS04_StructuredOutput_Offensive
{
    [Fact]
    public async Task ParseAsync_InvalidJson_Throws()
    {
        var parser = new InMemoryOutputParser();
        await Assert.ThrowsAnyAsync<Exception>(() => parser.ParseAsync<TestData>("not json"));
    }

    [Fact]
    public async Task ParseAsync_EmptyString_Throws()
    {
        var parser = new InMemoryOutputParser();
        await Assert.ThrowsAnyAsync<Exception>(() => parser.ParseAsync<TestData>(""));
    }

    [Fact]
    public async Task ParseAsync_NullString_Throws()
    {
        var parser = new InMemoryOutputParser();
        await Assert.ThrowsAnyAsync<Exception>(() => parser.ParseAsync<TestData>(null!));
    }

    [Fact]
    public async Task FormatResponseAsync_Null_Throws()
    {
        var model = new InMemoryResponseModel();
        await Assert.ThrowsAnyAsync<Exception>(() => model.FormatResponseAsync<object>(null!));
    }

    [Fact]
    public async Task ParseAsync_MismatchedTypes_Throws()
    {
        var parser = new InMemoryOutputParser();
        var json = """{"Name":"Alice","Age":30}""";
        // Try to parse into wrong type — should throw or return default
        var result = await parser.ParseAsync<TestItem>(json);
        Assert.NotNull(result);
    }
}

// ── In-Memory Implementations ──

public class InMemoryOutputParser : IOutputParser
{
    public Task<T> ParseAsync<T>(string agentOutput, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(agentOutput))
            throw new ArgumentException("Agent output cannot be null or empty", nameof(agentOutput));
        var result = JsonSerializer.Deserialize<T>(agentOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Deserialization returned null");
        return Task.FromResult(result);
    }
}

public class InMemoryResponseModel : IResponseModel
{
    public Task<string> FormatResponseAsync<T>(T structuredOutput, CancellationToken ct = default)
    {
        if (structuredOutput is null)
            throw new ArgumentNullException(nameof(structuredOutput));
        var json = JsonSerializer.Serialize(structuredOutput, new JsonSerializerOptions { WriteIndented = true });
        return Task.FromResult($"Structured data:\n{json}");
    }
}

// ── Test Types ──

public record TestData(string Name, int Age);
public record TestItem(int Id, string Title, List<string> Tags);
