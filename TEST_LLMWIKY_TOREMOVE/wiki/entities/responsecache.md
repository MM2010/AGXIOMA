---  
title: ResponseCache  
category: entity  
tags: [cache, llm, response, ttl, distributed]  
source_count: 1  
created_at: 2026-05-14  
updated_at: 2026-05-14  
---  

# ResponseCache  

## Summary  
The `ResponseCache` is a thread-safe, TTL-based caching component for LLM responses within the AGXIOME framework. It supports configurable cache strategies (`None`, `InMemory`, `Redis`, `Hybrid`) via the `CacheStrategy` enum, and uses a deterministic SHA-256 hash of provider, model, and prompt as the cache key. In-memory caching relies on a `ConcurrentDictionary` with periodic expiry sweep and LRU eviction when the maximum entry count is exceeded. Distributed caching (e.g., Redis) is pluggable through the `ICacheBackend` interface.  

## Key Details  
- **Cache key**: `SHA-256($"{providerInt}|{model.ToLower()}|{prompt}")` – deterministic and ensures no collisions across providers/models.  
- **Strategies**: `None` (disabled), `InMemory` (local `ConcurrentDictionary`), `Redis` (remote backend only), `Hybrid` (both L1 in-memory and L2 backend).  
- **TTL**: Default 15 minutes, configurable via `ResponseCacheOptions.DefaultTtl` or per-call via `SetAsync` parameter.  
- **Max entries**: Default 1000 (`ResponseCacheOptions.MaxEntries`). When exceeded, the oldest entry (by expiration time) is evicted.  
- **Expiry purge**: `PurgeExpired()` method removes expired entries from in-memory dictionary – intended to be called from a background timer.  
- **Backend interface**: `ICacheBackend` provides `GetAsync`, `SetAsync`, `RemoveAsync` for distributed caching implementations.  
- **Thread safety**: Uses `ConcurrentDictionary` for in-memory operations; methods are async-aware and accept `CancellationToken`.  
- **Invalidation**: `InvalidateAsync` removes entries from both memory and backend.  
- **Usage**: `LLMProviderType` is an enum (no magic strings), ensuring type-safe cache key computation.  

## Cross-References  
- [[CacheStrategy]] — Enum that defines the caching behavior (None, InMemory, Redis, Hybrid)  
- [[ICacheBackend]] — Interface for pluggable distributed cache backends (e.g., Redis)  
- [[LLMProviderType]] — Enum used in cache key computation to differentiate providers  
- [[ResponseCacheOptions]] — Configuration record for TTL, strategy, and max entries  
- [[entities/llm]] — The LLM module that utilizes caching  

## Source Files  
- `AGXIOME.Core/LLM/ResponseCache.cs` (ingested 2026-05-14)