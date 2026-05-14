---
title: TelegramBot
category: entity
tags: [agent, telegram, bot, interface]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# TelegramBot

## Summary
The `TelegramBot` class is a sealed entity that bridges Telegram Bot API webhook updates with an AGXIOME agent. It handles webhook URL management, processes incoming messages by invoking the agent, and sends the agent’s response back to the user. This enables AGXIOME agents to interact with users through Telegram.

## Key Details
- Sealed class in `AGXIOME.Interfaces.Telegram` namespace.
- Stores bot token and uses `HttpClient` to call Telegram Bot API (`https://api.telegram.org/bot{token}/...`).
- Requires an `IAgxiomeAgent` instance to process user messages.
- `SetWebhookAsync(url, ct)` registers a webhook URL for receiving updates.
- `ProcessUpdateAsync(requestBody, ct)` parses Telegram update JSON, extracts chat ID and text, runs `_agent.RunAsync(new AgentRequest(text))`, sends reply via `sendMessage`, and returns a `TelegramMessage` record.
- `DeleteWebhookAsync(ct)` removes the registered webhook.
- Returns `null` from `ProcessUpdateAsync` if the JSON does not contain a `message` property.
- `TelegramMessage` record holds `ChatId`, `UserText`, `BotResponse`, and `MessageId`.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used to process messages.
- [[AgentRequest]] — request model passed to the agent.
- [[TelegramMessage]] — result record produced by `ProcessUpdateAsync`.

## Source Files
- `AGXIOME.Interfaces/Telegram/TelegramBot.cs` (ingested 2026-05-14)