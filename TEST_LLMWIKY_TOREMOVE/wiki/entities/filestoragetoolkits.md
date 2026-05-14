---
title: File Storage Toolkits
category: entity
tags: [file-storage, toolkit, local-file, compression, pdf, image-processing, ftp-sftp, cloud]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# File Storage Toolkits

## Summary
The `AGXIOME.Toolkits.FileStorage` namespace provides a collection of toolkits for performing file and storage operations. It includes a fully implemented local filesystem toolkit (`LocalFileToolkit` with 18 actions), a compression toolkit (zip/gzip), a PDF toolkit (text extraction, metadata), an image processing toolkit (resize, convert, crop, etc.), and stub toolkits for FTP/SFTP and cloud storage providers. These toolkits follow the standard `ToolkitBase` pattern and require no API keys for the local operations, while the FTP/SFTP and cloud toolkits require additional NuGet packages and vendor SDKs.

## Key Details
- **`LocalFileToolkit`** (T-132): 18 actions including `read`, `read_bytes`, `write`, `append`, `write_lines`, `delete`, `list`, `exists`, `copy`, `move`, `mkdir`, `rmdir`, `info`, `search`, `head`, `tail`, `hash`. No external dependencies.
- **`CompressionToolkit`** (T-139): Built on `System.IO.Compression`. Actions: `zip`, `unzip`, `list_zip`, `add_entry`, `gzip_compress`, `gzip_decompress`.
- **`PdfToolkit`** (T-141): Uses `UglyToad.PdfPig`. Actions: `extract_text`, `page_count`, `extract_page`, `metadata`, `search_text`.
- **`ImageProcessingToolkit`** (T-140): Uses `SixLabors.ImageSharp`. Actions: `resize`, `convert`, `info`, `crop`, `grayscale`, `rotate`, `flip`, `thumbnail`.
- **`FtpSftpToolkit`** (T-133): Stub requiring `FluentFTP` and `SSH.NET` packages. Not yet fully implemented.
- **Cloud toolkits** (T-134–T-138): Stubs requiring vendor SDKs (e.g., Azure Blob, AWS S3, Google Cloud Storage).
- All toolkits inherit from `ToolkitBase` and are registered with `[Toolkit]` attribute using `ToolkitCategory.FileStorage`.
- Local file actions accept optional `encoding`, `overwrite`, `pattern`, `recursive` parameters in the parameter dictionary.

## Cross-References
- [[ToolkitBase]] — base class for all toolkits, provides `ExecuteCoreAsync` pattern.
- [[ToolkitConfig]] — configuration passed at construction, optional for local toolkit.
- [[ToolkitCategory]] — used to group toolkits under `FileStorage`.

## Source Files
- `AGXIOME.Toolkits/FileStorage/FileStorageToolkits.cs` (ingested 2026-05-14)