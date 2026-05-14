---
title: DynamoDB Store
category: entity
tags: [dynamodb, storage, store, commandstore]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# DynamoDB Store

## Summary
The `DynamoDbCommandStore` is a concrete implementation of the [[ICommandStore]] interface that stores and retrieves entities using Amazon DynamoDB. It uses the low-level `AWSSDK.DynamoDBv2` client (`PutItem`, `DeleteItem`) for direct table operations, serializing all entity properties to DynamoDB `AttributeValue` maps. The store is designed for on-demand (PAY_PER_REQUEST) billing and requires a `DynamoDbStoreOptions` configuration including table name, service URL, and AWS credentials.

## Key Details
- Implements `ICommandStore` with `SaveAsync`, `UpdateAsync`, and `DeleteAsync` methods.
- Uses `AmazonDynamoDBClient` with `BasicAWSCredentials` and a configurable service endpoint.
- Serialization supports `string`, `int`, `long`, `double`, `float`, `bool`; all other types are JSON-serialized as strings.
- `UpdateAsync` delegates to `SaveAsync` (full replacement via `PutItem`) rather than using DynamoDB's `UpdateItem`.
- `DeleteAsync` requires an `Id` string which is used as the partition key in DynamoDB.
- The `DynamoDbStoreOptions` record requires `TableName`, `ServiceUrl`, `AccessKey`, `SecretKey`; `Region` defaults to `us-east-1`.
- Provider type is `StorageProviderType.DynamoDB`.

## Cross-References
- [[ICommandStore]] — the interface this store implements
- [[StorageProviderType]] — enum value identifying the storage backend
- [[InMemoryStore]] — alternative in-memory implementation

## Source Files
- `AGXIOME.Storage/Stores/DynamoDbStore.cs` (ingested 2026-05-14)