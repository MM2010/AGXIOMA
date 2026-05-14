---
title: WhatsAppBot
category: entity
tags: [whatsapp, agent, webhook, integration]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# WhatsAppBot

## Summary
A sealed class that provides a WhatsApp Business API integration for AGXIOME agents. It receives incoming webhook messages (text), processes them through an [[IAgxiomeAgent]], and sends the agent's reply back via the WhatsApp Cloud API. This enables conversational AI agents to interact with users over WhatsApp.

## Key Details
- Sealed class; cannot be inherited.
- Constructor requires `phoneNumberId`, `accessToken` (both strings), and an [[IAgxiomeAgent]] instance.
- Uses `HttpClient` with hardcoded Facebook Graph API base URL (`https://graph.facebook.com/v22.0`).
- `ProcessWebhookAsync` takes a raw JSON request body and an optional cancellation token.
- Parses the webhook payload to extract sender (`from`), message text (`body`), and message ID.
- Calls `_agent.RunAsync(new AgentRequest(text))` to generate a response.
- Sends the agent's response back as a text reply to the same sender via `POST /{phoneNumberId}/messages`.
- Returns `true` if any messages were processed; otherwise `false`.
- Authentication via Bearer token in HTTP header.

## Cross-References
- [[IAgxiomeAgent]] — the agent interface used to process incoming messages.
- [[AgentRequest]] — the request model passed to the agent's `RunAsync` method.
- [[AgxiomeAgent]] — the default agent implementation that can be injected into WhatsAppBot.

## Source Files
- `AGXIOME.Interfaces/WhatsApp/WhatsAppBot.cs` (ingested 2026-05-14)