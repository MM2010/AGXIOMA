---
title: CosmosDbStore
category: entity
tags: [cosmosdb, storage, provider, commandstore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# CosmosDbStore

## Summary
`CosmosDbCommandStore` is a concrete implementation of `ICommandStore` that persists aggregate commands and events to Azure Cosmos DB using the Microsoft.Azure.Cosmos SDK v3. It uses upsert semantics for both save and update operations, providing idempotent insert-or-replace behavior. The store defaults the partition key to `/id` and supports either serverless or provisioned throughput configurations via `CosmosDbStoreOptions`.

## Key Details
- Implements `ICommandStore` and `IDisposable`.
- Provider type is `StorageProviderType.CosmosDB`.
- Requires a `CosmosDbStoreOptions` record with `ConnectionString`, `DatabaseId`, and `ContainerId`; `PartitionKeyPath` defaults to `/id`.
- `SaveAsync<T>` and `UpdateAsync<T>` both call `UpsertItemAsync` – no partial document patch support.
- `DeleteAsync<T>` uses `DeleteItemAsync` with the document ID as partition key.
- `Dispose` disposes the internal `CosmosClient` instance.
- Designed with a single container and no built-in change feed or leasing logic (intended for command/event storage, not projections).

## Cross-References
- [[ICommandStore]] — interface that this store implements
- [[StorageProviderType]] — enumeration that distinguishes storage backends
- [[InMemoryStore]] — alternative store implementation for testing

## Source Files
- `AGXIOME.Storage/Stores/CosmosDbStore.cs` (ingested 2026-05-14)