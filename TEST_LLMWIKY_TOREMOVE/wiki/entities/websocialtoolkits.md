---
title: WebSocialToolkits
category: entity
tags: [toolkit, social-media, web-api, AGXIOME]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WebSocialToolkits

## Summary
The `AGXIOME.Toolkits.WebSocial` namespace provides a collection of toolkit implementations for interacting with major web and social media platforms (Twitter/X, Reddit, LinkedIn, Instagram, YouTube, and more). Each toolkit wraps a platform's API behind a consistent `ToolkitBase` interface, handling authentication, request formatting, and response parsing. These toolkits enable agents to search, retrieve profiles, timelines, and media from social networks without exposing raw HTTP calls.

## Key Details
- **TwitterToolkit** (ID `twitter`) — Requires a bearer token. Supports actions: `search`, `get_tweet`, `get_user`, `user_timeline`.
- **RedditToolkit** (ID `reddit`) — No authentication required for public data. Supports actions: `posts`, `search`, `comments`, `info`.
- **LinkedInToolkit** (ID `linkedin`) — Requires an OAuth2 access token. Supports actions: `get_profile`, `get_email`, `get_network`.
- **InstagramToolkit** (ID `instagram`) — Requires a Meta Graph API access token and optional user ID. Supports actions: `get_profile`, `list_media`, `get_insights`.
- **YouTubeToolkit** (ID `youtube`) — Requires an API key (from truncated snippet). Supports video search and channel info.
- All toolkits inherit from `ToolkitBase` and use a shared static `HttpClient` (30‑second timeout) for requests.
- Configuration is managed via `ToolkitConfig` with settings such as `bearer_token`, `access_token`, `api_key`, etc.
- Each toolkit overrides `ExecuteCoreAsync` to dispatch actions and return `ToolkitResult` containing JSON data or an error.
- The category for all is `ToolkitCategory.WebSocial`.

## Cross-References
- [[ToolkitBase]] — base class used by all web/social toolkits
- [[ToolkitConfig]] — configuration model for toolkit settings
- [[ToolkitResult]] — result type returned by toolkit operations
- [[TwitterToolkit]], [[RedditToolkit]], [[LinkedInToolkit]], [[InstagramToolkit]], [[YouTubeToolkit]] — individual toolkit pages (if created)

## Source Files
- `AGXIOME.Toolkits/WebSocial/WebSocialToolkits.cs` (ingested 2026-05-14)