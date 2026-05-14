---
title: AgxiomeLogger
category: entity
tags: [logging, logger, .net, framework, provider]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgxiomeLogger

## Summary
AgxiomeLogger is the per-module logging instance within the AGXIOME .NET agent framework. It is created by the `AgxiomeLoggerFactory` and supports multiple log providers (Console, Seq, NLog, Graylog, Serilog) as well as per-module minimum-level overrides with wildcard support. The logger enforces level filtering before delegating to the configured provider, and gracefully handles both standard and structured (placeholder-based) log messages.

## Key Details
- Implements `IAgxiomeLogger` and is internal; instantiated only via `AgxiomeLoggerFactory`.
- Factory `Configure()` is thread-safe (uses lock) and rebuilds module override dictionary from `LoggingConfig`.
- Default factory method creates a logger with Console provider at Information level.
- `IsEnabled()` checks default minimum level, then iterates over module overrides — supports exact match, `*` wildcard, and prefix wildcard (e.g., `AGXIOME.Core*`).
- Convenience methods: `Debug`, `Information`, `Warning`, `Error`, `Exception`.
- `Exception()` method appends exception type, message, and stack trace to the log message.
- `Log()` method attempts `string.Format`; on `FormatException` falls back to raw message (supports structured logging placeholders like `{Name}`).
- Provider routing via `CreateProvider()`: `"seq"` → `SeqLogProvider`, `"nlog"` → `NLogProvider`, `"graylog"` → `GraylogProvider`, `"serilog"` → `SerilogProvider`, default → `ConsoleLogProvider`.
- Console provider uses color-coded output and is thread-safe with a static lock.
- Seq provider sends structured JSON over HTTP; Graylog sends GELF 1.1 JSON; NLog and Serilog providers are stubs that currently write to `Trace`.
- All providers are fire-and-forget (never throw exceptions).

## Cross-References
- [[LoggingConfig]] — configuration object used to set default level, provider, module overrides, and endpoint/api key.
- [[IAgxiomeLogger]] — public interface implemented by AgxiomeLogger.
- [[AgxiomeLoggerFactory]] — factory that creates and configures AgxiomeLogger instances.

## Source Files
- `AGXIOME.Core/Logging/AgxiomeLogger.cs` (ingested 2026-05-14)