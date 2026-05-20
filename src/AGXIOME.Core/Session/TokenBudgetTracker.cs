using System.Diagnostics.CodeAnalysis;
using AGXIOME.Core.Cache;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Session;

/// <summary>
/// Token budget tracker with compression thresholds.
///
/// Decides what to include/exclude from the prompt based on:
///   - Hard limit (max tokens for the LLM call)
///   - Cache window (provider cache delta before invalidation)
///   - Priority of each segment (frozen > summary > context)
///
/// Thread-safe. Designed to be called once per turn.
///
/// Usage:
///   var advice = budget.GetAdvice(session.History, profile);
///   if (advice == CacheAdvice.CompressAndSummarize)
///       await GenerateSummaryAsync(session);
///   var built = budget.ApplyBudget(segments, profile);
/// </summary>
public sealed class TokenBudgetTracker
{
    private readonly AgentMemoryConfig _config;
    private int _turnsSinceLastSummary;

    public TokenBudgetTracker(AgentMemoryConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Returns advice on what to do given current history and profile.
    /// Call BEFORE building the prompt.
    /// </summary>
    public CacheAdvice GetAdvice(
        IReadOnlyList<AgentMessage> history,
        CacheProfile profile)
    {
        var estimated = EstimateTokens(history);

        // Still within budget? Send everything.
        if (estimated <= _config.TokenBudget)
            return CacheAdvice.Ok;

        // Approaching cache invalidation threshold?
        var tailPercent = (double)_config.MaxRecentMsg / Math.Max(history.Count, 1);
        if (tailPercent < 0.3 && estimated > _config.TokenBudget * profile.CompressThreshold)
            return CacheAdvice.ShrinkWindow;

        // Time for a summary checkpoint?
        if (_config.SummaryEvery > 0 && _turnsSinceLastSummary >= _config.SummaryEvery)
            return CacheAdvice.CompressAndSummarize;

        // Otherwise: shrink window
        return CacheAdvice.ShrinkWindow;
    }

    /// <summary>
    /// Apply budget constraints to segments. Drops low-priority segments
    /// until within budget, then trims context segments if still over.
    /// </summary>
    public List<PromptSegment> ApplyBudget(
        List<PromptSegment> segments,
        CacheProfile profile)
    {
        var budget = _config.TokenBudget;
        var total  = EstimateTokens(segments);

        if (total <= budget)
            return segments;

        // Drop context segments (lowest priority) first
        var filtered = segments
            .OrderBy(s => s.Priority)
            .ToList();

        var result = new List<PromptSegment>();
        var running = 0;

        foreach (var seg in filtered)
        {
            if (seg.Priority == SegmentPriority.Frozen || seg.Priority == SegmentPriority.Query)
            {
                // Never drop these
                result.Add(seg);
                running += seg.TokenEst;
                continue;
            }

            if (running + seg.TokenEst <= budget)
            {
                result.Add(seg);
                running += seg.TokenEst;
            }
            // else: dropped (context / summary)
        }

        return result;
    }

    /// <summary>
    /// Call after a summary is generated to reset the counter.
    /// </summary>
    public void OnSummaryGenerated() => _turnsSinceLastSummary = 0;

    /// <summary>Increment turn counter (call once per turn).</summary>
    public void OnTurn() => _turnsSinceLastSummary++;

    // ── Helpers ────────────────────────────────────────────────────

    private static int EstimateTokens(IReadOnlyList<AgentMessage> history) =>
        history.Sum(m => EstimateTokens(m.Content));

    private static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 3.5);

    private static int EstimateTokens(List<PromptSegment> segments) =>
        segments.Sum(s => s.TokenEst > 0 ? s.TokenEst : EstimateTokens(s.Content));
}
