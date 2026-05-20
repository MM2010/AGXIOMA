using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Unified agent memory interface that ties together:
///   - Session management (existing ISessionManager)
///   - Pointer cache (ContentPointerCache)
///   - Token budget (TokenBudgetTracker)
///   - Checkpoint recovery (WorkflowCheckpointStore)
///
/// Implementations can be memory-only, Redis-backed, or full
/// PostgreSQL. The interface is designed to be strategy-agnostic:
/// the AgentMemoryConfig decides which features are active.
///
/// Usage (DI registration):
///   builder.Services.AddSingleton&lt;IAgentMemoryStore, AgentMemoryStore&gt;();
///
/// Configuration (appsettings.json):
///   "Agent:Memory": {
///     "Strategy": "pointer",
///     "TokenBudget": 4000,
///     "SummaryEvery": 5,
///     "Pointers": { "Enabled": true, "TtlHours": 24 }
///   }
/// </summary>
public interface IAgentMemoryStore
{
    // ── Session lifecycle ──────────────────────────────────────────

    /// <summary>Create a new session.</summary>
    Task<AgentSession> CreateSessionAsync(
        string userId, string componentId, SessionType type,
        string? name = null, CancellationToken ct = default);

    /// <summary>Get session by ID.</summary>
    Task<AgentSession?> GetSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Recover or create session (auto-detects interruption).</summary>
    Task<(AgentSession Session, SessionRecoveryInfo? Recovery)> GetOrRecoverAsync(
        string sessionId, string userId, string componentId,
        SessionType type, CancellationToken ct = default);

    // ── Message history ────────────────────────────────────────────

    /// <summary>Append a message to session history.</summary>
    Task AddMessageAsync(string sessionId, AgentMessage msg, CancellationToken ct = default);

    /// <summary>Get recent messages (up to N).</summary>
    Task<IReadOnlyList<AgentMessage>> GetHistoryAsync(
        string sessionId, int? lastN = null, CancellationToken ct = default);

    // ─── Prompt building (pointer-optimized) ───────────────────────

    /// <summary>
    /// Build an optimised prompt for the LLM call.
    /// Uses pointer cache to skip already-sent content.
    /// Applies token budget (drops low-priority segments).
    /// </summary>
    Task<BuiltPrompt> BuildPromptAsync(
        string sessionId, string userMessage,
        string systemPrompt, string agentDefinition,
        CancellationToken ct = default);

    // ── Checkpoint / Recovery ──────────────────────────────────────

    /// <summary>Save a workflow checkpoint.</summary>
    Task<string> SaveCheckpointAsync(WorkflowCheckpoint ckpt);

    /// <summary>Generate and persist a summary for the session.</summary>
    Task<string?> GenerateSummaryAsync(string sessionId, string summary);

    /// <summary>Get session summary if available.</summary>
    Task<string?> GetSummaryAsync(string sessionId);

    // ── Agent memory (cross-session) ───────────────────────────────

    /// <summary>Store a semantic fact for cross-session retrieval.</summary>
    Task RememberAsync(string agentId, string key, string value);

    /// <summary>Retrieve a stored fact.</summary>
    Task<string?> RecallAsync(string agentId, string key);

    // ── Cleanup ────────────────────────────────────────────────────

    /// <summary>Delete session and all associated data.</summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}
