---
title: MariaDbStore
category: entity
tags: [store, mariadb, mysql, dapper, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MariaDbStore

## Summary
The `MariaDbStore` is a concrete implementation of the [[ICommandStore]] interface that provides CRUD operations against a MariaDB (or MySQL) database. It uses the Dapper micro-ORM and the `MySqlConnector` ADO.NET client for wire‑compatible access with MariaDB 11+. SQL statements are automatically generated from entity class properties, with convention‑based table naming (entity name lowercased with an appended "s"). Each operation includes a 30‑second command timeout.

## Key Details
- Implements `ICommandStore` for generic persistence of any `class` entity.
- Wire protocol compatible with MariaDB 11+ and MySQL 8.x via `MySqlConnector`.
- Connection string format: `"Server=localhost;Database=agxiome;User=root;Password=..."`
- Auto‑generates SQL using reflection on public instance properties.
- Table naming convention: `typeof(T).Name.ToLower() + "s"` (e.g., `Order` → `orders`).
- Primary key column is assumed to be named `id` (case‑sensitive in SQL generation for `Update` and `Delete`).
- `SaveAsync` – generates `INSERT INTO` with all properties.
- `UpdateAsync` – generates `UPDATE` setting all properties except `Id`, filtered by `WHERE id = @Id`.
- `DeleteAsync` – executes `DELETE FROM` by `@Id` parameter.
- All async operations accept an optional `CancellationToken` and use `commandTimeout: 30`.
- No batching, transactions, or custom table/column mappings included (pure convention‑based).

## Cross-References
- [[ICommandStore]] — contract that this store fulfills
- [[PostgresStore]] — alternative PostgreSQL store implementing the same interface

## Source Files
- `AGXIOME.Storage/Stores/MariaDbStore.cs` (ingested 2026-05-14)