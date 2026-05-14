---
title: FirestoreStore
category: entity
tags: [firestore, storage, store, commandstore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# FirestoreStore

## Summary
The `FirestoreCommandStore` is an implementation of the [[ICommandStore]] interface using Google Cloud Firestore as the persistence backend. It stores entities as Firestore documents, derives collection names from the entity type (PascalCase → lower plural + "s"), and supports idempotent upsert operations via `SetAsync` with `MergeAll`. Configuration is managed through the `FirestoreStoreOptions` record, which requires a GCP project ID and optionally accepts a service account credential file path.

## Key Details
- Uses `Google.Cloud.Firestore` SDK; if no credential file is provided, defaults to Application Default Credentials (ADC)
- Collection name derived from `typeof(T).Name.ToLowerInvariant() + "s"` (e.g., `TodoTask` → `todotasks`)
- Document IDs are read from an `Id` property on the entity, or auto-generated if absent
- Serialization: converts entity to `Dictionary<string, object>` via `JsonSerializer` + custom `JsonElement` unwrapping
- `SaveAsync` and `UpdateAsync` both perform upsert with `SetOptions.MergeAll` for idempotency
- `DeleteAsync` removes the document by collection and ID
- Subcollections can be supported in derived classes via explicit `collectionPath` override (design intent, not shown in this code)
- `StorageProviderType` is set to `StorageProviderType.Firestore`
- Errors are not wrapped; callers should handle Firestore-specific exceptions

## Cross-References
- [[ICommandStore]] — interface implemented by this store
- [[StorageProviderType]] — enumeration value identifying the provider
- [[FirestoreStoreOptions]] — configuration record for GCP project and credentials

## Source Files
- `AGXIOME.Storage/Stores/FirestoreStore.cs` (ingested 2026-05-14)