---
title: RunCommand
category: entity
tags: [cli, command, execution, agent, team, workflow]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# RunCommand

## Summary
The `RunCommand` class implements the `agxiome run` CLI command (MU-01). It serves as the primary entry point for executing an agent, team, or workflow at runtime. The command parses `--id`, `--type`, `--input`, and `--stream` flags, maps the type to the appropriate API endpoint, sends the request via `IAgxiomeApiClient`, and outputs the result. It performs basic validation (required flags, server reachability) and supports streaming output when the `--stream` flag is present.

## Key Details
- **Name**: `"run"`, **Description**: `"Execute an agent, team, or workflow."`
- **Signature**: `ExecuteAsync(string[] args, CancellationToken ct = default)` returns `ValueTask<CliResult>`
- **Required flags**: `--id` (entity identifier), `--type` (one of `agent`, `team`, `workflow`)
- **Optional flags**: `--input` (string payload, defaults to empty), `--stream` (flag, enables streaming output)
- **Endpoint mapping**: `agent` → `api/runs`, `team` → `api/teams/{id}/coordinate`, `workflow` → `api/workflows/{id}/execute`
- **Validation**: Returns `CliResult.Fail` with message if `--id` or `--type` missing, if type is unknown, or if `client.IsReachable` is `false`
- **Error handling**: Catches exceptions during API call and returns `CliResult.Fail` with exception message
- **Dependencies**: `IAgxiomeApiClient` (for HTTP POST), `ICliOutput` (for writing result to console)
- **Integration**: Used by the CLI dispatcher to route `run` commands; part of the `AGXIOME.CLI.Commands` namespace

## Cross-References
- [[ICliCommand]] — interface that `RunCommand` implements
- [[IAgxiomeApiClient]] — client used to send execution requests
- [[ICliOutput]] — output abstraction for writing results
- [[CliResult]] — return type representing success/failure

## Source Files
- `AGXIOME.CLI/Commands/RunCommand.cs` (ingested 2026-05-14)