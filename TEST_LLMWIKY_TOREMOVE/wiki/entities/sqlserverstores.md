---
title: SqlServerStores
category: entity
tags: [SQL Server, Storage, Command Store, Dapper, LocalDB]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SqlServerStores

## Summary
The `SqlServerCommandStoreBase` abstract class provides a shared implementation of `ICommandStore` for both full SQL Server (enterprise) and SQL Server LocalDB (development) environments. It leverages Dapper and `Microsoft.Data.SqlClient` to execute CRUD operations with a `MERGE` (upsert) pattern, ensuring idempotent saves. Concrete classes `SqlServerCommandStore` and `SqlServerLocalDbCommandStore` offer straightforward configuration via dedicated options records and differentiate themselves through the `StorageProviderType` enum.

## Key Details
- **Abstract base class**: `SqlServerCommandStoreBase` implements `ICommandStore` with `SaveAsync`, `UpdateAsync`, and `DeleteAsync` methods.
- **Enterprise store**: `SqlServerCommandStore` uses `SqlServerStoreOptions` and returns `StorageProviderType.SQLServer`.
- **LocalDB store**: `SqlServerLocalDbCommandStore` uses `SqlServerLocalDbStoreOptions` and returns `StorageProviderType.SQLServerLocalDB`.
- **Upsert via MERGE**: `SaveAsync` executes a SQL Server `MERGE` statement with `Id` as the key, row-by-row with 30-second timeout.
- **Dapper + SqlClient**: All queries use parameterized inputs; no stored procedures required.
- **Table naming convention**: Table name is inferred as the entity class name plus "s" (e.g., `Product` → `Products`).
- **Options records**: Both options are `sealed record` types with required `ConnectionString` property.
- **Id-based deletion**: `DeleteAsync` expects a string `Id` parameter.

## Cross-References
- [[ICommandStore]] — interface that the base class implements
- [[StorageProviderType]] — enum used to identify the store variant

## Source Files
- `AGXIOME.Storage/Stores/SqlServerStores.cs` (ingested 2026-05-14)