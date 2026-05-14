# 10 — Monitoring & Observability

## Scopo

OpenTelemetry-first. Metrics, traces, logs strutturati. Alerting proattivo.

## Architettura

```
Application (AGXIOME)
    │
    ├── OpenTelemetry SDK
    │     ├── Traces    ──► OTLP Exporter ──► Seq / Grafana Tempo / Jaeger
    │     ├── Metrics   ──► OTLP Exporter ──► Prometheus / Grafana
    │     └── Logs      ──► Structured    ──► Seq / ELK / Loki
    │
    └── Health Checks
          └── /health ──► Liveness + Readiness
```

## Strumenti

```csharp
// ── OpenTelemetry setup ──
public static IServiceCollection AddAgxiomeTelemetry(
    this IServiceCollection services, TelemetryConfig config)
{
    services.AddOpenTelemetry()
        .WithTracing(t => t
            .AddSource("AGXIOME.*")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRedisInstrumentation()
            .AddNpgsql()
            .AddOtlpExporter(o => o.Endpoint = config.OtlpEndpoint))
        .WithMetrics(m => m
            .AddMeter("AGXIOME.*")
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter());

    return services;
}

public class TelemetryConfig
{
    public Uri? OtlpEndpoint { get; set; }
    public string ServiceName { get; set; } = "agxiome";
    public string Environment { get; set; } = "development";
    public bool EnableDetailedTracing { get; set; } = false;
    public double SamplingRate { get; set; } = 1.0; // 1.0 = 100%
}
```

## Metriche chiave

| Nome | Tipo | Descrizione |
|------|------|-------------|
| `agxiome.agent.runs.total` | Counter | Totale esecuzioni agente |
| `agxiome.agent.runs.active` | UpDownCounter | Agenti correntemente in esecuzione |
| `agxiome.agent.run.duration` | Histogram | Durata esecuzione (ms) |
| `agxiome.agent.tool.calls` | Counter | Chiamate tool per tipo |
| `agxiome.agent.stream.chunks` | Counter | Chunk inviati in streaming |
| `agxiome.rag.search.latency` | Histogram | Latenza ricerca semantica |
| `agxiome.rag.chunks.retrieved` | Histogram | Chunk per query RAG |
| `agxiome.workflow.runs` | Counter | Esecuzioni workflow |
| `agxiome.workflow.duration` | Histogram | Durata workflow (ms) |
| `agxiome.xlink.messages` | Counter | Messaggi X-Link inviati/ricevuti |
| `agxiome.xlink.bytes` | Counter | Byte trasferiti su X-Link |
| `agxiome.xlink.compression.ratio` | Gauge | Rapporto compressione |
| `agxiome.security.auth.failures` | Counter | Fallimenti autenticazione |
| `agxiome.security.rate.limit.hits` | Counter | Rate-limit attivati |
| `agxiome.db.query.latency` | Histogram | Latenza query DB |
| `agxiome.db.connections.active` | UpDownCounter | Connessioni DB attive |

### Run & Session Metrics

```csharp
public class RunMetrics
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public TimeSpan Duration { get; set; }
    public int ToolCallCount { get; set; }
    public Dictionary<string, ModelMetrics> PerModelMetrics { get; set; } = new();
}

public class ModelMetrics
{
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public TimeSpan InferenceTime { get; set; }
    public decimal EstimatedCost { get; set; }

    public decimal CalculateCost(ProviderCostConfig costConfig)
    {
        var inputCost = PromptTokens / 1_000_000m * (decimal)costConfig.CostPerMillionInputTokens;
        var outputCost = CompletionTokens / 1_000_000m * (decimal)costConfig.CostPerMillionOutputTokens;
        return inputCost + outputCost;
    }
}

public class SessionMetrics
{
    public int TotalRuns { get; set; }
    public int TotalToolCalls { get; set; }
    public int TotalPromptTokens { get; set; }
    public int TotalCompletionTokens { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, ModelMetrics> AggregatedModelMetrics { get; set; } = new();
}

// Metriche aggiuntive:
public static class AdditionalMetrics
{
    // Guardrail hits rate
    // "agxiome.guardrail.hits" Counter{guardrail="pii",action="block"}
    
    // HITL pauses
    // "agxiome.hitl.pauses" Counter{type="confirmation"}
    
    // HITL resolution time
    // "agxiome.hitl.resolution_time" Histogram

    // Learning operations
    // "agxiome.learning.memories_created" Counter
    // "agxiome.learning.memories_retrieved" Counter

    // Schedule events
    // "agxiome.schedule.runs" Counter{schedule_id="..."}
    // "agxiome.schedule.duration" Histogram
}
```

