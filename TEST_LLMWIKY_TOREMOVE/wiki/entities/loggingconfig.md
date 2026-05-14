---
title: LoggingConfig
category: entity
tags: [logging, configuration, LogLevel, DebugContextConfig]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# LoggingConfig

## Summary
The `LoggingConfig` record defines the global logging configuration for the AGXIOMA framework, including a default minimum severity level, per‑module log level overrides, and provider settings (Console, Seq, NLog, Graylog, Serilog). It works with the `LogLevel` enum and `DebugContextConfig` record to enable granular, runtime‑adjustable logging.

## Key Details
- `LogLevel` enum: `Debug=0`, `Information=1`, `Warning=2`, `Error=3`, `Exception=4`.
- `DebugContextConfig` record specifies per‑module log level and whether caller info is enabled. Default module `"*"` applies to all modules.
- `LoggingConfig.DefaultMinimumLevel` is `Information` by default.
- `LoggingConfig.ModuleOverrides` is a list of `DebugContextConfig` entries to override logging for specific modules.
- `LoggingConfig.Provider` defaults to `"Console"`; other supported providers: `Seq`, `NLog`, `Graylog`, `Serilog`.
- `Endpoint` and `ApiKey` are optional and used by providers like Seq and Graylog.

## Cross-References
- [[Logging]] — framework‑wide logging architecture
- [[Configuration]] — general configuration pattern in AGXIOMA

## Source Files
- `AGXIOME.Shared/Logging/LoggingConfig.cs` (ingested 2026-05-14)