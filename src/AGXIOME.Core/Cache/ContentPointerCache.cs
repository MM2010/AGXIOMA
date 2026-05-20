using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using Microsoft.Extensions.Logging;

namespace AGXIOME.Core.Cache;

/// <summary>
/// Content-addressable pointer cache.
///
/// Registers content by SHA-256 hash and returns a short pointer key.
/// On prompt build, pointers already sent in this session are SKIPPED,
/// so only new/changed content is transmitted = maximum LLM cache hit.
///
/// Architecture:
///   L1: ConcurrentDictionary in-process (μs latency, bounded)
///   L2: Redis (optional, ~2ms, shared across instances)
///   L3: None (no fallback — if not cached, send full content)
///
/// Thread-safe. All public methods are async.
///
/// Usage:
///   var ptr = await cache.RegisterAsync(systemPrompt);
///   // ptr = "p:a1b2c3d4e5f6"
///   var prompt = cache.BuildPrompt([segment1, segment2, ...]);
///   // skips ptrs already sent
/// </summary>
public sealed class ContentPointerCache
{
    private readonly PointerCacheConfig _config;
    private readonly ILogger<ContentPointerCache> _log;

    // L1 in-memory
    private readonly ConcurrentDictionary<string, string> _local = new();
    private readonly ConcurrentDictionary<string, byte>   _sent  = new();

    // L2 store (optional — Redis etc.)
    private readonly ICacheStore? _store;

    private const string PtrPrefix = "p:";

    public ContentPointerCache(
        PointerCacheConfig config,
        ILogger<ContentPointerCache> log,
        ICacheStore? store = null)
    {
        _config = config;
        _log    = log;
        _store  = store;
    }

    // ── Public API ─────────────────────────────────────────────────

    /// <summary>
    /// Register content and get a pointer key.
    /// Dedup: same content → same pointer (content is stored only once).
    /// </summary>
    public async Task<string> RegisterAsync(string content, TimeSpan? ttl = null)
    {
        if (string.IsNullOrEmpty(content))
            return "";

        var ptr = HashToPtr(content);

        // Already in L1? Skip storage.
        if (_local.ContainsKey(ptr))
            return ptr;

        // L2 store (dedup across instances)
        if (_store != null)
        {
            var stored = await _store.SetAsync(
                Key(ptr), content, ttl ?? TimeSpan.FromHours(_config.TtlHours));
        }

        // L1 local
        if (_config.EnableLocalL1 && _local.Count < _config.LocalL1Max)
            _local[ptr] = content;

        return ptr;
    }

    /// <summary>Resolve a pointer back to its content.</summary>
    public async Task<string?> ResolveAsync(string ptr)
    {
        if (string.IsNullOrEmpty(ptr))
            return null;

        // L1
        if (_local.TryGetValue(ptr, out var cached))
            return cached;

        // L2 store
        if (_store != null)
        {
            var content = await _store.GetAsync(Key(ptr));
            if (content != null)
            {
                if (_config.EnableLocalL1 && _local.Count < _config.LocalL1Max)
                    _local[ptr] = content;
                return content;
            }
        }

        return null;
    }

    /// <summary>
    /// Mark a pointer as "already sent" in this session.
    /// BuildPrompt checks this to skip already-sent content.
    /// </summary>
    public void MarkSent(string ptr)
    {
        if (!string.IsNullOrEmpty(ptr))
            _sent[ptr] = 0;
    }

    /// <summary>Check if a pointer was already sent in this session.</summary>
    public bool WasSent(string ptr) => _sent.ContainsKey(ptr);

    /// <summary>
    /// Build an optimised prompt from segments.
    /// Segments whose pointer was already sent are SKIPPED (cache hit on LLM side).
    /// </summary>
    public BuiltPrompt BuildPrompt(IReadOnlyList<PromptSegment> segments)
    {
        var sb     = new StringBuilder();
        var sent   = new List<string>();
        var missed = new List<string>();
        var saved  = 0;

        foreach (var seg in segments)
        {
            if (!string.IsNullOrEmpty(seg.Ptr) && _sent.ContainsKey(seg.Ptr))
            {
                // Already sent — LLM still has it cached. Skip!
                missed.Add(seg.Ptr);
                saved += seg.TokenEst;
                continue;
            }

            sb.AppendLine(seg.Content);

            if (!string.IsNullOrEmpty(seg.Ptr))
            {
                _sent[seg.Ptr] = 0;
                sent.Add(seg.Ptr);
            }
        }

        var prompt = sb.ToString();
        var total  = EstimateTokens(prompt) + saved;

        return new BuiltPrompt
        {
            Prompt      = prompt,
            PtrsSent    = sent.AsReadOnly(),
            PtrsSkipped = missed.AsReadOnly(),
            TotalTokens = total,
            SavedTokens = saved,
            CacheRatio  = total > 0 ? (double)saved / total : 0,
        };
    }

    /// <summary>Clear session-local sent tracking.</summary>
    public void ResetSession() => _sent.Clear();

    // ── Helpers ────────────────────────────────────────────────────

    private static string HashToPtr(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return PtrPrefix + Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }

    private string Key(string ptr) => $"{_config.KeyPrefix}:{ptr}";

    /// <summary>Rough token estimate (chars / 3.5).</summary>
    private static int EstimateTokens(string text) =>
        string.IsNullOrEmpty(text) ? 0 : (int)Math.Ceiling(text.Length / 3.5);
}
