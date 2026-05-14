---
title: Orchjson
category: entity
tags: [serialization, json, infrastructure]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Orchjson

## Summary

`OrchJson` is an internal static class that wraps `System.Text.Json` serialization logic, providing a single entry point for JSON operations across the orchestration infrastructure. By keeping the JSON serialiser confined to this type, any future changes to the serialisation library or configuration can be applied in one place without affecting consumer code.

## Key Details

- Declared as `internal static` – not accessible outside the AGXIOME.Orchestration assembly.
- Uses `JsonSerializerOptions` configured with:
  - `PropertyNamingPolicy = CamelCase`
  - `DefaultIgnoreCondition = WhenWritingNull`
  - `WriteIndented = false`
- Exposes two generic methods:
  - `Serialize<T>(T value)` → returns a JSON `string`.
  - `Deserialize<T>(string json)` → returns `T?`.
- Both methods rely on the static `Options` instance, ensuring consistent behaviour.
- Allows the orchestration layer to be decoupled from the choice of JSON library (e.g., swapping to Newtonsoft.Json would only require changes in this class).

## Cross-References

- [[AgentToolExecutor]] – one potential consumer of serialized command payloads.
- [[Serialization]] – broader concept of JSON handling in the framework.

## Source Files

- `AGXIOME.Orchestration/Infrastructure/OrchJson.cs` (ingested 2026-05-14)