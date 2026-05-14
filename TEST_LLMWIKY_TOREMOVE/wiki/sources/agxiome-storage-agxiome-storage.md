---
title: Agxiome Storage
category: source
tags: [storage, .NET, cloud, database, file-parsing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Storage

## Summary
The `AGXIOME.Storage` project provides a unified abstraction layer over dozens of storage backends — including cloud object stores, relational and NoSQL databases, key-value stores, search engines, time-series databases, and file parsing libraries — that are used throughout the AGXIOMA agent framework. By aggregating these dependencies into a single project, it simplifies dependency management and enables the rest of the framework to interact with storage and document processing through consistent interfaces.

## Key Details
- **Targets:** .NET 10.0 with nullable reference types and implicit usings enabled.
- **Cloud Object Stores:** Amazon S3 (via `AWSSDK.S3`), Azure Blob Storage (via `Azure.Storage.Blobs`), Google Cloud Storage (via `Google.Cloud.Storage.V1`).
- **NoSQL & Document Databases:** MongoDB (via `MongoDB.Driver`), Couchbase (via `CouchbaseNetClient`), RavenDB (via `RavenDB.Client`), Azure Cosmos DB (via `Microsoft.Azure.Cosmos`), Google Firestore (via `Google.Cloud.Firestore`), DynamoDB (via `AWSSDK.DynamoDBv2`), etcd (via `dotnet-etcd`).
- **Relational Databases & Microsoft Graph:** SQL Server (via `Microsoft.Data.SqlClient`), PostgreSQL (via `Npgsql`), MySQL (via `MySqlConnector`), SQLite (via `Microsoft.Data.Sqlite`), DuckDB (via `DuckDB.NET.Data.Full`), ClickHouse (via `ClickHouse.Client`), Dapper for micro-ORM, and Microsoft Graph (via `Microsoft.Graph`).
- **Search Engines & Time-Series:** Elasticsearch (via `Elastic.Clients.Elasticsearch`), Meilisearch (via `Meilisearch`), InfluxDB (via `InfluxDB.Client`), Qdrant vector database (via `Qdrant.Client`).
- **Key-Value & In-Memory Caches:** Redis (via `StackExchange.Redis`), RocksDB (via `RocksDbSharp`), Hazelcast (via `Hazelcast.Net`).
- **Document & File Parsing:** PDF (PdfPig), EPUB (`VersOne.Epub`), Excel (ClosedXML & OpenXML), CSV (CsvHelper), HTML (HtmlAgilityPack), Markdown (Markdig), RTF (RtfPipe), YAML (YamlDotNet), plus OCR via Tesseract and compression via SharpCompress/Snappier.
- **Dependency for I/O Abstractions:** Shares the same foundational types (`IAgentStorage`, etc.) defined in [[AGXIOME.Shared]].

## Cross-References
- [[sources/agxiome-shared-agxiome-shared]] — Shared types and interfaces consumed by Storage.
- [[concepts/agent-storage]] — Conceptual overview of storage abstraction in AGXIOMA (if such page exists).
- [[overview/overview]] — High-level framework documentation.

## Source Files
- `AGXIOME.Storage/AGXIOME.Storage.csproj` (ingested 2026-05-14)