using System.Text.Json.Serialization;

namespace AGXIOME.Shared.Models;

// ── Compressed storage records (1-char property keys, no pretty print) ─────

/// <summary>Ultra-compact turn record for Redis/DB storage.</summary>
public sealed record CompressedTurn
{
    [JsonPropertyName("r")] public string  Role    { get; init; } = "";  // u|a|s|t
    [JsonPropertyName("c")] public string  Content { get; init; } = "";
    [JsonPropertyName("t")] public string  Ts      { get; init; } = "";  // ISO short
    [JsonPropertyName("k")] public int?    Tokens  { get; init; }
}

/// <summary>Compressed session for Redis/DB. GZip after serialization.</summary>
public sealed record CompressedSession
{
    [JsonPropertyName("i")] public string                 Id       { get; init; } = "";
    [JsonPropertyName("v")] public int                    V        { get; init; } = 2;
    [JsonPropertyName("m")] public List<CompressedTurn>   Msgs     { get; init; } = [];
    [JsonPropertyName("s")] public string?                Summary  { get; init; }
    [JsonPropertyName("h")] public string?                HashChain { get; init; }
}

// ── Pointer cache records ───────────────────────────────────────────────────

/// <summary>A content-addressed pointer segment.</summary>
public sealed record PromptSegment
{
    public string          Ptr      { get; init; } = "";
    public string          Content  { get; init; } = "";
    public SegmentPriority Priority { get; init; }
    public int             TokenEst { get; init; }
}

public enum SegmentPriority
{
    Frozen  = 0,  // Never dropped, never changes
    Summary = 1,  // Droppable under budget pressure
    Context = 2,  // Droppable first
    Query   = 0,  // Never dropped (user's question)
}

/// <summary>Result from the prompt builder.</summary>
public sealed record BuiltPrompt
{
    public string                Prompt      { get; init; } = "";
    public IReadOnlyList<string> PtrsSent    { get; init; } = [];
    public IReadOnlyList<string> PtrsSkipped { get; init; } = [];
    public int                   TotalTokens { get; init; }
    public int                   SavedTokens { get; init; }  // tokens not re-sent
    public double                CacheRatio  { get; init; }  // 0-1
}

// ── Pointer Cache Configuration ────────────────────────────────────────────

/// <summary>Pointer cache settings. Drop into appsettings or DI.</summary>
public sealed record PointerCacheConfig
{
    /// <summary>Enable content-addressed pointer cache. Default: true.</summary>
    public bool   Enabled        { get; init; } = true;

    /// <summary>TTL for cached pointer contents in hours. Default: 24.</summary>
    public int    TtlHours       { get; init; } = 24;

    /// <summary>Max chars per pointer content before truncation. Default: 4096.</summary>
    public int    MaxPtrChars    { get; init; } = 4096;

    /// <summary>Redis key prefix. Default: "ptr".</summary>
    public string KeyPrefix      { get; init; } = "ptr";

    /// <summary>Enable local in-memory L1 cache (faster, less Redis calls).</summary>
    public bool   EnableLocalL1  { get; init; } = true;

    /// <summary>Local L1 max entries. Default: 500.</summary>
    public int    LocalL1Max     { get; init; } = 500;
}

// ── Memory / Budget Configuration ──────────────────────────────────────────

/// <summary>Agent memory strategy. Drop into appsettings or DI.</summary>
public sealed record AgentMemoryConfig
{
    /// <summary>Memory strategy: full|pointer|summary.</summary>
    public string Strategy { get; init; } = "pointer";

    /// <summary>Token budget per prompt. Default: 4000.</summary>
    public int    TokenBudget  { get; init; } = 4000;

    /// <summary>Max recent messages to always include. Default: 3.</summary>
    public int    MaxRecentMsg { get; init; } = 3;

    /// <summary>Generate LLM summary every N turns. 0 = disabled. Default: 5.</summary>
    public int    SummaryEvery { get; init; } = 5;

    /// <summary>Pointer cache sub-config.</summary>
    public PointerCacheConfig Pointers { get; init; } = new();
}

// ── Workflow Checkpoint ────────────────────────────────────────────────────

/// <summary>Snapshot of a workflow at a point in time for recovery.</summary>
public sealed record WorkflowCheckpoint
{
    [JsonPropertyName("i")] public string   Id           { get; init; } = "";
    [JsonPropertyName("s")] public string   SessionId    { get; init; } = "";
    [JsonPropertyName("u")] public string   UserId       { get; init; } = "";
    [JsonPropertyName("a")] public string   AgentId      { get; init; } = "";
    [JsonPropertyName("p")] public string   Phase        { get; init; } = "";  // workflow phase
    [JsonPropertyName("w")] public string?  WorkflowType { get; init; }       // workflow class
    [JsonPropertyName("t")] public int      Turn         { get; init; }
    [JsonPropertyName("c")] public string?  ContextHash  { get; init; }       // hash del contesto
    [JsonPropertyName("d")] public string?  DataJson     { get; init; }       // stato compresso
    [JsonPropertyName("h")] public string?  HashChain    { get; init; }       // per replay
    [JsonPropertyName("n")] public DateTime CreatedAt    { get; init; }
}

/// <summary>Recovery info loaded when resuming an interrupted session.</summary>
public sealed record SessionRecoveryInfo
{
    public string                  SessionId     { get; init; } = "";
    public string                  UserId        { get; init; } = "";
    public string                  AgentId       { get; init; } = "";
    public string?                 LastPhase     { get; init; }
    public int                     LastTurn      { get; init; }
    public string?                 Summary       { get; init; }
    public string?                 HashChain     { get; init; }
    public IReadOnlyList<string>   PendingPtrs   { get; init; } = [];
    public DateTime                LastActive    { get; init; }
}

// ── Cache advice ───────────────────────────────────────────────────────────

public enum CacheAdvice
{
    Ok,                     // within budget, send full
    ShrinkWindow,           // reduce recent messages
    CompressAndSummarize,   // force summary generation
    Checkpoint,             // save checkpoint before continuing
}
