---
title: Directory Build
category: source
tags: [nuget, msbuild, build-configuration, warnings]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Directory Build

## Summary
The `Directory.Build.props` file in the `src/` folder is a shared MSBuild configuration that applies to every project under that directory. It suppresses two NuGet warnings—`NU1603` (benign version resolution when NuGet falls back to an available version) and `NU1510` (noisy package deprecation messages)—to reduce build log clutter across all source projects.

## Key Details
- Applies automatically to all projects inside `src/` via MSBuild's directory-based `Directory.Build.props` convention.
- Suppressed warnings: `NU1603` (version resolution fallback) and `NU1510` (package deprecation noise).
- Both warnings are considered non-actionable for the project’s dependency resolution strategy.
- The suppression is additive: it appends to any existing `$(NoWarn)` property, so individual projects can still override or extend their own warning filters.
- Format: unifies warning management at the solution level rather than per-project.
- Typical use in AGXIOMA to keep build logs focused on actual errors or severe warnings.

## Cross-References
- [[SuppressWarnings]] — related pattern for per-project or global warning suppression.
- [[Build Properties]] — overview of other shared build configuration files and properties.

## Source Files
- `src/Directory.Build.props` (ingested 2026-05-14)