## Tracing (spans)

| Span name | Source | Attributi |
|-----------|--------|-----------|
| `agent.run` | AGXIOME.Core | agent.id, agent.type, status |
| `agent.tool.call` | AGXIOME.Core | tool.name, tool.duration |
| `agent.llm.infer` | AGXIOME.LLMWIKY | provider, model, tokens |
| `agent.llm.embed` | AGXIOME.LLMWIKY | provider, dimensions |
| `rag.search` | AGXIOME.Knowledge | top_k, min_score, results |
| `rag.ingest` | AGXIOME.Knowledge | doc_type, chunk_count |
| `storage.read` | AGXIOME.Storage | store_type, entity |
| `storage.write` | AGXIOME.Storage | store_type, entity |
| `xlink.send` | AGXIOME.XLink | target, size, compressed |
| `xlink.receive` | AGXIOME.XLink | source, checksum_valid |

## Health checks

```csharp
// Endpoints
GET /health          → Liveness (semplice)
GET /health/ready    → Readiness (DB, Redis, LLM provider)
GET /health/startup  → Startup (pronto a ricevere traffico)

// Output
{
  "status": "Healthy",
  "checks": [
    { "name": "postgresql", "status": "Healthy", "latency": "3ms" },
    { "name": "redis",      "status": "Healthy", "latency": "1ms" },
    { "name": "llmwikiy",   "status": "Degraded", "latency": "1200ms" }
  ],
  "version": "1.0.0",
  "timestamp": "2026-05-11T12:00:00Z"
}
```

## Alerting rules

| Rule | Condition | Severity | Action |
|------|-----------|----------|--------|
| Agent error rate > 5% | `rate(agxiome.agent.runs.total{status="error"}[5m]) / rate(agxiome.agent.runs.total[5m]) > 0.05` | Critical | PagerDuty + Slack |
| LLM latency > 10s p95 | `histogram_quantile(0.95, rate(agxiome.agent.run.duration_bucket[5m])) > 10000` | Warning | Slack |
| RAG latency > 2s p95 | `histogram_quantile(0.95, rate(agxiome.rag.search.latency_bucket[5m])) > 2000` | Warning | Slack |
| Auth failures spike | `rate(agxiome.security.auth.failures[5m]) > 10` | Critical | PagerDuty + Security |
| Rate-limit hits spike | `rate(agxiome.security.rate.limit.hits[5m]) > 100` | Warning | Slack |
| DB connection pool > 80% | `agxiome.db.connections.active / agxiome.db.connections.max > 0.8` | Warning | Slack |
| Check checksum failures | `rate(agxiome.xlink.checksum.failures[5m]) > 0` | Critical | PagerDuty + Alert |

## Logging policy

```csharp
// Pseudonymization: log mai include PII
// Campo identificativo pseudonimizzato via SHA256(salt + id)
public static class LogPolicy
{
    public static string Pseudonymize(string id) =>
        Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"agxiome-salt-2026:{id}")))
        .ToLowerInvariant()[..12];

    public static string RedactSensitive(string input)
    {
        // Redact: api_key, secret, password, token, credit_card
        return Regex.Replace(input,
            @"(api[_-]?key|secret|password|token)['""]?\s*[:=]\s*['""]?[^'""\s]+",
            "$1: ***REDACTED***",
            RegexOptions.IgnoreCase);
    }
}
```

## NFR

| Metrica | Target |
|---------|--------|
| Metric ingestion latency | < 1s |
| Trace sampling (default) | 10% |
| Log retention (debug) | 7 days |
| Log retention (error) | 90 days |
| Metric retention | 30 days (1s resolution) |
| Dashboard refresh | 15s |
| Alert delivery | < 1min |
