# P14 — Testing & SLO

> **Priority**: 🟡 P1 | **Dipende da**: Tutti | **Usato da**: CI/CD
> **Target**: 500+ test, benchmark SLO, quality gates

---

## Task

| # | Task | Descrizione | Effort |
|---|------|-------------|--------|
| TS-01 | **Model Provider Tests** | 250 test per 50 provider | 3gg |
| TS-02 | **Toolkit Tests** | 800 test per 161 toolkit | 5gg |
| TS-03 | **DB Provider Tests** | 175 test per 25 DB | 3gg |
| TS-04 | **Vector DB Tests** | 210 test per 30 vector DB | 3gg |
| TS-05 | **Reader Tests** | 224 test per 28 reader | 3gg |
| TS-06 | **Embedder Tests** | 175 test per 25 embedder | 2gg |
| TS-07 | **Chunker/Reranker Tests** | 100 test per 12+8 | 1gg |
| TS-08 | **Runtime Integration Tests** | 200 test API + SignalR | 3gg |
| TS-09 | **Benchmark Suite** | Benchmark p50/p95/p99 per hot path | 2gg |
| TS-10 | **SLO Validation** | Test automatici per error budget | 1gg |
| TS-11 | **Property-Based Tests** | FsCheck per input random | 2gg |
| TS-12 | **Security Tests** | Penetration test base | 2gg |

---

## Target Numerici

| Metrica | Target | Metodo |
|---------|--------|--------|
| **Total Unit Tests** | 500+ | xUnit |
| **Integration Tests** | 200+ | Testcontainers + WebApplicationFactory |
| **Benchmark Tests** | 30+ | BenchmarkDotNet |
| **Property-Based Tests** | 50+ | FsCheck |
| **Code Coverage** | ≥80% | Coverlet |
| **p50 Agent Run** | ≤500ms | Benchmark |
| **p95 Agent Run** | ≤2000ms | Benchmark |
| **p99 Agent Run** | ≤5000ms | Benchmark |
| **Token Selection** | ≤50ms | Benchmark |
| **Embedding (512 tokens)** | ≤100ms | Benchmark |

---

## Struttura Test

```
AGXIOME.Tests/
├── Unit/
│   ├── ModelProviders/
│   ├── Toolkits/
│   ├── DBProviders/
│   ├── VectorDB/
│   ├── Readers/
│   ├── Embedders/
│   └── Core/
├── Integration/
│   ├── Runtime/
│   ├── Storage/
│   └── Realtime/
├── Benchmark/
│   ├── AgentRunBenchmark.cs
│   ├── EmbeddingBenchmark.cs
│   └── TokenCountBenchmark.cs
├── Property/
│   ├── AgentProperties.cs
│   ├── WorkflowProperties.cs
│   └── CryptoProperties.cs
└── Security/
    ├── AuthPenetration.cs
    └── XssInjection.cs
```

**Totale test target: ~2300+ | File: ~100+**
