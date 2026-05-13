using System.Runtime.CompilerServices;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Agent;

/// <summary>Creates configured agent instances. Central factory for all agent creation.</summary>
public static class AgentFactory
{
    private static ILoggerFactory? _loggerFactory;

    public static void Configure(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;

    public static IAgxiomeAgent Create(AgentConfig config)
    {
        var log = _loggerFactory?.CreateLogger<AgxiomeAgent>() ?? NullLogger<AgxiomeAgent>.Instance;
        var engine = CreateEngine(config.ReasoningEngine);
        return new AgxiomeAgent(config.Id, engine, config, log);
    }

    public static IAgxiomeAgent Create(string name, Action<AgentConfig>? configure = null)
    {
        var config = new AgentConfig { Id = name };
        configure?.Invoke(config);
        return Create(config);
    }

    /// <summary>Create agent without LLM engine (for testing).</summary>
    public static IAgxiomeAgent CreateTestAgent(string name = "test-agent")
    {
        var log = NullLogger<AgxiomeAgent>.Instance;
        var engine = new TestLLMWIKYEngine();
        return new AgxiomeAgent(name, engine, new AgentConfig(), log);
    }

    /// <summary>
    /// Create agent with a pre-built engine instance.
    /// Use this when the caller owns the engine (e.g. CLI demo wiring LLMWIKY directly).
    /// </summary>
    public static IAgxiomeAgent CreateWithEngine(AgentConfig config, ILLMWIKYEngine engine)
    {
        var log = _loggerFactory?.CreateLogger<AgxiomeAgent>() ?? NullLogger<AgxiomeAgent>.Instance;
        return new AgxiomeAgent(config.Id, engine, config, log);
    }

    private static ILLMWIKYEngine CreateEngine(LLMWIKYConfig llmConfig)
    {
        throw new NotImplementedException("LLMWIKYEngine not yet configured. Install AGXIOME.LLMWIKY and wire via AgentFactory.Configure().");
    }
}

/// <summary>Test LLMWIKY engine that returns fixed responses.</summary>
internal sealed class TestLLMWIKYEngine : ILLMWIKYEngine
{
    public string FixedResponse { get; set; } = "Test response";

    public Task<LLMWIKYResponse> InferAsync(LLMWIKYRequest request, CancellationToken ct = default)
        => Task.FromResult(new LLMWIKYResponse(FixedResponse));

    public async IAsyncEnumerable<LLMWIKYChunk> InferStreamAsync(LLMWIKYRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new LLMWIKYChunk(FixedResponse, IsFinal: true);
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<float[][]> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default) => throw new NotImplementedException();
    public void RegisterTools(IReadOnlyList<System.Text.Json.Nodes.JsonObject> toolSchemas) { }
}

/// <summary>Logger that does nothing (avoids null checks).</summary>
internal sealed class NullLogger<T> : ILogger<T>
{
    public static readonly NullLogger<T> Instance = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}
