---
title: F15 TS02 HotStore Contract Tests
category: entity
tags: [hotstore, contract-tests, inmemory, tests]
source_count: 1
created_at: 2026-05-14
updated_at: 2026-05-14
---

# F15 TS02 HotStore Contract Tests

## Summary
This test suite (10 tests) validates the contract of `InMemoryHotStore` — the in-memory implementation of the `IHotStore` interface. It covers basic set/get, missing key handling, key deletion, existence checks, increment operations, set data structure operations, and pub/sub publish-subscribe message delivery. The tests serve as a behavioral spec for any `IHotStore` implementation, ensuring correctness and TTL expiry via `Clear()`.

## Key Details
- All tests use `Fresh()` helper that creates a new `InMemoryHotStore` and calls `Clear()` to reset state.
- `Set_ThenGet_ReturnsSameValue` — verifies key-value round trip.
- `Get_MissingKey_ReturnsNull` — confirms missing keys yield `null`.
- `Delete_ExistingKey_RemovesIt` — ensures deletion makes key inaccessible.
- `Exists_BeforeAndAfterSet` — tests `ExistsAsync` returns `false` before set and `true` after.
- `Increment_StartsAtZero_ReturnsOne` — first increment on new key returns `1`.
- `Increment_TwiceLong_ReturnsTwo` — subsequent increment returns `2`.
- `SetAdd_ThenSetMembers_ContainsValue` — single element add and retrieval.
- `SetAdd_MultipleMembers_AllReturned` — three elements added and membership count verified.
- `SetMembers_EmptySet_ReturnsEmpty` — non-existent set returns empty list.
- `PubSub_PublishSubscribe_DeliversSingleMessage` — subscribes asynchronously, publishes, and asserts message delivery within timeout.

## Cross-References
- [[AGXIOME.Orchestration.Infrastructure.InMemoryHotStore]] — the tested implementation.
- [[IHotStore]] — the interface whose contract is validated (if documented).
- [[F15-TS01|F15 TS01 HotStore Interface]] — likely the interface definition test.

## Source Files
- `AGXIOME.Tests/F15/F15_TS02_HotStoreContractTests.cs` (ingested 2026-05-14)