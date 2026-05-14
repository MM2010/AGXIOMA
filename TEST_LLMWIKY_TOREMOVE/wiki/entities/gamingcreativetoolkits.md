---
title: Gaming & Creative Toolkits
category: entity
tags: [gaming, creative, toolkit, steam, twitch, spotify]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Gaming & Creative Toolkits

## Summary
The Gaming & Creative Toolkits entity groups a set of ready-made agent toolkits that wrap popular gaming and creative platform APIs. These toolkits provide a consistent interface for actions such as fetching player summaries (Steam), searching streams (Twitch), retrieving track details (Spotify), and more. Each toolkit handles authentication via API keys or OAuth tokens and caches credentials where applicable, making them directly usable within the AGXIOMA framework after minimal configuration.

## Key Details
- Implemented in the `AGXIOME.Toolkits.GamingCreative` namespace, file `GamingCreativeToolkits.cs`.
- All toolkits inherit from `ToolkitBase` and use a shared static `HttpClient` (timeout 30 seconds).
- **SteamToolkit** — supports actions: `get_player_summaries`, `get_owned_games`, `get_app_details`, `get_news`, `search_games`. Requires optional `api_key` for some endpoints.
- **TwitchToolkit** — supports actions: `get_streams`, `search_channels`, `get_users`, `get_games`, `get_channel_info`. Uses Client Credentials OAuth; requires `client_id` and `client_secret`. Caches access token with 60-second buffer.
- **SpotifyToolkit** — supports actions: `search`, `get_track`, `get_artist`, `get_playlist`, `recommendations`, `current_user`, `new_releases`. Requires a pre‑obtained `access_token`.
- Each toolkit exposes a `ToolkitCategory.GamingCreative` category and can be instantiated with a `ToolkitConfig` parameter.
- The `ExecuteCoreAsync` method dispatches actions by string name; unknown actions return a failure with valid action list.
- Configuration settings are retrieved via `GetSetting()` (from `ToolkitConfig`) at runtime.

## Cross-References
- [[entities/toolkitbase]] — base class used by all toolkits
- [[entities/toolkitconfig]] — configuration provider for settings like API keys
- [[entities/toolkitcategory]] — categorization enum (`GamingCreative`)

## Source Files
- `AGXIOME.Toolkits/GamingCreative/GamingCreativeToolkits.cs` (ingested 2026-05-14)