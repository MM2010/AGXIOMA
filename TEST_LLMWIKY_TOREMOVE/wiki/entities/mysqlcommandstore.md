---
title: MySqlCommandStore
category: entity
tags: [MySQL, MariaDB, CommandStore, Storage, Dapper]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# MySqlCommandStore

## Summary
`MySqlCommandStore` is a sealed implementation of `ICommandStore` that provides MySQL/MariaDB persistence for command data. It uses Dapper and `MySqlConnector` to execute dynamically generated SQL statements for insert, update, and delete operations, with table names derived from the entity type name.

## Key Details
- **Purpose**: Persist, update, and delete command entities in a MySQL/MariaDB database.
- **Constructor**: `MySqlCommandStore(string connectionString)` – initializes with a raw connection string.
- **Methods**: `SaveAsync<T>`, `UpdateAsync<T>`, `DeleteAsync<T>` – all accept a `CancellationToken` (default `none`).
- **Table naming**: Automatically pluralizes entity name (lowercase + "s") for table names.
- **SQL generation**: `GenerateInsertSql<T>` and `GenerateUpdateSql<T>` build parameterized queries using reflection on public instance properties.
- **Configuration**: Uses `commandTimeout: 30` for all operations.
- **Dependencies**: Dapper for ADO.NET mapping, `MySqlConnector` as the MySQL client.
- **Thread safety**: Not guaranteed; single instance per connection string expected.

## Cross-References
- [[ICommandStore]] — the interface this store implements
- [[AGXIOME.Storage]] — containing namespace for storage implementations

## Source Files
- `AGXIOME.Storage/Stores/MySqlCommandStore.cs` (ingested 2026-05-14)