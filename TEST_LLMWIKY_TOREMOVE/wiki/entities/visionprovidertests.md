---
title: VisionProviderTests
category: entity
tags: [tests, vision, image-utilities, media-content]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# VisionProviderTests

## Summary
The `VisionProviderTests` class contains unit tests for the core image utility functions and media content models used by the `AGXIORM` framework's vision provider system. It validates MIME type detection, image URL validation, and the construction of provider-specific image content (OpenAI, Anthropic, Gemini), as well as the `MediaContent` and `AgentMessage` data structures.

## Key Details
- Tests `ImageUtil.DetectMimeType` for JPEG, PNG, and WebP image extensions.
- Tests `ImageUtil.ValidateImageUrl` with valid HTTP/HTTPS image URLs and invalid inputs (empty string, non‑URL, non‑image PDF).
- Tests provider‑specific content builders: `BuildOpenAIImageContent`, `BuildAnthropicImageContent`, `BuildGeminiImageContent`—all accept a URL and return non‑null result.
- Tests `MediaContent` creation with all properties (MediaType, Data, MimeType, AltText, Source) are stored correctly.
- Tests `AgentMessage` instantiation with a media attachment confirms role, content, and media collection.

## Cross-References
- [[ImageUtil]] — utility class tested for MIME detection and URL validation
- [[MediaContent]] — data model validated for media storage and creation
- [[AgentMessage]] — message model tested with media attachment
- [[VisionProvider]] — provider abstraction that uses these utilities and models

## Source Files
- `AGXIOME.Tests/VisionProviderTests.cs` (ingested 2026-05-14)