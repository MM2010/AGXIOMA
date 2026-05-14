---
title: MemoryCommand
category: entity
tags: [cli, memory, command]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MemoryCommand

## Summary
The `MemoryCommand` class implements the `ICliCommand` interface to provide the `agxiome memory` CLI command. It manages a markdown‑based memory store with three subcommands: `init` (create a fresh store directory), `add` (ingest a markdown file as an agent interaction), and `search` (grep‑based lookup). It serves as the primary entry point for users to interact with the framework’s persistent memory layer from the command line.

## Key Details
- **Subcommands**: `init`, `add`, `search` — dispatched via `ExecuteAsync` using a `switch` expression.
- **Init**: Creates a `memory/` (default) directory with a `state/` subdirectory. Returns the absolute path.
- **Add**: Reads a given markdown file, wraps its content in an `AgentInteraction`, and saves it via `MarkdownMemoryStore.SaveAsMarkdownAsync`.
- **Search**: Runs a grep search (`MarkdownMemoryStore.SearchByGrepAsync`) over `.md` files in the store directory. Supports an optional `--dir` argument to override the base path.
- **Error Handling**: Returns `CliResult.Fail` with a usage message when arguments are missing or invalid.
- **Dependencies**: Uses `MarkdownMemoryConfig`, `MarkdownMemoryStore`, and `AgentInteraction` from the core libraries.
- **Async Pattern**: All subcommand implementations are asynchronous (`ValueTask<CliResult>`), with `init` being synchronous under `ValueTask.FromResult`.

## Cross-References
- [[MarkdownMemoryStore]] — core class that executes the actual file I/O and grep search.
- [[MarkdownMemoryConfig]] — configuration object used to set the store’s base path.
- [[AgentInteraction]] — model object wrapping the content and metadata of a memory entry.
- [[ICliCommand]] — interface that `MemoryCommand` implements, defining the CLI contract.
- [[CliResult]] — return type for all command results.

## Source Files
- `AGXIOME.CLI/Commands/MemoryCommand.cs` (ingested 2026-05-14)