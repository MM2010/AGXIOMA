---
title: Agxiome Telemetry
category: source
tags: [telemetry, opentelemetry, agxiome]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Telemetry

## Summary
The `AGXIOME.Telemetry` project provides OpenTelemetry-based observability for the AGXIOME agent framework. It instruments ASP.NET Core, HTTP, and runtime metrics, and exports telemetry via the OpenTelemetry Protocol (OTLP). This library is a dependency for agent services that require distributed tracing, metrics, and logging.

## Key Details
- Targets .NET 10 with nullable enabled and implicit usings.
- Depends on [[AGXIOME.Core]] for core abstractions.
- Packages: OpenTelemetry (1.15.3), OTLP exporter (1.15.3), Extensions.Hosting (1.15.3), ASP.NET Core instrumentation (1.15.2), HTTP instrumentation (1.15.1), Runtime instrumentation (1.15.1).
- Designed to be configured via standard OpenTelemetry builder patterns in host startup.
- Enables metrics collection for CPU, memory, and GC via runtime instrumentation.
- Supports exporting to any OTLP-compatible backend (e.g., Jaeger, Zipkin, Prometheus, Azure Monitor).

## Cross-References
- [[AGXIOME.Core]] — shared abstractions for agent definitions, execution contexts, and dependencies.
- [[sources/agxiome-agent-host-agxiome-agent-host]] — host application that configures and activates this telemetry module.
- [[concepts/opentelemetry-principles]] — overview of the observability patterns used.

## Source Files
- `AGXIOME.Telemetry/AGXIOME.Telemetry.csproj` (ingested 2026-05-14)