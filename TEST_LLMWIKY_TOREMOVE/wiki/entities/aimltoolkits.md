---
title: AI/ML Toolkits
category: entity
tags: [toolkit, ai, ml, openai, vision, dalle, stable-diffusion, whisper]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# AI/ML Toolkits

## Summary
The AI/ML Toolkits module provides a set of agent-accessible toolkits that integrate with popular artificial intelligence and machine learning APIs. These toolkits follow the standard `ToolkitBase` pattern and enable agents to perform vision analysis (OpenAI GPT‑4o), image generation (DALL‑E, Stable Diffusion), and speech‑to‑text (Whisper). Each toolkit requires either an API key or a local endpoint, and exposes configurable actions and parameters.

## Key Details
- **OpenAiVisionToolkit** (`openai_vision`) – Analyzes images using OpenAI’s GPT‑4o or GPT‑4 Vision models. Requires `api_key` setting. Accepts image URL or base64, optional prompt and max tokens.
- **DalleToolkit** (`dalle`) – Generates, edits, or creates variations of images via DALL‑E 3 (default). Requires `api_key`. Actions: `generate`/`create`, `edit`, `variation`.
- **StableDiffusionToolkit** (`stable_diffusion`) – Image generation via Stability AI cloud API or a local Automatic1111/SDAPI endpoint. Requires either `api_key` (cloud) or `base_url` (local). Supports text‑to‑image with standard parameters (prompt, negative prompt, steps, dimensions).
- **WhisperToolkit** (`whisper`) – Speech‑to‑text transcription using OpenAI’s Whisper model or a compatible local endpoint. Requires `api_key` or `base_url`. Accepts audio file path, optional language and prompt.
- All toolkits inherit from `ToolkitBase`, use a shared HTTP client with 120‑second timeout, and expose `IsConfigured` to verify required settings.
- Toolkits are registered with the `ToolkitCategory.AiMl` category and identified by their `Id` (e.g., `openai_vision`, `dalle`, `stable_diffusion`, `whisper`).

## Cross-References
- [[tlbases#entities/toolkitbase|ToolkitBase]] — Base class and configuration pattern used by all AI/ML toolkits.
- [[ToolkitConfig]] — Configuration object passed to toolkit constructors.
- [[ToolkitCategory]] — Enumeration that includes `AiMl` as a category.

## Source Files
- `AGXIOME.Toolkits/AiMl/AiMlToolkits.cs` (ingested 2026-05-14)