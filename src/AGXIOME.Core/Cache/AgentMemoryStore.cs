using AGXIOME.Core.Session;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Default implementation of <see cref="IAgentMemoryStore"/>.
///
/// Orchestrates:
///   - ISessionManager (existing — in-memory, Redis, or PostgreSQL)
///   - ContentPointerCache (L1 memory + L2 Redis)
///   - TokenBudgetTracker (token-aware prompt trimming)
///   - WorkflowCheckpointStore (session recovery on crash)
///
/// Feature flags from AgentMemoryConfig control which optimisations
/// are active. Defaults are conservative — all features ON.
/// </summary>
public sealed class AgentMemoryStore : IAgentMemoryStore
{
    private readonly ISessionManager         _sessions;
    private readonly ContentPointerCache     _ptrs;
    private readonly TokenBudgetTracker      _budget;
    private readonly WorkflowCheckpointStore _ckpts;
    private readonly PromptBuilder           _builder;
    private readonly AgentMemoryConfig       _config;
    private readonly ILogger<AgentMemoryStore> _log;

    public AgentMemoryStore(
        ISessionManager          sessions,
        ContentPointerCache      ptrs,
        TokenBudgetTracker       budget,
        WorkflowCheckpointStore  ckpts,
        PromptBuilder            builder,
        AgentMemoryConfig        config,
        ILogger<AgentMemoryStore> log)
    {
        _sessions = sessions;
        _ptrs     = ptrs;
        _budget   = budget;
        _ckpts    = ckpts;
        _builder  = builder;
        _config   = config;
        _log      = log;
    }

    // ── Session lifecycle ──────────────────────────────────────────

    public async Task<AgentSession> CreateSessionAsync(
        string userId, string componentId, SessionType type,
        string? name = null, CancellationToken ct = default)
    {
        var session = await _sessions.CreateSessionAsync(
            userId, componentId, type,
            new SessionOptions { Name = name },
            ct);

        if (_config.Pointers.Enabled)
            _ptrs.ResetSession();

        return session;
    }

    public Task<AgentSession?> GetSessionAsync(
        string sessionId, CancellationToken ct = default)
        => _sessions.GetSessionAsync(sessionId, ct);

    public async Task<(AgentSession Session, SessionRecoveryInfo? Recovery)> GetOrRecoverAsync(
        string sessionId, string userId, string componentId,
        SessionType type, CancellationToken ct = default)
    {
        // Try recovery first
        var recovery = await _ckpts.GetRecoveryInfoAsync(sessionId);
        if (recovery != null)
        {
            _log.LogInformation("[MemoryStore] Recovered session {Sid} at turn {Turn}", sessionId, recovery.LastTurn);

            // Reset pointer cache for fresh start
            if (_config.Pointers.Enabled)
                _ptrs.ResetSession();

            // Try to get existing session
            var existing = await _sessions.GetSessionAsync(sessionId, ct);
            if (existing != null)
                return (existing, recovery);
        }

        // Create fresh
        var session = await CreateSessionAsync(userId, componentId, type, null, ct);
        return (session, recovery);
    }

    // ── Message history ────────────────────────────────────────────

    public Task AddMessageAsync(string sessionId, AgentMessage msg, CancellationToken ct = default)
        => _sessions.AddMessageAsync(sessionId, msg, ct);

    public Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(
        string sessionId, int? lastN = null, CancellationToken ct = default)
        => _sessions.GetHistoryAsync(sessionId, lastN, ct);

    // ── Prompt building ────────────────────────────────────────────

    public async Task<BuiltPrompt> BuildPromptAsync(
        string sessionId, string userMessage,
        string systemPrompt, string agentDefinition,
        CancellationToken ct = default)
    {
        var session = await _sessions.GetSessionAsync(sessionId, ct);
        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        var history = await _sessions.GetHistoryAsync(sessionId, lastN: _config.MaxRecentMsg + 5, ct);
        var profile = CacheProfile.FromProvider("deepseek"); // or get from provider

        return await _builder.BuildAsync(
            session, systemPrompt, agentDefinition,
            history, userMessage, profile, ct);
    }

    // ── Checkpoint / Recovery ──────────────────────────────────────

    public Task<string> SaveCheckpointAsync(WorkflowCheckpoint ckpt)
        => _ckpts.SaveAsync(ckpt);

    public async Task<string?> GenerateSummaryAsync(string sessionId, string summary)
    {
        var session = await _sessions.GetSessionAsync(sessionId);
        if (session == null) return null;

        // Store summary in session metadata
        var meta = session.Metadata ?? new Dictionary<string, string>();
        meta["summary"] = summary;
        meta["summary_at"] = DateTime.UtcNow.ToString("O");

        _budget.OnSummaryGenerated();
        return summary;
    }

    public async Task<string?> GetSummaryAsync(string sessionId)
    {
        var session = await _sessions.GetSessionAsync(sessionId);
        return session?.Metadata?.GetValueOrDefault("summary");
    }

    // ── Cross-session memory ───────────────────────────────────────

    // Simple in-memory fallback. In production, use Redis or PostgreSQL.
    private static readonly Dictionary<string, Dictionary<string, string>> _facts = new();

    public Task RememberAsync(string agentId, string key, string value)
    {
        lock (_facts)
        {
            if (!_facts.ContainsKey(agentId))
                _facts[agentId] = new Dictionary<string, string>();
            _facts[agentId][key] = value;
        }
        return Task.CompletedTask;
    }

    public Task<string?> RecallAsync(string agentId, string key)
    {
        lock (_facts)
        {
            if (_facts.TryGetValue(agentId, out var agentFacts))
                return Task.FromResult(agentFacts.GetValueOrDefault(key));
            return Task.FromResult<string?>(null);
        }
    }

    // ── Cleanup ────────────────────────────────────────────────────

    public async Task DeleteSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await _sessions.DeleteSessionAsync(sessionId, ct);

        if (_config.Pointers.Enabled)
            _ptrs.ResetSession();
    }
}
