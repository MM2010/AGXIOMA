---
title: Cloud Storage Readers
category: entity
tags: [document-reader, storage, aws, azure, gcp]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Cloud Storage Readers

## Summary
The `CloudStorageReaders` are a set of `IDocumentReader` implementations that download documents from major cloud storage providers: AWS S3, Azure Blob Storage, and Google Cloud Storage (GCS). Each reader is specialized for a single provider, accepts its respective SDK client via constructor injection, and supports both direct URI parsing and stream-based reading. They enrich the returned `DocumentContent` with provider‑specific metadata (ETag, content type, last modified, bucket/container info).

## Key Details
- **AWSS3Reader** (R-19): Uses `IAmazonS3` SDK client. URI formats: `s3://bucket/key` or `https://bucket.s3.region.amazonaws.com/key`. Metadata includes `etag`, `lastModified`, `contentType`, `bucket`, `key`.
- **AzureBlobReader** (R-20): Uses `BlobServiceClient` from `Azure.Storage.Blobs`. URI formats: `azblob://container/blob-path` or full Azure Blob URL. Metadata includes `container`, `blobPath`, `contentType`, `eTag`, `lastModified`.
- **GCSReader** (R-21): Uses `StorageClient` from `Google.Cloud.Storage.V1`. URI formats: `gcs://bucket/object-name` or `gs://bucket/object-name`. Metadata includes `bucket`, `objectName`, `contentType`, `size`, `etag`.
- All readers implement `IDocumentReader` with `ReaderType` set to the corresponding `DocumentReaderType` enum value.
- `SupportedExtensions` is empty (no file‑type filtering).
- `ReadFromStreamAsync` is supported for all three, reading text content from a stream and returning a single `DocumentSection`.
- All require the SDK client to be pre‑configured (e.g., via connection string, managed identity, or application default credentials).
- Constructor throws `ArgumentNullException` if the SDK client is `null`.

## Cross-References
- [[IDocumentReader]] — interface that all cloud storage readers implement
- [[DocumentContent]] — data model returned by `ReadAsync` and `ReadFromStreamAsync`
- [[DocumentReaderType]] — enum used to identify the reader provider
- [[DocumentSection]] — component of `DocumentContent` for segmented text

## Source Files
- `AGXIOME.Storage/Readers/CloudStorageReaders.cs` (ingested 2026-05-14)