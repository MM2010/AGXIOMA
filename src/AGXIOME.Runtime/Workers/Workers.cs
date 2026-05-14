// RT-18..RT-20 — Background Workers: BackgroundRunWorker, SessionCleanupJob, TokenRotationJob
// All workers use IHostedService pattern with graceful cancellation.
// BackgroundRunWorker uses Channel<T> for bounded backpressure.

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AGXIOME.Security.Keys;
using AGXIOME.Security.Audit;

namespace AGXIOME.Runtime.Workers;

// ─── Run Command ──────────────────────────────────────────────────────────────

public sealed record RunCommand(string RunId, string AgentId, string Input, CancellationToken UserCt);

// ─── RT-18 — BackgroundRunWorker ──────────────────────────────────────────────

/// <summary>
/// Processes agent run commands from a bounded Channel.
/// Provides backpressure: if the channel is full, StartRun returns 503.
/// Production: replace channel with Redis Streams for distributed fan-out.
/// </summary>
public sealed class BackgroundRunWorker : BackgroundService
{
    private const int Capacity = 256; // bounded to control memory under burst
    private readonly Channel<RunCommand> _channel =
        Channel.CreateBounded<RunCommand>(new BoundedChannelOptions(Capacity)
        {
            FullMode      = BoundedChannelFullMode.DropWrite,
            SingleReader  = false,
            SingleWriter  = false
        });

    private readonly ILogger<BackgroundRunWorker> _log;

    public BackgroundRunWorker(ILogger<BackgroundRunWorker> log) => _log = log;

    /// <summary>Enqueues a run command. Returns false if the channel is at capacity.</summary>
    public bool TryEnqueue(RunCommand cmd) => _channel.Writer.TryWrite(cmd);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var cmd in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            if (stoppingToken.IsCancellationRequested) break;
            try
            {
                await ProcessRunAsync(cmd, stoppingToken);
            }
            catch (OperationCanceledException) { /* graceful shutdown */ }
            catch (Exception ex)
            {
                // Log without exposing PII or secrets
                _log.LogError("Run {RunId} failed: {Error}", cmd.RunId, ex.GetType().Name);
            }
        }
    }

    private static Task ProcessRunAsync(RunCommand cmd, CancellationToken ct)
    {
        // Production: resolve IAgentRunner, execute, publish result event
        // Stub: simulate work and mark complete
        return Task.Delay(TimeSpan.FromMilliseconds(100), ct);
    }
}

// ─── RT-19 — SessionCleanupJob ────────────────────────────────────────────────

/// <summary>
/// Periodically removes expired sessions from in-memory and Redis stores.
/// Runs every CleanupInterval; does not hold locks outside the cleanup window.
/// </summary>
public sealed class SessionCleanupJob : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);
    private readonly ILogger<SessionCleanupJob> _log;

    public SessionCleanupJob(ILogger<SessionCleanupJob> log) => _log = log;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
                await CleanupExpiredSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                _log.LogWarning("Session cleanup error: {ErrorType}", ex.GetType().Name);
            }
        }
    }

    private static Task CleanupExpiredSessionsAsync(CancellationToken ct)
    {
        // Production: query ISessionRepository for sessions past TTL and delete from Redis
        return Task.CompletedTask;
    }
}

// ─── RT-20 — TokenRotationJob ─────────────────────────────────────────────────

/// <summary>
/// Background job that triggers JWT/ECDSA key rotation on the configured schedule.
/// Calls IKeyRotationService.ForceRotate() and writes an audit entry.
/// </summary>
public sealed class TokenRotationJob : BackgroundService
{
    private readonly IKeyRotationService _keys;
    private readonly AuditLogger         _audit;
    private readonly ILogger<TokenRotationJob> _log;
    private static readonly TimeSpan RotationInterval = TimeSpan.FromHours(24);

    public TokenRotationJob(IKeyRotationService keys, AuditLogger audit, ILogger<TokenRotationJob> log)
    {
        _keys  = keys;
        _audit = audit;
        _log   = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RotationInterval, stoppingToken);
                var newKey = _keys.ForceRotate();
                await _audit.LogAsync(AuditAction.KeyRotated, AuditSeverity.Info,
                    "system:token-rotation-job", "SigningKey", newKey.Kid, "rotated");
                _log.LogInformation("Key rotated. New kid: {Kid}", newKey.Kid);
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                _log.LogError("Token rotation failed: {ErrorType}", ex.GetType().Name);
            }
        }
    }
}
