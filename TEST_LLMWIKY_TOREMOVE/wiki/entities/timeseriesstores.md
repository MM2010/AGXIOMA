---
title: TimeseriesStores
category: entity
tags: [time-series, influxdb, questdb, command-store]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TimeseriesStores

## Summary
The `TimeseriesStores` source file contains two implementations of the [[ICommandStore]] interface for time-series databases: an **InfluxDB** store and a **QuestDB** store. Both providers treat writes as append-only (no in-place updates) and use custom reflection-based conventions to map entity properties to tags/fields (InfluxDB) or ILP line protocol fields (QuestDB). They are designed for high-throughput telemetry or event-sourced scenarios where storage is optimized for time-stamped data.

## Key Details
- Two concrete store classes: `InfluxDbCommandStore` and `QuestDbCommandStore`, both under the `AGXIOME.Storage.Stores` namespace.
- `InfluxDbCommandStore` uses the official `InfluxDB.Client` NuGet package (v2/Cloud OSS). It maps entity properties using a convention: `Id` and string properties ending with `"Tag"` become tags; all other properties become fields. Timestamps are added at write time (`DateTime.UtcNow`) with millisecond precision.
- `QuestDbCommandStore` uses the QuestDB HTTP `/write` endpoint with InfluxDB Line Protocol (ILP) for high-throughput inserts. Delete operations are emulated via SQL `DROP` on a hypertable (QuestDB limitation) and updates are inserts of new records.
- Both stores require a dedicated options record (`InfluxDbStoreOptions` / `QuestDbStoreOptions`) for connection settings.
- `UpdateAsync` is implemented as an insert of a new point (same timestamp overwrites in InfluxDB; append in QuestDB).
- `DeleteAsync` uses time-range predicates in InfluxDB; QuestDB uses a raw SQL `DROP` statement (non‑standard, partition‑level).
- Source file: `AGXIOME.Storage/Stores/TimeSeriesStores.cs`

## Cross-References
- [[ICommandStore]] — the interface both stores implement
- [[StorageProviderType]] — enum identifying the provider (InfluxDB / QuestDB)
- [[SqlStores]] — relational (SQL) command stores for comparison

## Source Files
- `AGXIOME.Storage/Stores/TimeSeriesStores.cs` (ingested 2026-05-14)