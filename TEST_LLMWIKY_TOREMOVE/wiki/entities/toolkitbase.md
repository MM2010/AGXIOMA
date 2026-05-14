---
title: ToolkitBase
category: entity
tags: [toolkit, infrastructure, IToolkit, ToolkitAttribute, async, registry]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ToolkitBase

## Summary
ToolkitBase is the abstract base class from which all 161 AGXIOME toolkits derive. It implements the `IToolkit` interface and enforces an async-first execution model, returning a `ToolkitResult` with typed output. Each toolkit is decorated with a `[Toolkit]` attribute for auto-discovery via reflection, and configuration is injected through `ToolkitConfig` or environment variables. The companion `ToolkitRegistry` provides registration and discovery of all toolkits.

## Key Details
- **Enums**: `ToolkitCategory` (12 categories from Communication to Enterprise), `ToolkitResultStatus` (Ok, Error, Skipped, RateLimited, Unauthorized).
- **`[Toolkit]` attribute**: Marks a class as a toolkit with Id, DisplayName, and Category; used for reflection-based auto-discovery.
- **`ToolkitResult` value object**: Sealed record with Status, Data, Error, Duration, and optional Metadata; provides static factory methods `Success`, `Fail`, `NotConfigured`.
- **`IToolkit` interface**: Defines `Id`, `Name`, `Category`, `IsConfigured`, and `ExecuteAsync` method.
- **`ToolkitBase`**:
  - Abstract, requires subclasses to implement `Id`, `Name`, `Category`, and `ExecuteCoreAsync`.
  - `ExecuteAsync` checks configuration, measures duration, wraps errors, and returns `ToolkitResult`.
  - Provides helper methods `GetParam<T>` and `GetSetting` for subclasses.
- **`ToolkitRegistry`**: Singleton-like dictionary; methods `Register`, `Find`, `All`, `ByCategory`, and static `DiscoverTypes` to scan an assembly for non-abstract types with `[Toolkit]`.
- All toolkits are async-first; cancellation via `CancellationToken` is supported.

## Cross-References
- [[entities/IToolkit]] — Contract that all toolkits implement
- [[entities/ToolkitResult]] — Return type for every toolkit execution
- [[entities/ToolkitAttribute]] — Metadata attribute used for auto-discovery

## Source Files
- `AGXIOME.Toolkits/ToolkitBase.cs` (ingested 2026-05-14)