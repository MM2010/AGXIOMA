---
title: Communication Toolkits
category: entity
tags: [toolkit, communication, slack, discord, telegram, twilio, sendgrid, smtp]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Communication Toolkits

## Summary
The Communication Toolkits module provides a set of standardized, reusable toolkits for integrating Agent workflows with popular messaging and notification platforms – including Slack, Discord, Telegram, Twilio (SMS), SendGrid (email), and SMTP. Each toolkit inherits from `ToolkitBase`, shares a common configuration pattern (API token or credentials), and exposes a consistent set of actions for sending messages, retrieving history, managing channels, and uploading files. A shared, short-lived HTTP client (`Http.Client` with 30-second timeout) underpins all REST API calls, ensuring predictable performance and resource usage.

## Key Details
- **Common base class**: all communication toolkits derive from `ToolkitBase` and override `ExecuteCoreAsync` to dispatch actions.
- **Configuration**: each toolkit requires a specific setting (e.g., `token` for Slack, `bot_token` for Discord/Telegram, `account_sid`/`auth_token` for Twilio, `api_key` for SendGrid, `smtp_*` for SMTP). Presence of the required setting determines `IsConfigured`.
- **Standard actions** (per platform):
  - Slack: `send_message`, `list_channels`, `get_history`, `upload_file`
  - Discord: `send_message`, `get_messages`, `get_guilds`
  - Telegram: `send_message`, `send_photo`, `send_document`, `get_updates`, `get_me`, `set_webhook`, `delete_webhook`
  - Twilio: `send_sms`
  - SendGrid: `send_email`
  - SMTP: `send_email`
- **Error handling**: all toolkits return `ToolkitResult` – either `Success` (with JSON payload) or `Fail` (with an error message). API errors are parsed and surfaced.
- **Shared HTTP client**: a single `HttpClient` instance (`Http.Client`) with a 30-second timeout is reused across all communication toolkits.
- **Security**: bearer tokens (Slack) or `Bot` prefix (Discord) are injected into request headers; no secrets are logged.
- **File uploads**: Slack supports multipart upload via a two-step process (`getUploadURLExternal` + `upload`).
- **Extensibility**: new communication platforms can be added by implementing a new class decorated with `[Toolkit("id", "Name", ...)]` and following the same pattern.

## Cross-References
- [[ToolkitBase]] — abstract base class all toolkits inherit from
- [[ToolkitConfig]] — configuration object passed to each toolkit constructor
- [[ToolkitCategory]] — enumeration used to group toolkits (e.g., Communication)
- [[HttpClient]] — shared HTTP client used internally

## Source Files
- `AGXIOME.Toolkits/Communication/CommunicationToolkits.cs` (ingested 2026-05-14)