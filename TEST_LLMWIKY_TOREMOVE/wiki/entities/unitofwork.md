---
title: UnitOfWork
category: entity
tags: [unit-of-work, transaction, postgresql, command-store, storage]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# UnitOfWork

## Summary
`UnitOfWork` provides an explicit transaction boundary for command-side operations in the AGXIOMA framework. It wraps a PostgreSQL connection and transaction, exposing a command store for writing events and commands. The class enforces manual control over transaction lifecycle via `BeginAsync`, `CommitAsync`, and `RollbackAsync`, and implements `IDisposable` for clean resource management.

## Key Details
- Sealed class implementing `IUnitOfWork` interface.
- Creates and manages a single `NpgsqlConnection` and `NpgsqlTransaction`.
- Exposes a `Commands` property of type `PostgresCommandStore` for command persistence.
- Supports explicit begin, commit, and rollback operations with cancellation tokens.
- Dispose releases both transaction and connection resources.
- Constructor takes a `connectionString` parameter; connection is opened only at `BeginAsync`.

## Cross-References
- [[IUnitOfWork]] — interface contract for unit-of-work pattern
- [[PostgresCommandStore]] — concrete store used for command operations
- [[ICommandStore]] — interface exposed by `Commands` property

## Source Files
- `AGXIOME.Storage/UnitOfWork.cs` (ingested 2026-05-14)