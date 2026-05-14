---
title: AgxiomeTelemetrySetup
category: entity
tags: [telemetry, open-telemetry, setup, configuration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeTelemetrySetup

## Summary
The `AgxiomeTelemetrySetup` static class provides a single extension method `AddAgxiomeTelemetry` that bootstraps OpenTelemetry tracing and metrics for AGXIOMA agents. It configures core instrumentation (ASP.NET Core, HTTP client), adds source names for the platform's internal libraries, and exports telemetry via OTLP with HTTP protocol. The accompanying `TelemetryConfig` record centralizes endpoint, service name, environment, and sampling rate settings.

## Key Details
- Extension method on `IServiceCollection` for DI integration.
- Tracing sources: `"AGXIOME.Core"`, `"AGXIOME.LLMWIKY"`, `"AGXIOME.XLink"`.
- Instrumentation includes `AddAspNetCoreInstrumentation()` and `AddHttpClientInstrumentation()`.
- Exporter: OTLP using `HttpProtobuf` protocol, endpoint defaults to `http://localhost:4317`.
- Metrics meter: `"AGXIOME.Core"`, plus ASP.NET Core and runtime instrumentation.
- `TelemetryConfig` record with `OtlpEndpoint`, `ServiceName` (default `"agxiome"`), `Environment` (default `"development"`), `SamplingRate` (default `0.1`).
- Designed for plug-and-play telemetry in any AGXIOMA host application.

## Cross-References
- [[TelemetryConfig]] — the configuration record used by this setup.
- [[AgxiomeAgent]] — the core agent that produces telemetry from the listed sources.

## Source Files
- `AGXIOME.Telemetry/AgxiomeTelemetrySetup.cs` (ingested 2026-05-14)