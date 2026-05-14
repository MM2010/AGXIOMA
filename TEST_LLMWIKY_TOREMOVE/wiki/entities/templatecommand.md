---
title: TemplateCommand
category: entity
tags: [cli, command, template, project-gen]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TemplateCommand

## Summary
The `TemplateCommand` is a CLI command (`agxiome new`) that scaffolds a new AGXIOME agent project from a built-in template. It copies a predefined directory structure (the `agxioma-agent-template`) into a user-specified output location, providing the foundation for a new agent solution. This command simplifies project initialization for developers using the AGXIOME framework.

## Key Details
- **Command Name**: `"new"` (accessible via `agxiome new [name] [--output path]`)
- **Description**: "Create a new AGXIOME agent project from template"
- **Implements**: `ICliCommand` interface, returning `CliResult`
- **Arguments**:
  - First positional argument: project name (defaults to `"MyAgent"`)
  - `--output` flag: specifies target directory (defaults to current `.` directory)
- **Template Resolution**:
  - Initially looks in the app’s base directory (relative path `../../../../apps/agxioma-agent-template`)
  - Falls back to the current working directory (`./apps/agxioma-agent-template`)
  - Returns a failure result if the template is not found, with instructions to install via `dotnet new install`
- **Project Generation**: Uses `CopyDirectory` to recursively copy all files and subdirectories from the template into `{output}/{name}`
- **Output Message**: Provides the project path and subsequent steps (`cd`, `dotnet restore`, `dotnet run`)
- **Recursive Copy**: `CopyDirectory` creates the destination folder, copies all files (with overwrite), and recursively processes subdirectories

## Cross-References
- [[ICliCommand]] — interface contract for all CLI commands
- [[CliResult]] — return type used to communicate success or failure
- [[AGXIOME CLI]] — parent command group that hosts this command

## Source Files
- `AGXIOME.CLI/Commands/TemplateCommand.cs` (ingested 2026-05-14)