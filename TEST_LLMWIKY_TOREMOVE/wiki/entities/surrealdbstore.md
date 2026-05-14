---
title: SurrealDbStore
category: entity
tags: [storage, surreal-db, command-store, rest-api, ICommandStore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SurrealDbStore

## Summary
`SurrealDbCommandStore` is an implementation of `ICommandStore` that persists and retrieves entities using SurrealDB via its HTTP REST API (JSON over HTTP). It avoids the heavier `SurrealDB.Net` client by wrapping direct API calls. Table names are derived from the entity type name, lowercased and pluralized. Authentication is performed using HTTP Basic Auth.

## Key Details
- Implements `ICommandStore` with `SaveAsync`, `UpdateAsync`, `DeleteAsync` methods for generic entity types.
- Configured via `SurrealDbStoreOptions` record requiring `BaseUrl`, `Namespace`, `Database`, `Username`, `Password`.
- Uses `HttpClient` with Basic Auth and custom `NS` and `DB` headers set per request.
- Table name convention: `typeof(T).Name.ToLowerInvariant() + "s"` (e.g., `MyEntity` → `myentitys`).
- Entity ID is extracted from a public `Id` property; falls back to `Guid.NewGuid().ToString()` if missing.
- `SaveAsync` and `UpdateAsync` both use `PUT /key/{table}/{id}` for full record replacement.
- `DeleteAsync` uses `DELETE /key/{table}/{id}`.
- Constructor accepts optional `HttpClient`; if not provided, creates one with base URL from options.

## Cross-References
- [[ICommandStore]] — the interface this store implements.
- [[StorageProviderType]] — enumeration used by `ProviderType` property.
- [[SurrealDbStoreOptions]] — configuration record for connection settings.

## Source Files
- `AGXIOME.Storage/Stores/SurrealDbStore.cs` (ingested 2026-05-14)