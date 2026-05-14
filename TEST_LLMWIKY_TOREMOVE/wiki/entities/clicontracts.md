---
title: Clicontracts
category: entity
tags: [contract, CLI, testability, async]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Clicontracts

## Summary
The Clicontracts establish the core interfaces and types for the AGXIOME CLI command layer. By defining injectable `IAgxiomeApiClient` and `ICliOutput` abstractions, along with a non-blocking `ICliCommand` contract, these contracts enable fully unit-testable command implementations without coupling to console I/O or blocking network calls.

## Key Details
- `CliResult` sealed record with static factory methods `Ok` and `Fail`; includes `Success`, `Output`, `Error`, and `ExitCode`.
- `ICliCommand` defines `Name`, `Description`, and `ValueTask<CliResult> ExecuteAsync(string[], CancellationToken)` — all command logic is async-only.
- `IAgxiomeApiClient` provides `GetAsync`, `PostAsync`, `DeleteAsync` (all `ValueTask<string>`), plus `BaseUrl` and `IsReachable`.
- `ICliOutput` abstracts console output with `Write`, `WriteLine`, and `WriteError` methods.
- Two `ICliOutput` implementations: `ConsoleCliOutput` (real console) and `StringCliOutput` (captures output into `StringBuilder` for test assertions).
- Designed for dependency injection; commands receive API client and output via constructor.
- No blocking I/O in the command layer — `ExecuteAsync` returns `ValueTask` for efficient async execution.

## Cross-References
- [[Commands]] — concrete CLI commands implementing `ICliCommand`

## Source Files
- `AGXIOME.CLI/Commands/CliContracts.cs` (ingested 2026-05-14)