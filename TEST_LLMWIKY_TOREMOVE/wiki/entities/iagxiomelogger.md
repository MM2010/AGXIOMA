---
title: IAgxiomeLogger
category: entity
tags: [logging, interface, factory, shared]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IAgxiomeLogger

## Summary
The `IAgxiomeLogger` interface serves as the unified logging contract for the entire AGXIOME framework. All modules are expected to log through this interface, ensuring consistent log levels, formatting, and configuration. A companion factory interface, `IAgxiomeLoggerFactory`, provides per‑module logger creation and runtime configuration management.

## Key Details
- `IAgxiomeLogger` exposes a `ModuleName` property to identify the source of log entries.
- `IsEnabled(LogLevel level)` allows callers to avoid expensive log message construction when a level is disabled.
- Logging methods: `Debug`, `Information`, `Warning`, `Error`, and `Exception` (the last includes an `Exception` parameter). All accept a format string and optional arguments.
- `IAgxiomeLoggerFactory` abstracts creation of loggers per module via `CreateLogger(string moduleName)`.
- `Configure(LoggingConfig config)` enables runtime reconfiguration of all loggers created by the factory.
- Both interfaces reside in the `AGXIOME.Shared.Logging` namespace, making them a shared foundational layer.

## Cross-References
- [[LoggingConfig]] — the configuration object consumed by `IAgxiomeLoggerFactory.Configure`

## Source Files
- `AGXIOME.Shared/Logging/IAgxiomeLogger.cs` (ingested 2026-05-14)