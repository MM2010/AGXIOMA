---
title: Other CLI Commands
category: entity
tags: [cli, commands, list, session, admin, config]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Other CLI Commands

## Summary
The four commands in this group extend the `agxiome` CLI with operations for listing runtime entities (`list`), managing agent sessions (`session`), performing administrative tasks (`admin`), and reading or modifying runtime configuration (`config`). They all communicate with the runtime server via the `IAgxiomeApiClient` and format output through `ICliOutput`.

## Key Details
- **`agxiome list`** — Lists agents, teams, workflows, sessions, runs, or tools from the runtime. Defaults to agents if no type is given. Supports `--type` flag.
- **`agxiome session`** — Create, delete, inspect, or list agent sessions. Uses `--action` and related flags (`--agent`, `--id`). Default action is `list`.
- **`agxiome admin`** — Query runtime health, metrics, logs, errors, or configuration. Action is specified via `--action`; defaults to `health`.
- **`agxiome config`** — Get or set runtime configuration key-value pairs. Use `--action get [--key KEY]` or `--action set --key KEY --value VALUE`.
- All commands return `CliResult` and catch exceptions to provide consistent error messages.
- Each command verifies server reachability (`client.IsReachable`) before making API calls.
- API endpoints are constructed from a fixed path map or string interpolation.

## Cross-References
- [[AGXIOME CLI Overview]] — context for all CLI commands
- [[ApiClient]] — the client used by these commands to reach the runtime server
- [[Command Base]] — shared interface and result types

## Source Files
- `AGXIOME.CLI/Commands/OtherCommands.cs` (ingested 2026-05-14)