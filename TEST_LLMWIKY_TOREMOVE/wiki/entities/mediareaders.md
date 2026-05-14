---
title: Mediareaders
category: entity
tags: [OCR, Tesseract, Whisper, audio transcription, media reader, image OCR]
source_count: 2
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Mediareaders

## Summary
The **Mediareaders** entity comprises two concrete implementations of `IDocumentReader` that process non-textual media: `ImageOcrReader` uses the Tesseract OCR engine to extract text from images, and `AudioWhisperReader` sends audio files to the OpenAI Whisper API for transcription with optional timestamped segments. Both readers follow the same `ReadAsync` / `ReadFromStreamAsync` pattern and return structured `DocumentContent` enriched with metadata such as OCR confidence or detected language.

## Key Details
- `ImageOcrReader` (R-24): Requires native Tesseract 5.x libraries and tessdata language packs at runtime. Constructor accepts `dataPath` (directory containing `tessdata/`) and optional `language` (default "eng").
- `AudioWhisperReader` (R-25): Requires an `HttpClient` pre‑configured with base address `https://api.openai.com/` and a Bearer token. Accepts a model name (default "whisper-1").
- Both readers implement `IDocumentReader` and define a `ReaderType` property (`DocumentReaderType.ImageOCR` and `DocumentReaderType.AudioWhisper` respectively).
- Supported extensions for `ImageOcrReader`: `.png`, `.jpg`, `.jpeg`, `.tiff`, `.tif`, `.bmp`, `.gif`.
- Supported extensions for `AudioWhisperReader`: `.mp3`, `.mp4`, `.mpeg`, `.mpga`, `.m4a`, `.wav`, `.webm`.
- `ImageOcrReader` copies stream data to a `MemoryStream` internally because Tesseract requires seekable byte arrays. It returns OCR confidence in metadata (`ocrConfidence`).
- `AudioWhisperReader` builds a multipart/form-data request with `response_format=verbose_json` to obtain segment timestamps. Sections are created for each segment; metadata includes segment start/end times and detected language.
- Both readers are sealed and handle cancellation via `CancellationToken`.

## Cross-References
- [[IDocumentReader]] — the interface both readers implement
- [[DocumentReaderType]] — enumeration used to identify reader capabilities
- [[DocumentContent]] — the output model produced by all readers
- [[DocumentSection]] — used to represent segmented content (e.g., timestamped audio segments)

## Source Files
- `AGXIOME.Storage/Readers/MediaReaders.cs` (ingested 2026-05-14)