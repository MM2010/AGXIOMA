# P11 — Tools Infrastructure

> **Priority**: 🟡 P1 | **Dipende da**: P09 (Core) | **Usato da**: P02 (Toolkits)
> **Target**: Tool caching, call limits, factory, include/exclude

---

## Task

| # | Task | Descrizione | Effort |
|---|------|-------------|--------|
| TI-01 | **Tool Result Cache** | Cache risultati tool per ridurre chiamate (TTL + hash chiave) | 1gg |
| TI-02 | **Tool Call Limit** | Max tool call per run + max errori consecutivi | 1gg |
| TI-03 | **Stop After Tool** | Config per fermare run dopo tool call specifica | 0.5gg |
| TI-04 | **Include/Exclude Tools** | Filtri su quali tool usare per sessione | 0.5gg |
| TI-05 | **Callable Factory** | Genera funzioni callable da tool per LLM | 1gg |
| TI-06 | **Tool Metrics** | Track usage count, latency, error rate per tool | 1gg |

### Pattern: Tool Result Cache

```csharp
public sealed class ToolResultCache
{
    private readonly ConcurrentDictionary<string, CachedResult> _cache;
    private readonly TimeSpan _defaultTtl;
    
    public string BuildCacheKey(string toolName, string arguments)
    {
        // Hash(arguments) = cache key deterministico
        return $"{toolName}:{HashArguments(arguments)}";
    }
    
    public bool TryGet(string key, out object? result) { ... }
    public void Set(string key, object result, TimeSpan? ttl) { ... }
    public void Invalidate(string toolName) { ... }
}
```

### Pattern: Tool Call Limit

```csharp
public sealed class ToolCallTracker
{
    private int _callCount;
    private int _errorCount;
    private const int MaxCalls = 50;
    private const int MaxErrors = 5;
    
    public bool CanContinue() => _callCount < MaxCalls && _errorCount < MaxErrors;
    public void RecordCall() => Interlocked.Increment(ref _callCount);
    public void RecordError() => Interlocked.Increment(ref _errorCount);
}
```

**Totale task: 6 | File: ~8 | Test: ~40**
