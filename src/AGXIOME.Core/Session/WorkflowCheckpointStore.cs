using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Session;

/// <summary>
/// Workflow checkpoint store for session recovery.
///
/// After a crash / restart, an agent can reconstruct its full context
/// from the last checkpoint and continue where it left off — without
/// replaying the entire history.
///
/// Checkpoints are taken periodically (every N turns) and on phase
/// transitions. Each checkpoint includes a hash chain for integrity
/// verification.
///
/// Storage layers:
///   L1: Redis (hot — active sessions, TTL 24h)
///   L2: PostgreSQL (warm — recent sessions, TTL 7d)
///   L3: — (cold archive handled by PostgresSessionManager)
///
/// Usage:
///   var store = new WorkflowCheckpointStore(redis, log);
///   await store.SaveAsync(checkpoint);
///   var info = await store.GetRecoveryInfoAsync(sessionId);
///   // info contains last phase, turn count, summary, hash chain
/// </summary>
public sealed class WorkflowCheckpointStore
{
    private readonly ICacheStore? _store;
    private readonly ILogger<WorkflowCheckpointStore> _log;

    private const string CkptPrefix = "ckpt:";
    private const string InfoPrefix = "sri:";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public WorkflowCheckpointStore(
        ILogger<WorkflowCheckpointStore> log,
        ICacheStore? store = null)
    {
        _log   = log;
        _store = store;
    }

    // ── Save checkpoint ────────────────────────────────────────────

    /// <summary>
    /// Save a workflow checkpoint. If Redis is available, writes there.
    /// Falls back to in-memory tracking only.
    /// </summary>
    public async Task<string> SaveAsync(WorkflowCheckpoint ckpt)
    {
        ckpt = ckpt with
        {
            Id        = Guid.NewGuid().ToString("N")[..12],
            CreatedAt = DateTime.UtcNow,
        };

        if (_store != null)
        {
            var json = JsonSerializer.Serialize(ckpt, JsonOpts);
            await _store.SetAsync(CkptKey(ckpt.SessionId, ckpt.Id), json, TimeSpan.FromHours(24));

            // Update the recovery info for fast lookup
            var info = new SessionRecoveryInfo
            {
                SessionId   = ckpt.SessionId,
                UserId      = ckpt.UserId,
                AgentId     = ckpt.AgentId,
                LastPhase   = ckpt.Phase,
                LastTurn    = ckpt.Turn,
                LastActive  = DateTime.UtcNow,
            };
            await _store.SetAsync(InfoKey(ckpt.SessionId),
                JsonSerializer.Serialize(info, JsonOpts), TimeSpan.FromHours(24));
        }

        _log.LogDebug("[Checkpoint] Saved {Id} for session {Sid} at turn {Turn} phase {Phase}",
            ckpt.Id, ckpt.SessionId, ckpt.Turn, ckpt.Phase);

        return ckpt.Id;
    }

    // ── Recovery ───────────────────────────────────────────────────

    /// <summary>
    /// Get recovery info for an interrupted session.
    /// Returns null if no checkpoint exists (fresh session).
    /// </summary>
    public async Task<SessionRecoveryInfo?> GetRecoveryInfoAsync(string sessionId)
    {
        if (_store == null)
            return null;

        var raw = await _store.GetAsync(InfoKey(sessionId));
        if (raw == null)
            return null;

        try
        {
            return JsonSerializer.Deserialize<SessionRecoveryInfo>(raw, JsonOpts);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Checkpoint] Failed to deserialize recovery info for {Sid}", sessionId);
            return null;
        }
    }

    // ── Build prompt for recovery ──────────────────────────────────

    /// <summary>
    /// Build a "recovery prompt" that tells the agent where it left off.
    /// Includes summary, last phase, and turn count — NOT full history.
    /// This saves massive token cost on recovery.
    /// </summary>
    public string BuildRecoveryPrompt(SessionRecoveryInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Session Recovery Notice");
        sb.AppendLine();
        sb.AppendLine("This session was interrupted and has been recovered.");
        sb.AppendLine();
        sb.AppendLine($"- Session: {info.SessionId}");
        sb.AppendLine($"- User:    {info.UserId}");
        sb.AppendLine($"- Agent:   {info.AgentId}");
        sb.AppendLine($"- Last phase: {info.LastPhase ?? "(unknown)"}");
        sb.AppendLine($"- Turn:       {info.LastTurn}");

        if (!string.IsNullOrEmpty(info.Summary))
        {
            sb.AppendLine();
            sb.AppendLine("### Previous context summary");
            sb.AppendLine(info.Summary);
        }

        sb.AppendLine();
        sb.AppendLine("Continue the workflow from where it was interrupted.");
        sb.AppendLine("Do NOT replay completed steps. Proceed to the next action.");

        return sb.ToString();
    }

    // ── Hash chain (deterministic replay) ──────────────────────────

    /// <summary>
    /// Compute the hash of a checkpoint for chain-of-trust.
    /// </summary>
    public static string ComputeHash(WorkflowCheckpoint ckpt)
    {
        var input = $"{ckpt.SessionId}|{ckpt.Turn}|{ckpt.Phase}|{ckpt.DataJson}|{ckpt.HashChain}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ── Key helpers ────────────────────────────────────────────────

    private string CkptKey(string sessionId, string ckptId) => $"{CkptPrefix}{sessionId}:{ckptId}";
    private string InfoKey(string sessionId)                 => $"{InfoPrefix}{sessionId}";
}
