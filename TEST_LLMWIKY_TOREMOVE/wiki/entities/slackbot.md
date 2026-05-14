---
title: SlackBot
category: entity
tags: [slack, bot, agent, events, api, integration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# SlackBot

## Summary
The `SlackBot` class provides a sealed interface for integrating AGXIOME agents with Slack via the Events API. It handles URL verification challenges, processes `app_mention` and `message` events, runs the agent on incoming text, and posts the agent's response back to the Slack channel (optionally in a thread). It is designed to be mounted as an ASP.NET Minimal API endpoint and requires a bot token and signing secret for authentication.

## Key Details
- Uses `IAgxiomeAgent` to process requests via `RunAsync(AgentRequest)`.
- Constructor accepts `botToken`, `signingSecret`, and an `IAgxiomeAgent` instance.
- `ProcessEventAsync(string requestBody, CancellationToken)` parses Slack JSON payloads:
  - Returns `challenge` string for URL verification.
  - For `app_mention` or `message` events, extracts `text`, `channel`, `user`, and optional `thread_ts`, runs the agent, and posts the reply via `chat.postMessage` API.
- `InitializeAsync(CancellationToken)` fetches the bot user ID by calling Slack's `auth.test` endpoint and stores it in `BotUserId`.
- Uses an internal `HttpClient` with Bearer token authorization from `_botToken`.
- All HTTP calls are asynchronous and support cancellation.
- The class is sealed and intended for single-instance use as a minimal API handler.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used for request processing.
- [[AgentRequest]] — the model passed to the agent.
- [[AGXIOME.Core.Agent]] — the core agent implementation that `SlackBot` can work with.

## Source Files
- `AGXIOME.Interfaces/Slack/SlackBot.cs` (ingested 2026-05-14)