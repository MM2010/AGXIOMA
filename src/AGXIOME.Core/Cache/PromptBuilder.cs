using AGXIOME.Core.Session;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Unified prompt builder that orchestrates pointer cache, budget tracker,
/// and checkpoint recovery to build the most token-efficient prompt possible.
///
/// Flow:
///   1. Check for session recovery (interrupted session → load checkpoint)
///   2. Register system/agent content as pointers (cache HIT on subsequent turns)
///   3. Build segments with priorities
///   4. Apply budget (drop low-priority segments if over limit)
///   5. Build final prompt (skip already-sent pointers)
///
/// Thread-safe. Call once per turn.
///
/// Usage:
///   var prompt = await builder.BuildAsync(session, userMessage, profile);
///   // send prompt.Prompt to LLM
///   // prompt.SavedTokens tells you what you saved
/// </summary>
public sealed class PromptBuilder
{
    private readonly ContentPointerCache    _ptrs;
    private readonly TokenBudgetTracker     _budget;
    private readonly WorkflowCheckpointStore _ckpts;
    private readonly ILogger<PromptBuilder>  _log;
    private readonly AgentMemoryConfig       _config;

    public PromptBuilder(
        ContentPointerCache    ptrs,
        TokenBudgetTracker     budget,
        WorkflowCheckpointStore ckpts,
        AgentMemoryConfig       config,
        ILogger<PromptBuilder>  log)
    {
        _ptrs   = ptrs;
        _budget = budget;
        _ckpts  = ckpts;
        _config = config;
        _log    = log;
    }

    // ── Main entry point ───────────────────────────────────────────

    /// <summary>
    /// Build an optimised prompt for the given session and user message.
    /// </summary>
    public async Task<BuiltPrompt> BuildAsync(
        AgentSession session,
        string systemPrompt,
        string agentDefinition,
        IReadOnlyList<AgentMessage> recentHistory,
        string userMessage,
        CacheProfile profile,
        CancellationToken ct = default)
    {
        _budget.OnTurn();

        // 1. Check recovery
        var recoveryInfo = await _ckpts.GetRecoveryInfoAsync(session.Id);
        var recoveryPrompt = recoveryInfo != null
            ? _ckpts.BuildRecoveryPrompt(recoveryInfo)
            : null;

        // 2. Get advice
        var advice = _budget.GetAdvice(recentHistory, profile);
        if (advice == CacheAdvice.CompressAndSummarize)
        {
            _log.LogInformation("[PromptBuilder] Budget threshold reached — summary due");
        }

        // 3. Register and build segments
        var segments = new List<PromptSegment>();

        // Frozen: system prompt (register once, cached forever)
        var sysPtr = await _ptrs.RegisterAsync(systemPrompt);
        segments.Add(new PromptSegment
        {
            Ptr      = sysPtr,
            Content  = systemPrompt,
            Priority = SegmentPriority.Frozen,
            TokenEst = EstimateTokens(systemPrompt),
        });

        // Frozen: agent definition
        var agentPtr = await _ptrs.RegisterAsync(agentDefinition);
        segments.Add(new PromptSegment
        {
            Ptr      = agentPtr,
            Content  = agentDefinition,
            Priority = SegmentPriority.Frozen,
            TokenEst = EstimateTokens(agentDefinition),
        });

        // Recovery prompt (if resuming)
        if (recoveryPrompt != null)
        {
            var recPtr = await _ptrs.RegisterAsync(recoveryPrompt);
            segments.Add(new PromptSegment
            {
                Ptr      = recPtr,
                Content  = recoveryPrompt,
                Priority = SegmentPriority.Summary,
                TokenEst = EstimateTokens(recoveryPrompt),
            });
        }

        // Recent messages (variable, limited by MaxRecentMsg)
        var recent = recentHistory.TakeLast(_config.MaxRecentMsg).ToList();
        foreach (var msg in recent)
        {
            var msgPtr = await _ptrs.RegisterAsync(msg.Content);
            segments.Add(new PromptSegment
            {
                Ptr      = msgPtr,
                Content  = FormatMessage(msg),
                Priority = SegmentPriority.Context,
                TokenEst = EstimateTokens(msg.Content),
            });
        }

        // User query (always included)
        segments.Add(new PromptSegment
        {
            Ptr      = "",
            Content  = userMessage,
            Priority = SegmentPriority.Query,
            TokenEst = EstimateTokens(userMessage),
        });

        // 4. Apply budget
        segments = _budget.ApplyBudget(segments, profile);

        // 5. Build final prompt (skip already-sent pointers)
        var result = _ptrs.BuildPrompt(segments);

        _log.LogInformation(
            "[PromptBuilder] Built {Total} tok prompt, saved {Saved} tok via ptr cache ({Ratio:P1})",
            result.TotalTokens, result.SavedTokens, result.CacheRatio);

        // 6. Checkpoint logic: save if summary threshold reached
        if (advice == CacheAdvice.CompressAndSummarize)
        {
            await _ckpts.SaveAsync(new WorkflowCheckpoint
            {
                SessionId    = session.Id,
                UserId       = session.UserId,
                AgentId      = session.ComponentId,
                Phase        = "active",
                Turn         = recentHistory.Count,
                ContextHash  = ComputeSimpleHash(systemPrompt + agentDefinition),
            });
            _budget.OnSummaryGenerated();
        }

        return result;
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static string FormatMessage(AgentMessage msg) =>
        $"{msg.Role}: {msg.Content}";

    private static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 3.5);

    private static string ComputeSimpleHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes, 0, 8).ToLowerInvariant();
    }
}
