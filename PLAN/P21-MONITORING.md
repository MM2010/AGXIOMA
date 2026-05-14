# P21 — Monitoring

## File: `Telemetry/TelemetrySetup.cs`

Namespace: `AGXIOME.Telemetry`

## Setup OpenTelemetry

```csharp
public static class AgxiomeTelemetry
{
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
}

public record TelemetryConfig
{
    public Uri? OtlpEndpoint { get; init; }
    public string ServiceName { get; init; } = "agxiome";
    public string Environment { get; init; } = "development";
    public double SamplingRate { get; init; } = 0.1;
}
```

## Metriche chiave

```csharp
public static class AgentMetrics
{
    public static readonly Meter Meter = new("AGXIOME.Core", "1.0.0");

    // Counter
    public static Counter<int> AgentRuns = Meter.CreateCounter<int>("agxiome.agent.runs");
    public static Counter<int> ToolCalls = Meter.CreateCounter<int>("agxiome.agent.tool_calls");
    public static Counter<int> GuardrailHits = Meter.CreateCounter<int>("agxiome.guardrail.hits");
    public static Counter<int> HITLPauses = Meter.CreateCounter<int>("agxiome.hitl.pauses");

    // Histogram
    public static Histogram<double> AgentRunDuration = Meter.CreateHistogram<double>("agxiome.agent.run.duration", "ms");
    public static Histogram<double> LLMLatency = Meter.CreateHistogram<double>("agxiome.llm.latency", "ms");
    public static Histogram<double> RAGSearchLatency = Meter.CreateHistogram<double>("agxiome.rag.search.latency", "ms");
    public static Histogram<double> StorageWriteLatency = Meter.CreateHistogram<double>("agxiome.storage.write.latency", "ms");
    public static Histogram<double> XLinkLatency = Meter.CreateHistogram<double>("agxiome.xlink.latency", "ms");

    // UpDownCounter
    public static UpDownCounter<int> ActiveAgents = Meter.CreateUpDownCounter<int>("agxiome.agent.active");
}
```

## Health checks

```csharp
// GET /health      → liveness
// GET /health/ready → readiness (DB, Redis, LLM)
// GET /health/startup → startup

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)
    .AddRedis(redisConnection)
    .AddUrlGroup(new Uri(config.LLM.HealthEndpoint), "llm");
```

## Run/Session metrics

```csharp
public record RunMetrics(
    int PromptTokens, int CompletionTokens, int TotalTokens,
    TimeSpan Duration, int ToolCallCount,
    Dictionary<string, ModelMetrics> PerModel
);

public record ModelMetrics(string Provider, string Model,
    int PromptTokens, int CompletionTokens, int TotalTokens,
    TimeSpan InferenceTime, decimal EstimatedCost);

public record SessionMetrics(
    int TotalRuns, int TotalToolCalls,
    int TotalPromptTokens, int TotalCompletionTokens,
    decimal TotalEstimatedCost, TimeSpan TotalDuration
);
```

## Test

| Test | Descrizione |
|------|------------|
| Meter incrementa | Counter aumenta |
| Histogram registra | Valore nello storico |
| Health check OK | Endpoint risponde 200 |
| RunMetrics calcolati | Tokens e cost corretti |
