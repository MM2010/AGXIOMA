# P22 — Testing Strategy

## File: `Tests/` (xUnit + Testcontainers)

## Pattern

```csharp
// Tutti test in: AGXIOMA.Tests
// Fixture: TestContainers (PostgreSQL, Redis)
// Mock: NSubstitute per ILLMProvider
// Property: FsCheck per roundtrip

[CollectionDefinition("Storage")]
public class StorageCollection : ICollectionFixture<PostgresFixture> { }

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("agxiome_test")
        .Build();

    public string ConnectionString => _pg.GetConnectionString();

    public async Task InitializeAsync() => await _pg.StartAsync();
    public async Task DisposeAsync() => await _pg.DisposeAsync();
}
```

## Test per modulo

| Modulo | Tipo | Cosa testare |
|--------|------|-------------|
| Core Agent | Unit | Lifecycle, status, cancellation |
| Agent Run | Unit | Sync, streaming, event stream |
| Tool Registry | Unit | Register, resolve, schema |
| LLMWIKY | Unit | Mock provider, streaming, fallback |
| Provider | Integration | Connessione reale o mock HTTP |
| Teams | Unit | Mode coordinate/route/broadcast |
| Workflows | Unit | DAG, condition, loop, parallel |
| Storage | Integration | PostgreSQL CRUD, transactions |
| RAG | Integration | pgvector ANN, chunking |
| Session | Integration | Create, persist, resume |
| Context | Unit | Instructions, dependencies, few-shot |
| Learning | Integration | Memory save/retrieve |
| Guardrails | Unit | PII, injection, moderation |
| Hooks | Unit | Pre/post, background, tool hook |
| HITL | Integration | Pause, confirm, reject, resume |
| Skills | Integration | Apply, remote download |
| Scheduler | Integration | Cron, poller execution |
| X-Link | Unit | Envelope roundtrip, HMAC, compression |
| Realtime | Integration | SignalR hub, group, reconnect |
| Security | Unit | Sign/verify, HMAC, rate-limit |
| Monitoring | Unit | Meter increment, health check |
| End-to-End | Integration | Agente completo con DB + LLM mock |

## Replay test (determinismo)

```csharp
[Fact]
public async Task SameSeed_SameResult()
{
    var config = new AgentConfig { /* SessionSeed = "fixed" */ };
    var agent1 = factory.CreateAgent(config);
    var agent2 = factory.CreateAgent(config with { /* stesso seed */ });

    var r1 = await agent1.RunAsync(new AgentRequest("test"));
    var r2 = await agent2.RunAsync(new AgentRequest("test"));

    Assert.Equal(r1.Content, r2.Content);
    Assert.Equal(r1.Metadata.InferenceCount, r2.Metadata.InferenceCount);
}
```

## Chaos test

```csharp
[Fact]
public async Task RedisDown_FallbackToDB()
{
    // Testcontainers: stop redis container
    // Run agent con cache miss
    // Verifica che legga da PostgreSQL
}

[Fact]
public async Task LLMTimeout_RetryThenFallback()
{
    // Mock HTTP che ritorna 408 dopo 30s
    // Verifica retry 3x, poi fallback provider
}
```

## Coverage target

| Layer | Coverage |
|-------|----------|
| Core Agent | ≥ 90% |
| Tool Registry | ≥ 95% |
| Storage | ≥ 85% |
| RAG | ≥ 80% |
| Workflows | ≥ 85% |
| X-Link | ≥ 90% |
| Security | ≥ 95% |
| Realtime | ≥ 80% |
