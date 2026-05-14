---
title: ClaudeBridge
category: entity
tags: [bridge, anthropic, claude, xlink]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# ClaudeBridge

## Summary
`ClaudeBridge` is a direct HTTP bridge to Anthropic’s Messages API for Claude models (claude-3.x and claude-3-5). It eliminates external SDK dependencies by using raw HTTP calls and enforces a security-first pattern: the API key must be supplied via the `ANTHROPIC_API_KEY` environment variable and is never accepted from client payloads. The bridge implements the `IClaudeBridge` interface and returns standardized `AgentBridgeResult` objects for both success and failure paths.

## Key Details
- Implements `IClaudeBridge` interface with `CompleteAsync`, `SupportedModels`, and `IsAvailable` members.
- Constructor accepts optional `apiKey` and `HttpClient`; environment variable `ANTHROPIC_API_KEY` always takes precedence (security rule).
- Uses `https://api.anthropic.com/v1/messages` endpoint with `anthropic-version: 2023-06-01` header.
- Supported models (default): `claude-3-haiku-20240307`, `claude-3-sonnet-20240229`, `claude-3-opus-20240229`, `claude-3-5-sonnet-20241022`.
- `CompleteAsync` serializes a JSON body with `model`, `max_tokens`, and `messages` (user role), then parses the `content[0].text` field from the response.
- Returns `AgentBridgeResult` with success flag, content string, optional error message, source name "Claude", and elapsed milliseconds.
- On missing API key, returns an unavailable result immediately without making an HTTP call.
- Catches all exceptions and returns a failed `AgentBridgeResult` with the exception message.

## Cross-References
- [[AgentBridgeResult]] — the result type used by all bridge implementations.
- [[IClaudeBridge]] — the interface contract for Claude bridge implementations.
- [[XLink]] — parent namespace for cross-link bridging components.

## Source Files
- `AGXIOME.XLink/Bridges/ClaudeBridge.cs` (ingested 2026-05-14)