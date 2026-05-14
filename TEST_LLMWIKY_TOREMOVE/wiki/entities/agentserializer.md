---
title: AgentSerializer
category: entity
tags: [serialization, agent configuration, snapshot, JSON, utility]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AgentSerializer

## Summary

The `AgentSerializer` class provides a stateless utility for serializing and deserializing portable snapshots of an agent's configuration and runtime-observable state (`AgentSnapshot`). It supports round‑tripping to and from string‑keyed dictionaries (for transport or inspection) and direct file I/O to JSON. This enables save, load, and clone workflows without depending on any specific agent implementation class. The companion `AgentSnapshot` record intentionally omits in‑flight run state, which is handled separately by [[BackgroundRunWorker]].

## Key Details

- `AgentSnapshot` is a `record` that stores an agent’s `Id`, `Name`, `Description`, `Config` (of type `AgentConfig`), an optional `ExtraProperties` dictionary, a `SnapshotAt` timestamp, and a `SchemaVersion`.
- `AgentSerializer` is a `sealed class` designed as a stateless singleton; all methods use a shared `JsonSerializerOptions` instance with indented output and string‑enum conversion.
- `ToDictionary()` flattens a snapshot into a `Dictionary<string, object?>`; the `Config` and `ExtraProperties` are serialized as `JsonDocument` sub‑objects.
- `FromDictionary()` reconstructs a snapshot from a dictionary previously produced by `ToDictionary()`, throwing `InvalidOperationException` on missing required fields.
- `SaveAsync()` serializes a snapshot to a JSON file at a given path.
- `LoadAsync()` reads a JSON file and deserializes it into an `AgentSnapshot`, throwing `FileNotFoundException` if the file is missing.
- The `Required<T>` helper method extracts a required field from the dictionary with type conversion.
- Validation: `ArgumentNullException` for null snapshot or dictionary; `ArgumentException` for empty file paths.

## Cross-References

- [[AgentConfig]] — the configuration object embedded in every snapshot
- [[BackgroundRunWorker]] — manages in‑flight run state, explicitly excluded from snapshots
- [[AGXIOME.Core.Agent]] — namespace containing the serializer and related agent types

## Source Files

- `AGXIOME.Core/Agent/AgentSerializer.cs` (ingested 2026-05-14)