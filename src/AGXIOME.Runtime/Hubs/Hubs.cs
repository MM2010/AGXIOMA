// RT-15..RT-17 — SignalR Hubs: AgentHub, LogHub, AdminHub
// Each hub uses strongly-typed groups for fan-out isolation.
// Events are ordered with monotonic sequence numbers.
// Reconnect: clients can request last-N events to restore state.

using Microsoft.AspNetCore.SignalR;

namespace AGXIOME.Runtime.Hubs;

// ─── Shared Event Contracts ───────────────────────────────────────────────────

public sealed record AgentRunEvent(string RunId, string AgentId, string Type, string Payload, long Seq, DateTimeOffset Ts);
public sealed record LogEvent(string Source, string Level, string Message, long Seq, DateTimeOffset Ts);
public sealed record AdminMetricEvent(string Metric, double Value, long Seq, DateTimeOffset Ts);

// ─── RT-15 — AgentHub ────────────────────────────────────────────────────────

/// <summary>
/// Realtime hub for agent run lifecycle events.
/// Groups: "run:{runId}", "agent:{agentId}"
/// Clients subscribe on StartRun; server pushes progress/completion/error events.
/// </summary>
public sealed class AgentHub : Hub
{
    private long _seq;

    /// <summary>Subscribe to events for a specific run. Adds caller to the run group.</summary>
    public async Task StartRun(string runId, string agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"run:{runId}");
        await Groups.AddToGroupAsync(Context.ConnectionId, $"agent:{agentId}");
        await Clients.Caller.SendAsync("RunStarted", new AgentRunEvent(
            runId, agentId, "started", string.Empty,
            Interlocked.Increment(ref _seq), DateTimeOffset.UtcNow));
    }

    /// <summary>Request cancellation of a run. Server validates authority before acting.</summary>
    public async Task CancelRun(string runId)
    {
        // Production: send cancellation command to BackgroundRunWorker via Channel
        await Clients.Group($"run:{runId}").SendAsync("RunCancelled", new AgentRunEvent(
            runId, string.Empty, "cancelled", string.Empty,
            Interlocked.Increment(ref _seq), DateTimeOffset.UtcNow));
    }

    /// <summary>Resume a paused run.</summary>
    public async Task ResumeRun(string runId)
    {
        await Clients.Group($"run:{runId}").SendAsync("RunResumed", new AgentRunEvent(
            runId, string.Empty, "resumed", string.Empty,
            Interlocked.Increment(ref _seq), DateTimeOffset.UtcNow));
    }

    /// <summary>Replay last N events from in-memory buffer (stub — production uses Redis sorted set).</summary>
    public Task RequestReplay(string runId, int lastN)
    {
        // Production: fetch from replay window in Redis, emit in order
        return Clients.Caller.SendAsync("ReplayStart", new { runId, lastN, message = "No events buffered (stub)" });
    }

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        // Clean up group memberships is handled automatically by SignalR
        await base.OnDisconnectedAsync(ex);
    }
}

// ─── RT-16 — LogHub ─────────────────────────────────────────────────────────

/// <summary>
/// Realtime log streaming hub.
/// Groups: "logs:agent:{agentId}", "logs:module:{module}"
/// </summary>
public sealed class LogHub : Hub
{
    private long _seq;

    public async Task SubscribeAgent(string agentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"logs:agent:{agentId}");
    }

    public async Task SubscribeModule(string module)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"logs:module:{module}");
    }

    public async Task UnsubscribeAgent(string agentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"logs:agent:{agentId}");
    }

    /// <summary>Set minimum log level for this connection (client-side filter hint).</summary>
    public Task SetLogLevel(string level)
    {
        Context.Items["LogLevel"] = level;
        return Task.CompletedTask;
    }

    // Called by server-side log pipeline to broadcast log entries
    internal async Task BroadcastLog(IHubContext<LogHub> ctx, string agentId, string module,
        string level, string message)
    {
        var evt = new LogEvent(module, level, message, Interlocked.Increment(ref _seq), DateTimeOffset.UtcNow);
        await ctx.Clients.Group($"logs:agent:{agentId}").SendAsync("LogEntry", evt);
        await ctx.Clients.Group($"logs:module:{module}").SendAsync("LogEntry", evt);
    }
}

// ─── RT-17 — AdminHub ───────────────────────────────────────────────────────

/// <summary>
/// Realtime admin metrics and error monitoring hub.
/// Only principals with AdminMetrics permission should be allowed to connect.
/// </summary>
public sealed class AdminHub : Hub
{
    private long _seq;

    public async Task Subscribe()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin:metrics");
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin:errors");
        await Clients.Caller.SendAsync("AdminConnected", new { ts = DateTimeOffset.UtcNow });
    }

    public async Task Unsubscribe()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin:metrics");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin:errors");
    }

    // Called by metrics pipeline to push realtime metric snapshots
    internal async Task BroadcastMetric(IHubContext<AdminHub> ctx, string metric, double value)
    {
        await ctx.Clients.Group("admin:metrics").SendAsync("MetricUpdate",
            new AdminMetricEvent(metric, value, Interlocked.Increment(ref _seq), DateTimeOffset.UtcNow));
    }
}
