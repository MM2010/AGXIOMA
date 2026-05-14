---
title: F12 AI/ML Tests
category: entity
tags: [test, ai-ml, toolkit, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 AI/ML Tests

## Summary
This test file validates the AI/ML toolkit implementations within the AGXIOME framework. It covers eight toolkits—OpenAI Vision, DALL‑E, Stable Diffusion, Whisper, ElevenLabs TTS, HuggingFace Hub, ML.NET AutoML, and PyTorch Bridge—ensuring correct identity properties (Id, Name, Category), graceful handling of unconfigured states (returning `Skipped` status), and successful execution when a valid configuration is provided.

## Key Details
- **8 test classes**, each following a uniform pattern: `Id_IsCorrect`, `Name_IsCorrect`, `Category_IsCorrect`, `NotConfigured_ReturnsSkipped`, `Execute_WithConfig_ReturnsOk`.
- All toolkits have `ToolkitCategory.AiMl` and reside in the `AGXIOME.Toolkits.AiMl` namespace.
- `MlNetAutoMlToolkitTests.NotConfigured_ReturnsSkipped` assertion expects `Ok` (not `Skipped`), indicating that ML.NET AutoML does not require external configuration.
- `PyTorchToolkitTests` uses `ToolkitConfig("pytorch_bridge", ...)` while the toolkit ID is `"pytorch"`; this mismatch is reflected in the test but may be intentional or a bug.
- Toolkit tests validate both default (unconfigured) behavior and behavior with a minimal configuration (e.g., `api_key` or dataset parameters).
- The `ExecuteAsync` method is called with an operation name (e.g., `"analyze_image"`, `"generate_image"`, `"transcribe"`) and a dictionary of parameters.

## Cross-References
- [[concepts/Toolkit]] — general toolkit concept in AGXIOME.
- [[entities/F12RootToolkitsTests]] — root-level toolkit test file (if exists).

## Source Files
- `AGXIOME.Tests/F12/F12AiMlTests.cs` (ingested 2026-05-14)