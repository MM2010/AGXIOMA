using Dapper;
using Npgsql;
using AGXIOME.Shared.Interfaces;

namespace AGXIOME.Storage;

/// <summary>Outbox writer: persists domain events for reliable delivery.</summary>
public sealed class OutboxWriter
{
    private readonly string _connectionString;

    public OutboxWriter(string connectionString) => _connectionString = connectionString;

    /// <summary>Write a domain event to the outbox table (same transaction as command).</summary>
    public async Task WriteAsync<T>(T eventPayload, string eventType, string? correlationId = null, NpgsqlTransaction? tx = null, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        if (tx != null) conn.EnlistTransaction(System.Transactions.Transaction.Current!);
        await conn.ExecuteAsync(
            @"INSERT INTO outbox_messages (event_type, event_body_json, correlation_id)
              VALUES (@EventType, @Body::jsonb, @CorrelationId)",
            new { EventType = eventType, Body = System.Text.Json.JsonSerializer.Serialize(eventPayload), CorrelationId = correlationId },
            commandTimeout: 30);
    }
}

/// <summary>Outbox background poller. Call in background service to process pending events.</summary>
public sealed class OutboxPoller
{
    private readonly string _connectionString;
    private readonly IReadOnlyList<IOutboundDispatcher> _dispatchers;

    public OutboxPoller(string connectionString, IEnumerable<IOutboundDispatcher>? dispatchers = null)
    {
        _connectionString = connectionString;
        _dispatchers = dispatchers?.ToList() ?? new List<IOutboundDispatcher>();
    }

    /// <summary>Process up to batchSize pending outbox messages. Call every N seconds.</summary>
    public async Task<int> ProcessBatchAsync(int batchSize = 50, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        var messages = (await conn.QueryAsync<OutboxMessage>(
            @"SELECT id, event_type, event_body_json, correlation_id
              FROM outbox_messages
              WHERE processed_at IS NULL
              ORDER BY created_at
              LIMIT @Limit FOR UPDATE SKIP LOCKED",
            new { Limit = batchSize }, commandTimeout: 30)).ToList();

        foreach (var msg in messages)
        {
            try
            {
                foreach (var d in _dispatchers)
                    await d.DispatchAsync(msg.EventType, msg.EventBodyJson, msg.CorrelationId, ct);

                await conn.ExecuteAsync(
                    "UPDATE outbox_messages SET processed_at = NOW() WHERE id = @Id",
                    new { msg.Id }, commandTimeout: 30);
            }
            catch { /* log error, retry next cycle */ }
        }
        return messages.Count;
    }

    private sealed record OutboxMessage(Guid Id, string EventType, string EventBodyJson, string? CorrelationId);
}