# Testing — AGXIOME

164 test, 0 falliti. Strategia di testing completa.

---

## Panoramica

| File | Test | Cosa copre |
|------|------|-----------|
| `AgentCoreTests.cs` | 10 | Agent base, tool, guardrail, HITL, session |
| `AdvancedTests.cs` | 17 | Workflow, context, chunker, cost, skill, schedule |
| `SharedTests.cs` | 7 | Record, enum, configurazione default |
| `AgentEdgeTests.cs` | 10 | Empty prompt, concorrenza, hook, guardrail |
| `ToolRegistryEdgeTests.cs` | 6 | Duplicati, null, schema vuoto, isolation |
| `WorkflowEdgeTests.cs` | 8 | Cicli, eccezioni, 100 parallel, router, CEL |
| `TeamTests.cs` | 4 | Empty team, route, broadcast, coordinate |
| `SessionHITLLearningTests.cs` | 12 | Session CRUD, state, context, learning, skill |
| `ProviderXLinkSecurityChunkTests.cs` | 16 | Cost, XLink, crypto, audit, chunkers |
| `PropertyBasedTests.cs` | 13 | Payload casuali, HMAC, ECDsa, roundtrip |
| `ExceptionCoverageTests.cs` | 38 | Null, concorrenza, invalid state, dispose |
| `ResilienceTests.cs` | 23 | Retry, circuit breaker, fallback, error types |
| `ErrorSystemTests.cs` | 8 | Errori strutturati, stack nascosto, catalog |
| **Totale** | **164** | |

## Tipi di test

### Unit test
Ogni modulo testato isolatamente con mock.

### Edge test
Valori limite: `null`, `""`, `int.MaxValue`, timeout 0ms, cancellazione immediata.

### Concurrency test
Race condition su `SessionState`, `ToolRegistry`, `Agent` esecuzione concorrente.

### Property-based test
Payload casuali (byte[]), grandi (10MB), HMAC determinismo, ECDsa roundtrip.

### Resilience test
429/500/502/503, timeout, circuit breaker, fallback chain, error classification.

### Security test
Stack trace mai esposto, messaggi user-safe, codici errore categorizzati.

## Esecuzione

```bash
cd src
dotnet test                # Esegue tutti i 164 test
dotnet test --verbosity detailed  # Output dettagliato
```

## Aggiungere test

```csharp
[Fact]
public void MyNewFeature_Works()
{
    // Arrange
    var sut = new MyClass();
    
    // Act
    var result = sut.DoSomething();
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(expected, result);
}
```

## Coverage target

| Modulo | Target |
|--------|--------|
| AGXIOME.Shared | ≥ 95% |
| AGXIOME.Core | ≥ 90% |
| AGXIOME.LLMWIKY | ≥ 85% |
| AGXIOME.Storage | ≥ 85% |
| AGXIOME.XLink | ≥ 90% |
| AGXIOME.Security | ≥ 95% |
