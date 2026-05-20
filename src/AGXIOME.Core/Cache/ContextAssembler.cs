using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AGXIOME.Core.Session;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Budget-aware context assembler with fallback hierarchy.
///
/// Replaces PromptBuilder with a more efficient, metric-producing version.
///
/// Flow per turn:
///   1. Check session recovery → load checkpoint summary if resuming
///   2. Snapshot budget → decide memory level (L1/L2/L3)
///   3. Build segments by priority (frozen → episodic → semantic → procedural)
///   4. Apply budget → drop low-priority segments if over limit
///   5. Resolve pointers → skip already-sent content (LLM cache HIT)
///   6. Produce TokenReport with exact savings
///
/// Memory strategy:
///   - Pooled arrays for temp buffers
///   - No LINQ in hot paths
///   - Struct enumerators where possible
///   - Lazy segment building (don't allocate until needed)
/// </summary>
public sealed class ContextAssembler
{
    private readonly ContentPointerCache   _ptrs;
    private readonly TokenBudgetTracker    _budget;
    private readonly ITokenizer            _tokenizer;
    private readonly ILogger<ContextAssembler> _log;

    private static readonly char[] NewlineSep = ['\n'];

    public ContextAssembler(
        ContentPointerCache       ptrs,
        TokenBudgetTracker        budget,
        ITokenizer                tokenizer,
        ILogger<ContextAssembler> log)
    {
        _ptrs      = ptrs;
        _budget    = budget;
        _tokenizer = tokenizer;
        _log       = log;
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>
    /// Build an optimised prompt with full metrics.
    /// Thread-safe. Call once per turn.
    /// </summary>
    public async Task<AssembledContext> AssembleAsync(
        string                          sessionId,
        string                          systemPrompt,
        string                          agentDefinition,
        IReadOnlyList<AgentMessage>     recentHistory,
        string                          userMessage,
        CacheProfile                    profile,
        string?                         recoverySummary = null,
        CancellationToken               ct = default)
    {
        var sw       = Stopwatch.StartNew();
        _budget.OnTurn();

        // 1. Budget snapshot → decide level
        var rawTokens = EstimatePromptRaw(systemPrompt, agentDefinition, recentHistory, userMessage);
        var snap     = new BudgetSnapshot(profile.MaxPrefixTokens + profile.MaxTailTokens, rawTokens);
        var level    = snap.SuggestedLevel;

        // 2. Build segments
        var segments = new List<PromptSegment>(8); // pre-size to avoid growth

        // Frozen: system (always included)
        var sysPtr = await _ptrs.RegisterAsync(systemPrompt);
        segments.Add(new PromptSegment
        {
            Ptr = sysPtr, Content = systemPrompt,
            Priority = SegmentPriority.Frozen,
            TokenEst = _tokenizer.Estimate(systemPrompt),
        });

        // Frozen: agent definition
        var agentPtr = await _ptrs.RegisterAsync(agentDefinition);
        segments.Add(new PromptSegment
        {
            Ptr = agentPtr, Content = agentDefinition,
            Priority = SegmentPriority.Frozen,
            TokenEst = _tokenizer.Estimate(agentDefinition),
        });

        // Recovery summary (if resuming)
        if (recoverySummary != null)
        {
            segments.Add(new PromptSegment
            {
                Ptr = "", Content = recoverySummary,
                Priority = SegmentPriority.Summary,
                TokenEst = _tokenizer.Estimate(recoverySummary),
            });
        }

        // Recent messages (limited by level + config)
        var maxRecent = level switch
        {
            3 => 1,        // L3: only last message
            2 => 2,        // L2: last 2
            _ => profile.MaxRecentMessages,  // L1: configured
        };

        var take = Math.Min(maxRecent, recentHistory.Count);
        if (take > 0)
        {
            var skip = recentHistory.Count - take;
            foreach (var msg in recentHistory.Skip(skip))
            {
                var formatted = FormatMessage(msg);
                var ptr = await _ptrs.RegisterAsync(formatted);
                segments.Add(new PromptSegment
                {
                    Ptr = ptr, Content = formatted,
                    Priority = SegmentPriority.Context,
                    TokenEst = _tokenizer.Estimate(msg.Content),
                });
            }
        }

        // User query (always)
        segments.Add(new PromptSegment
        {
            Ptr = "", Content = userMessage,
            Priority = SegmentPriority.Query,
            TokenEst = _tokenizer.Estimate(userMessage),
        });

        // 3. Apply budget
        var beforeCount = segments.Count;
        segments = _budget.ApplyBudget(segments, profile);
        var dropped = beforeCount - segments.Count;

        // 4. Resolve pointers
        var result = _ptrs.BuildPrompt(segments);

        sw.Stop();

        // 5. Build report
        var report = new TokenReport(
            sessionId:      sessionId,
            turn:           recentHistory.Count,
            strategy:       level == 1 ? "full" : level == 2 ? "compressed" : "summary",
            raw:            rawTokens,
            sent:           _tokenizer.Estimate(result.Prompt),
            saved:          result.SavedTokens,
            cacheHit:       result.PtrsSkipped.Count,
            cacheMiss:      result.PtrsSent.Count,
            dropped:        dropped,
            summaryGen:     level > 1,
            micros:         sw.ElapsedTicks / (Stopwatch.Frequency / 1_000_000),
            level:          level
        );

        _log.LogInformation(
            "[Assembler] {Sid} turn {T}: L{Level} {Strategy} — sent {Sent} tok of {Raw} raw ({Pct:P1}), saved {Saved} via ptr",
            sessionId, recentHistory.Count, level,
            report.Strategy, report.SentTokens, report.RawTokens,
            report.CacheRatio, report.SavedTokens);

        return new AssembledContext
        {
            Prompt      = result.Prompt,
            PtrsSent    = result.PtrsSent,
            PtrsSkipped = result.PtrsSkipped,
            Report      = report,
            MemoryLevel = level,
        };
    }

    // ── Helpers ────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatMessage(AgentMessage msg) =>
        string.Concat(msg.Role, ": ", msg.Content);

    private int EstimatePromptRaw(
        string sysPrompt, string agentDef,
        IReadOnlyList<AgentMessage> history, string userMsg)
    {
        var total = _tokenizer.Estimate(sysPrompt)
                  + _tokenizer.Estimate(agentDef)
                  + _tokenizer.Estimate(userMsg);
        foreach (var msg in history)
            total += _tokenizer.Estimate(msg.Content);
        return total;
    }
}

// ── Output record ─────────────────────────────────────────────────────────

/// <summary>Output of ContextAssembler. Lightweight.</summary>
public sealed record AssembledContext
{
    /// <summary>Final prompt text ready for LLM.</summary>
    public string Prompt { get; init; } = "";

    /// <summary>Pointers sent this turn (new).</summary>
    public IReadOnlyList<string> PtrsSent { get; init; } = [];

    /// <summary>Pointers skipped (cache HIT).</summary>
    public IReadOnlyList<string> PtrsSkipped { get; init; } = [];

    /// <summary>Metrics report.</summary>
    public TokenReport Report { get; init; }

    /// <summary>Memory level used.</summary>
    public byte MemoryLevel { get; init; }
}
