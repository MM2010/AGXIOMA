---
title: Mongostore
category: entity
tags: [mongo, store, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Mongostore

## Summary
`MongoStore` is a sealed implementation of [[ICommandStore|the ICommandStore interface]] that persists agent memory and session state as documents in a MongoDB database. It provides basic CRUD operations with automatic upsert capability and a query method accepting raw MongoDB filter definitions, making it suitable for unstructured, schemaless data storage in the AGXIOME framework.

## Key Details
- Implementation of `ICommandStore` for MongoDB.
- Constructed with a connection string and optional database name (default `"agxiome"`).
- Stores each entity type in a collection named by the lowercased type name with an appended `"s"` (e.g., `MongoSession` → `mongosessions`).
- `SaveAsync<T>` performs an upsert if the entity has an `Id` or `id` property – otherwise inserts.
- `UpdateAsync<T>` replaces the document matching the entity’s `_id` field.
- `DeleteAsync<T>` removes the document by its `_id`.
- `QueryAsync<T>` accepts a `FilterDefinition<T>` and returns up to `limit` results (default 50).
- Entity identity is resolved by reflection: looks for `Id` or `id` public properties.
- Includes a built-in example entity `MongoSession` with `BsonId`, `UserId`, `ComponentId`, a `BsonDocument` `State`, and a `CreatedAt` timestamp.

## Cross-References
- [[ICommandStore]] — the interface that MongoStore implements, defining the store contract.
- [[AGXIOME.Storage]] — the namespace containing the store abstraction and other storage implementations.

## Source Files
- `AGXIOME.Storage/Stores/MongoStore.cs` (ingested 2026-05-14)