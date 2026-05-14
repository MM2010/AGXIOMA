---
title: F12 File Storage Tests
category: entity
tags: [tests, file-storage, toolkit, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 File Storage Tests

## Summary
This entity represents the unit test suite for the F12 file storage toolkits in the AGXIOME .NET agent framework. The tests validate identity (id, name, category), behavior when the toolkit is not configured (should return `Skipped` for most toolkits, `Ok` for local file and compression), and successful execution with a dummy configuration. The suite covers all major file storage providers: Local File, FTP/SFTP, Google Cloud Storage, MinIO, Dropbox, Box, SharePoint, Compression, and Image Processing.

## Key Details
- Contains nine test classes: `LocalFileToolkitTests`, `FtpSftpToolkitTests`, `GcsToolkitTests`, `MinioToolkitTests`, `DropboxToolkitTests`, `BoxToolkitTests`, `SharePointToolkitTests`, `CompressionToolkitTests`, and `ImageProcessingToolkitTests`.
- Each class verifies `Id`, `Name`, and `Category` (all `ToolkitCategory.FileStorage`) using `Assert.Equal`.
- **NotConfigured** tests: most toolkits return `ToolkitResultStatus.Skipped`; `LocalFileToolkit` and `CompressionToolkit` return `Ok` when called without configuration.
- **Execute_WithConfig** tests: all toolkits accept a dummy `ToolkitConfig` (with `api_key = "test"`) and a minimal input dictionary, then return `ToolkitResultStatus.Ok`.
- Toolkits exercise different operations: `write`, `list_files`, `list_objects`, `list_folder`, `compress`, etc.
- The source file is located at `AGXIOME.Tests/F12/F12FileStorageTests.cs`.

## Cross-References
- [[F12FileStorageToolkits]] — the production toolkits being tested (each toolkit's Id and Name match the test assertions)
- [[ToolkitResultStatus]] — the enum used to verify execution outcomes
- [[ToolkitCategory]] — the category enum referenced in tests

## Source Files
- `AGXIOME.Tests/F12/F12FileStorageTests.cs` (ingested 2026-05-14)