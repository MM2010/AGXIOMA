---
title: Appsettings
category: source
tags: [configuration, appsettings, environment, json]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Appsettings

## Summary
The `appsettings.json` file for the `AGXIOME.CLI` project defines the foundational runtime configuration for the AGXIOMA framework in development mode. It specifies the default LLM provider (OpenAI, GPT-4o-mini), database and cache connection strings (PostgreSQL and Redis), and OpenTelemetry settings for telemetry export. This file is the primary environment-dependent configuration artifact, enabling cost tracking, service naming, and endpoint routing.

## Key Details
- LLM provider is set to **OpenAI** with model **gpt-4o-mini**; API key is empty (must be supplied per environment).
- Cost tracking is **enabled** (`"EnableCostTracking": true`).
- PostgreSQL connection string targets a local instance (`Host=localhost;Database=agxiome;Username=postgres;Password=postgres`).
- Redis connection string uses `localhost:6379` with `abortConnect=false`.
- Telemetry service name is `agxiome-dev` in an `development` environment, exporting via OTLP to `http://localhost:4317`.
- File is located at `AGXIOME.CLI/appsettings.json`.

## Cross-References
- [[LLMWIKY]] — configuration of the LLM provider and model used by the agent framework.
- [[Telemetry]] — OpenTelemetry settings for observability and tracing.
- [[ConnectionStrings]] — database and cache connectivity details.

## Source Files
- `AGXIOME.CLI/appsettings.json` (ingested 2026-05-14)