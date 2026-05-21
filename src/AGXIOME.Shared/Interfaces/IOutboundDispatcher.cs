namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Dispatches an outbox message to an external bus (SignalR, RabbitMQ, etc.).
///
/// Register zero or more implementations in DI:
///   services.AddSingleton&lt;IOutboundDispatcher, SignalRDispatcher&gt;();
///   services.AddSingleton&lt;IOutboundDispatcher, RabbitMqDispatcher&gt;();
///
/// If none registered, the outbox poller silently marks messages as processed
/// (reliable persistence without external dependencies).
/// </summary>
public interface IOutboundDispatcher
{
    /// <summary>Dispatch a single outbox message. Throw to trigger retry.</summary>
    Task DispatchAsync(string eventType, string eventBodyJson, string? correlationId, CancellationToken ct = default);
}
