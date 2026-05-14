---
title: Agxiome Embedders Agxiome Embedders
category: source
tags: [embedders, onnx, tokenizers, agxiome]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Agxiome Embedders Agxiome Embedders

## Summary
The `AGXIOME.Embedders` project provides embedding generation capabilities for the AGXIOME agent framework. It relies on ONNX Runtime for high-performance model inference and Microsoft.ML.Tokenizers for text tokenization. This project depends on the shared types and utilities defined in `AGXIOME.Shared`, and is built for `.NET 10.0` with nullable reference types enabled.

## Key Details
- **Target Framework**: `net10.0`
- **Root Namespace**: `AGXIOME.Embedders`
- **Dependencies**: `AGXIOME.Shared` project reference
- **NuGet Packages**:
  - `Microsoft.ML.OnnxRuntime` v1.26.0 – ONNX model inference
  - `Microsoft.ML.Tokenizers` v2.0.0 – text tokenization for transformer models
- **Language Features**: Implicit usings, nullable enabled

## Cross-References
- [[AGXIOME.Shared]] — provides shared types and abstractions used by the embedders
- [[AGXIOME OnnxRuntime]] — ONNX Runtime integration details (if available)

## Source Files
- `AGXIOME.Embedders/AGXIOME.Embedders.csproj` (ingested 2026-05-14)