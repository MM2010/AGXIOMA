---
title: ImageUtil
category: entity
tags: [image-processing, utility, multimodal, mime-type, base64]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ImageUtil

## Summary
The `ImageUtil` static class provides utility methods for image handling in vision and multimodal workflows. It supports loading image files as base64 strings, detecting MIME types from file extensions, validating whether a URL points to an image, and building provider-specific image content parts for OpenAI, Anthropic, and Gemini APIs. This abstraction simplifies integrating local image files and URLs into multimodal AI requests.

## Key Details
- **MIME type mapping**: Supports `image/jpeg`, `image/png`, `image/gif`, `image/webp`, `image/bmp` via extension lookup.
- **`LoadImageAsBase64Async(string filePath, CancellationToken)`**: Reads an image file and returns its base64 representation.
- **`DetectMimeType(string path)`**: Returns the MIME type based on the file extension (defaults to `image/png` if unknown).
- **`ValidateImageUrl(string url)`**: Checks that the URL has an absolute URI and a recognized image extension.
- **`BuildOpenAIImageContent(string imageUrl, string? detail = "auto")`**: Returns an anonymous object with `type` and `image_url` fields, converting local files to data URIs automatically.
- **`BuildAnthropicImageContent(string imagePath)`**: Returns an object with `type` and `source` fields, using `url` or `base64` depending on input.
- **`BuildGeminiImageContent(string imagePath)`**: Returns an `inline_data` object with `mime_type` and base64 `data`; for URLs, sets `data` to empty string (expected by Gemini SDK).
- All builder methods handle both HTTP URLs and local file paths transparently.

## Cross-References
- [[Core]] — parent utility namespace

## Source Files
- `AGXIOME.Core/Utils/ImageUtil.cs` (ingested 2026-05-14)