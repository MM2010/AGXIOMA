---
title: IStorageInterfaces
category: entity
tags: [interfaces, storage, CQRS, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# IStorageInterfaces

## Summary
The `ICommandStore`, `IQueryStore<T>`, `ISpecification<T>`, `IPaginatedResult<T>`, and `IUnitOfWork` interfaces together define the storage abstraction layer in the AGXIOME framework. Following the Command-Query Responsibility Segregation (CQRS) pattern, they separate write operations (commands) from read operations (queries), while providing specification-based querying, paginated results, and transaction support via a unit of work. These interfaces enable interchangeable backends and testable data access.

## Key Details
- **`ICommandStore`** provides `SaveAsync`, `UpdateAsync`, and `DeleteAsync` for mutable operations on entities.
- **`IQueryStore<T>`** offers `GetByIdAsync`, `QueryAsync` (using `ISpecification<T>`), and `QueryPagedAsync` for read-only access.
- **`ISpecification<T>`** encapsulates a query predicate as an `Expression<Func<T, bool>>` to decouple query logic from the store.
- **`IPaginatedResult<T>`** holds `Items`, `TotalCount`, `Page`, `PageSize`, and `TotalPages` for standard pagination metadata.
- **`IUnitOfWork`** combines transaction management (`BeginAsync`, `CommitAsync`, `RollbackAsync`) with an `ICommandStore` property, ensuring atomicity across commands.
- All interfaces use `CancellationToken` for async operations, following the standard .NET pattern.
- Generic constraints (`where T : class`) ensure entity types are reference types.

## Cross-References
- [[ISpecification]] — the specification pattern used by `IQueryStore<T>` for flexible filtering.
- [[IEntity]] — likely entity base types that implement the classes used with these stores.
- [[IUnitOfWork]] — details the transactional wrapper for command store operations.

## Source Files
- `AGXIOME.Shared/Interfaces/IStorageInterfaces.cs` (ingested 2026-05-14)