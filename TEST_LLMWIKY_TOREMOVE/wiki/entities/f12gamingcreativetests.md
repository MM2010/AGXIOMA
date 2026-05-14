---
title: F12 Gaming Creative Tests
category: entity
tags: [tests, gaming, creative, toolkits, unit-testing]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F12 Gaming Creative Tests

## Summary
This page documents the unit tests for the Gaming & Creative toolkits in the AGXIOME framework, located in the `AGXIOME.Tests.F12` namespace. The tests verify that each toolkit exposes correct identity metadata (id, name, category), returns a `Skipped` status when executed without configuration, and returns an `Ok` status when executed with a valid configuration. The eight toolkits covered are Steam, Twitch, Spotify, Last.fm, Unsplash, Pexels, Giphy, and OpenAI Image Gen.

## Key Details
- **Namespace:** `AGXIOME.Tests.F12`
- **Source file:** `AGXIOME.Tests/F12/F12GamingCreativeTests.cs`
- **Tested Toolkits:** Steam, Twitch, Spotify, Last.fm, Unsplash, Pexels, Giphy, OpenAI Image Gen
- **Base pattern for each toolkit:**
  - `Id_IsCorrect` – verifies the `Id` property matches the expected string
  - `Name_IsCorrect` – verifies the `Name` property
  - `Category_IsCorrect` – verifies the `Category` is `ToolkitCategory.GamingCreative`
  - `NotConfigured_ReturnsSkipped` – calls `ExecuteAsync` without a config and expects `ToolkitResultStatus.Skipped`
  - `Execute_WithConfig_ReturnsOk` – calls `ExecuteAsync` with a config containing an `api_key` and sample parameters, expects `ToolkitResultStatus.Ok`
- **Config class:** `ToolkitConfig` with id and a dictionary of settings (key `api_key` used for all toolkits)
- **Toolkit base:** All toolkits inherit from a common base in `AGXIOME.Toolkits.GamingCreative` and implement an `ExecuteAsync` method

## Cross-References
- [[SteamToolkit]] – the Steam toolkit entity
- [[TwitchToolkit]] – the Twitch toolkit entity
- [[SpotifyToolkit]] – the Spotify toolkit entity
- [[LastFmToolkit]] – the Last.fm toolkit entity
- [[UnsplashToolkit]] – the Unsplash toolkit entity
- [[PexelsToolkit]] – the Pexels toolkit entity
- [[GiphyToolkit]] – the Giphy toolkit entity
- [[OpenAiImageGenToolkit]] – the OpenAI Image Gen toolkit entity
- [[ToolkitCategory]] – the enumeration used to categorize toolkits
- [[entities/toolkitresultstatus]] – status values returned by toolkit execution

## Source Files
- `AGXIOME.Tests/F12/F12GamingCreativeTests.cs` (ingested 2026-05-14)