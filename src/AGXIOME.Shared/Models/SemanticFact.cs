using System.Text.Json.Serialization;

namespace AGXIOME.Shared.Models;

// ── Memory type separation ────────────────────────────────────────────────

/// <summary>
/// Memory classification for Episodic/Semantic/Procedural split.
/// Each type has different storage, retrieval, and TTL policies.
/// </summary>
public enum MemoryType : byte
{
    /// <summary>Raw conversation turns. For audit, replay, traceability.</summary>
    Episodic   = 0,

    /// <summary>Extracted facts, entities, relations. Cross-session retrieval.</summary>
    Semantic   = 1,

    /// <summary>Versioned system prompt, tool config, workflow instructions.</summary>
    Procedural = 2,
}

// ── Semantic Fact (cross-session) ─────────────────────────────────────────

/// <summary>
/// Ultra-compact semantic fact for cross-session agent memory.
/// Properties use 1-char JSON keys. No pretty print.
///
/// Storage: pgvector (embedding) + Redis (hot cache).
/// Embedding dimension: 384 (mxbai-embed-large).
/// </summary>
public sealed record SemanticFact
{
    [JsonPropertyName("i")] public string   Id         { get; init; } = "";
    [JsonPropertyName("a")] public string   AgentId    { get; init; } = "";
    [JsonPropertyName("e")] public string   Entity     { get; init; } = "";
    [JsonPropertyName("p")] public string   Predicate  { get; init; } = "";
    [JsonPropertyName("o")] public string   Object     { get; init; } = "";
    [JsonPropertyName("c")] public string   Content    { get; init; } = "";
    [JsonPropertyName("v")] public float[]? Embedding  { get; init; }
    [JsonPropertyName("s")] public string   SessionId  { get; init; } = "";
    [JsonPropertyName("t")] public int      Turn       { get; init; }
    [JsonPropertyName("n")] public DateTime CreatedAt  { get; init; }
    [JsonPropertyName("k")] public float    Score      { get; init; }  // relevance score 0-1
}

// ── Token Report (per-turn metrics) ───────────────────────────────────────

/// <summary>
/// Per-turn metrics report. Struct to minimize GC pressure.
/// Produced by ContextAssembler after each prompt build.
/// </summary>
public readonly struct TokenReport
{
    /// <summary>Session identifier.</summary>
    public readonly string SessionId;

    /// <summary>Turn number.</summary>
    public readonly int Turn;

    /// <summary>Memory strategy used.</summary>
    public readonly string Strategy;

    /// <summary>Raw tokens if no optimization applied.</summary>
    public readonly int RawTokens;

    /// <summary>Tokens actually sent after budget + pointer.</summary>
    public readonly int SentTokens;

    /// <summary>Tokens saved via pointer cache.</summary>
    public readonly int SavedTokens;

    /// <summary>Cache hit ratio (0-1).</summary>
    public readonly double CacheRatio;

    /// <summary>Budget usage fraction (0-1).</summary>
    public readonly double BudgetUsage;

    /// <summary>Segments dropped due to budget.</summary>
    public readonly int DroppedSegments;

    /// <summary>Pointer cache hits.</summary>
    public readonly int CacheHitPtrs;

    /// <summary>Pointer cache misses (new registrations).</summary>
    public readonly int CacheMissPtrs;

    /// <summary>Whether summary was generated this turn.</summary>
    public readonly bool SummaryGenerated;

    /// <summary>Assembly time in microseconds.</summary>
    public readonly long AssemblyMicros;

    /// <summary>Memory level used: 1=full, 2=compressed, 3=summary-only.</summary>
    public readonly byte MemoryLevel;

    public TokenReport(
        string sessionId, int turn, string strategy,
        int raw, int sent, int saved,
        int cacheHit, int cacheMiss,
        int dropped, bool summaryGen,
        long micros, byte level)
    {
        SessionId        = sessionId;
        Turn             = turn;
        Strategy         = strategy;
        RawTokens        = raw;
        SentTokens       = sent;
        SavedTokens      = saved;
        CacheRatio       = raw > 0 ? (double)saved / raw : 0;
        BudgetUsage      = sent > 0 ? (double)saved / sent : 0;
        DroppedSegments  = dropped;
        CacheHitPtrs     = cacheHit;
        CacheMissPtrs    = cacheMiss;
        SummaryGenerated = summaryGen;
        AssemblyMicros   = micros;
        MemoryLevel      = level;
    }
}

// ── Budget snapshot ───────────────────────────────────────────────────────

/// <summary>
/// Snapshot of budget state at a point in time.
/// Used by ContextAssembler for fallback decisions.
/// </summary>
public readonly struct BudgetSnapshot
{
    /// <summary>Total budget.</summary>
    public readonly int Total;

    /// <summary>Tokens consumed so far.</summary>
    public readonly int Used;

    /// <summary>Tokens remaining.</summary>
    public readonly int Remaining;

    /// <summary>Usage fraction (0-1).</summary>
    public readonly double Usage => Total > 0 ? (double)Used / Total : 0;

    /// <summary>Memory level to use based on usage.</summary>
    public readonly byte SuggestedLevel;

    public BudgetSnapshot(int total, int used)
    {
        Total     = total;
        Used      = used;
        Remaining = total - used;

        SuggestedLevel = Usage switch
        {
            > 0.90 => 3,  // L3: summary-only fallback
            > 0.70 => 2,  // L2: compressed + summary
            _      => 1,  // L1: full context
        };
    }
}

// ── Generation params (for deterministic replay) ──────────────────────────

/// <summary>
/// Parameters used for LLM generation at a given turn.
/// Stored with checkpoint for exact replay debugging.
/// </summary>
public sealed record GenerationParams
{
    [JsonPropertyName("m")] public string  Model      { get; init; } = "";
    [JsonPropertyName("t")] public double  Temperature { get; init; }
    [JsonPropertyName("s")] public int?    Seed       { get; init; }
    [JsonPropertyName("p")] public double? TopP       { get; init; }
    [JsonPropertyName("k")] public int?    MaxTokens  { get; init; }
}
