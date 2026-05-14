# 11 — Testing Strategy

## Scopo

Copertura completa: unit → integration → replay → chaos.
Replay determinista per validare riproducibilità agente.

## Piramide test

```
         ╱╲
        ╱  ╲
       ╱Chaos╲         ← Fault injection, network partition
      ╱────────╲
     ╱  Replay  ╲      ← Deterministic replay verification
    ╱────────────╲
   ╱  Integration ╲    ← DB, Redis, real, LLM mock
  ╱────────────────╲
 ╱    Unit Tests    ╲  ← Isolati, mock everything
╱────────────────────╲
```

## Unit test

```csharp
[TestFixture]
public class AgentEngineTests
{
    [Test]
    public async Task RunAsync_SimplePrompt_ReturnsResponse()
    {
        // Arrange
        var engine = Substitute.For<ILLMWIKYEngine>();
        engine.InferAsync(Arg.Any<LLMWIKYRequest>(), Arg.Any<CancellationToken>())
              .Returns(new LLMWIKYResponse("Hello, world!"));
        
        var agent = new AgxiomeAgent("test", engine);

        // Act
        var result = await agent.RunAsync(new AgentRequest("Say hello"));

        // Assert
        Assert.That(result.Content, Is.EqualTo("Hello, world!"));
        Assert.That(result.Status, Is.EqualTo(AgentStatus.Completed));
    }

    [Test]
    public void RegisterFunction_ValidType_AddsToRegistry()
    {
        var agent = CreateTestAgent();
        agent.RegisterFunction<MockTool>();
        Assert.That(agent.ToolRegistry.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task RunAsync_MaxIterationsExceeded_ReturnsError()
    {
        // Agent che chiama sempre tool senza mai rispondere
        var agent = CreateAgentThatAlwaysCallsTool();
        var result = await agent.RunAsync(new AgentRequest("do something"));
        
        Assert.That(result.Status, Is.EqualTo(AgentStatus.Error));
        Assert.That(result.Error, Contains.Substring("max iterations"));
    }
}
```

## Integration test

```csharp
[TestFixture]
public class StorageIntegrationTests
{
    private PostgresContainer _pg;
    private RedisContainer _redis;

    [OneTimeSetUp]
    public async Task Setup()
    {
        // TestContainers: PostgreSQL + Redis reali
        _pg = new PostgresBuilder()
            .WithImage("postgres:16")
            .WithDatabase("agxiome_test")
            .Build();
        await _pg.StartAsync();

        _redis = new RedisBuilder()
            .WithImage("redis:7")
            .Build();
        await _redis.StartAsync();
    }

    [Test]
    public async Task SaveAndQueryMemory_ReturnsCorrectData()
    {
        var store = new PostgresMemoryStore(_pg.GetConnectionString());
        var interaction = new AgentInteraction("agent-1", "user", "test message");

        await store.SaveInteractionAsync(interaction, CancellationToken.None);
        var history = await store.GetHistoryAsync("agent-1", limit: 10);

        Assert.That(history.Count, Is.EqualTo(1));
        Assert.That(history[0].Content, Is.EqualTo("test message"));
    }

    [Test]
    public async Task SemanticSearch_WithEmbeddings_ReturnsRelevantResults()
    {
        // Inserisci documenti con embedding
        // Cerca con query simile
        // Verifica ranking corretto
    }
}
```

## Replay test (determinismo)

```csharp
[TestFixture]
public class ReplayTests
{
    [Test]
    public async Task SameSeed_ProducesSameExecution()
    {
        // 1. Crea agente con seed fisso
        var config = new AgentConfig { SessionSeed = "test-seed-001" };
        var agent1 = CreateAgent(config);
        var agent2 = CreateAgent(config); // Stesso seed

        // 2. Esegui stesso prompt
        var result1 = await agent1.RunAsync("Analyze Q4 data");
        var result2 = await agent2.RunAsync("Analyze Q4 data");

        // 3. Output identici (determinismo)
        Assert.That(result1.Content, Is.EqualTo(result2.Content));
        Assert.That(result1.ToolResults, Is.EqualTo(result2.ToolResults));
        Assert.That(result1.Metadata.InferenceCount, Is.EqualTo(result2.Metadata.InferenceCount));
    }

    [Test]
    public async Task DifferentSeed_ProducesDifferentExecution()
    {
        var agent1 = CreateAgent(new AgentConfig { SessionSeed = "seed-a" });
        var agent2 = CreateAgent(new AgentConfig { SessionSeed = "seed-b" });

        var result1 = await agent1.RunAsync("Same prompt");
        var result2 = await agent2.RunAsync("Same prompt");

        // Output diversi (o tool path diverso)
        Assert.That(result1.Content, Is.Not.EqualTo(result2.Content));
    }
}
```

## Chaos test

| Scenario | Tool | Comportamento atteso |
|----------|------|---------------------|
| Redis crash | `chaos.TerminateContainer("redis")` | Cache miss → fallback a PostgreSQL |
| PostgreSQL disconnect | `chaos.TerminateContainer("pg")` | Retry + circuit breaker |
| LLM timeout | `chaos.HttpTimeout("llm-endpoint")` | Retry 3x, poi error graceful |
| Network partition | `chaos.Partition("agent-1", "agent-2")` | Buffer messaggi, reconnect |
| High latency DB | `chaos.AddLatency("pg", 2000ms)` | Slow but eventual completion |
| Throttled LLM | `chaos.RateLimit("llm-endpoint", 429)` | Exponential backoff + retry |

## Property-based test

```csharp
[TestFixture]
public class XLinkPropertyTests
{
    [Test]
    public void EnvelopeRoundtrip_AnyPayload_Unchanged()
    {
        Prop.ForAll(
            Arb.Default.ByteArray(),
            payload =>
            {
                var envelope = new XLinkEnvelope
                {
                    Header = new XLinkHeader { MessageId = Guid.NewGuid().ToString() },
                    Payload = payload
                };
                envelope.Checksum = ComputeHMAC(payload, testKey);

                var serialized = Serialize(envelope);
                var deserialized = Deserialize(serialized);

                return deserialized.Payload.SequenceEqual(payload)
                       && deserialized.Checksum == envelope.Checksum;
            }
        ).QuickCheckThrowOnFailure();
    }
}
```

## Coverage targets

| Layer | Coverage | Note |
|-------|----------|------|
| Core agent | 90%+ | Tutti i path di execution |
| Tool registry | 95%+ | Reflection + schema generation |
| Storage (CQRS) | 85%+ | Mock DB per unità |
| RAG pipeline | 80%+ | Integration con pgvector reale |
| Workflow engine | 85%+ | DAG execution + error path |
| X-Link transport | 90%+ | Roundtrip, checksum, compression |
| Security | 95%+ | Crypto, HMAC, rate-limit |
| SignalR hubs | 80%+ | Integration con backplane |
