---
title: Outbox
category: entity
tags: [outbox, messaging, persistence]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# Outbox

## Summary
The Outbox pattern guarantees reliable delivery of domain events by persisting them to a dedicated database table within the same transaction as the originating command. In the AGXIOMA framework, the `OutboxWriter` writes events, and the `OutboxPoller` asynchronously processes pending messages for forwarding to a message bus (e.g., SignalR, RabbitMQ). This ensures at-least-once delivery without coupling command execution to external infrastructure.

## Key Details
- `OutboxWriter.WriteAsync<T>` serializes the event payload to JSON and inserts a row into the `outbox_messages` table.
- `OutboxWriter` optionally accepts a `NpgsqlTransaction` to enlist the write in the same transaction as the caller (e.g., a command handler).
- `OutboxPoller.ProcessBatchAsync` queries for unprocessed events (`processed_at IS NULL`) using `FOR UPDATE SKIP LOCKED` to safely handle concurrent pollers.
- Each message is processed by publishing to an external bus (marked as TODO) and then marking it as processed via `UPDATE outbox_messages SET processed_at = NOW()`.
- Errors during processing are caught and logged; the poller retries on the next cycle (no back-off logic shown).
- Default batch size is 50, configurable per call. Query uses `LIMIT` and `ORDER BY created_at` to process oldest first.
- The `OutboxMessage` record holds `Id`, `EventType`, `EventBodyJson`, and optional `CorrelationId`.

## Cross-References
- [[Domain Events]] — events that flow through the outbox for reliable delivery.
- [[Command Handling]] — outbox writes are typically coordinated within the same database transaction as command execution.

## Source Files
- `AGXIOME.Storage/Outbox.cs` (ingested 2026-05-14)