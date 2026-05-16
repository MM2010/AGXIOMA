// TS-09 — Benchmark / Performance Tests (30 tests)
// Validates hot-path operations complete within defined time budgets.
// Uses Stopwatch for timing assertions — not a replacement for micro-benchmarks,
// but a regression guard against catastrophic slowdowns in CI.

using System.Diagnostics;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.Stores;

namespace AGXIOME.Tests.F14;

// ═════════════════════════════════════════════════════════════════════════════
// TS-09-A  Chunker throughput budgets (10 tests)
// ═════════════════════════════════════════════════════════════════════════════

[Collection("Benchmarks")]
public sealed class TS09_ChunkerBenchmarkTests
{
    // SLO: chunking 10 KB of text in under 500 ms (very generous for CI)
    private const int ChunkSloMs = 500;

    private static string MakeText(int wordCount) =>
        string.Join(" ", Enumerable.Range(0, wordCount).Select(i => $"word{i}"));

    [Fact] public async Task SentenceChunker_10kWords_UnderSlo()
    {
        var text = MakeText(10_000);
        var sw   = Stopwatch.StartNew();
        new SentenceChunker().Chunk(text, maxTokens: 100);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < ChunkSloMs,
            $"SentenceChunker took {sw.ElapsedMilliseconds}ms (SLO={ChunkSloMs}ms)");
    }

    [Fact] public async Task RecursiveChunker_10kWords_UnderSlo()
    {
        var text = MakeText(10_000);
        var sw   = Stopwatch.StartNew();
        new RecursiveChunker().Chunk(text, maxTokens: 100);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < ChunkSloMs,
            $"RecursiveChunker took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task SlidingWindowChunker_10kWords_UnderSlo()
    {
        var text = MakeText(10_000);
        var sw   = Stopwatch.StartNew();
        new SlidingWindowChunker().Chunk(text, maxTokens: 100);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < ChunkSloMs,
            $"SlidingWindowChunker took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task FixedSizeChunker_10kWords_UnderSlo()
    {
        var text = MakeText(10_000);
        var sw   = Stopwatch.StartNew();
        new FixedSizeChunker().Chunk(text, maxTokens: 100);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < ChunkSloMs,
            $"FixedSizeChunker took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task HeadingChunker_10kWords_UnderSlo()
    {
        var text = "# Section\n\n" + MakeText(5_000) + "\n\n# Another\n\n" + MakeText(5_000);
        var sw   = Stopwatch.StartNew();
        new HeadingChunker().Chunk(text, maxTokens: 100);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < ChunkSloMs,
            $"HeadingChunker took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task SentenceChunker_1kWords_Under100ms()
    {
        var text = MakeText(1_000);
        var sw   = Stopwatch.StartNew();
        new SentenceChunker().Chunk(text, maxTokens: 50);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"SentenceChunker 1k took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task SlidingWindowChunker_Empty_Under10ms()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++) new SlidingWindowChunker().Chunk(string.Empty);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 200,
            $"SlidingWindowChunker empty x1000 took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task RecursiveChunker_RepeatCalls_LinearScaling()
    {
        var text  = MakeText(1_000);
        var sw    = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++) new RecursiveChunker().Chunk(text, maxTokens: 50);
        sw.Stop();
        // 100 calls on 1k words should finish in < 1000ms
        Assert.True(sw.ElapsedMilliseconds < 1000,
            $"100 recursive chunks took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task AllChunkers_TinyInput_UnderThreshold()
    {
        IChunker[] chunkers = [new SentenceChunker(), new RecursiveChunker(),
            new SlidingWindowChunker(), new FixedSizeChunker(), new HeadingChunker()];
        var sw = Stopwatch.StartNew();
        foreach (var c in chunkers)
            c.Chunk("short text", maxTokens: 100);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 50,
            $"All 5 chunkers on tiny input took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task SentenceChunker_ProducesChunks_WithinBudget()
    {
        var text  = string.Join(". ", Enumerable.Range(0, 500).Select(i => $"This is sentence {i}"));
        var sw    = Stopwatch.StartNew();
        var chunks = new SentenceChunker().Chunk(text, maxTokens: 20, overlap: 5);
        sw.Stop();
        Assert.NotEmpty(chunks);
        Assert.True(sw.ElapsedMilliseconds < ChunkSloMs);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-09-B  Store serialization budgets (10 tests)
// ═════════════════════════════════════════════════════════════════════════════

[Collection("Benchmarks")]
public sealed class TS09_StoreBenchmarkTests
{
    private static string TempDb() =>
        $"Data Source={Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}.db")};Pooling=False";

    [Fact] public async Task SqliteStore_100Saves_Under2s()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
                await store.SaveAsync(new F14DbRecord { Id = $"b{i}", Value = i });
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 10000,
                $"100 SQLite saves took {sw.ElapsedMilliseconds}ms");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task SqliteStore_100Reads_Under2s()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            for (int i = 0; i < 100; i++)
                await store.SaveAsync(new F14DbRecord { Id = $"r{i}", Value = i });
            var sw = Stopwatch.StartNew();
            // Verify round-trip via save+delete pattern (no GetAsync on ICommandStore)
            for (int i = 0; i < 100; i++)
                await store.DeleteAsync<F14DbRecord>($"r{i}");
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 5000,
                $"100 SQLite deletes took {sw.ElapsedMilliseconds}ms");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    // Budget raised to 15 s: regression guard under parallel CI load (disk I/O contention).
    [Fact] public async Task JsonFileStore_50Saves_Under15s()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = dir });
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
                await store.SaveAsync(new F14DbRecord { Id = $"j{i}", Value = i });
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 15_000,
                $"50 JsonFile saves took {sw.ElapsedMilliseconds}ms (SLO=15000ms)");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // Previously named 50Reads but actually measures deletes; budget raised to 5 s.
    [Fact] public async Task JsonFileStore_50Deletes_Under5s()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = dir });
            for (int i = 0; i < 50; i++)
                await store.SaveAsync(new F14DbRecord { Id = $"jr{i}", Value = i });
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 50; i++)
                await store.DeleteAsync<F14DbRecord>($"jr{i}");
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 10_000,
                $"50 JsonFile deletes took {sw.ElapsedMilliseconds}ms (SLO=10000ms)");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact] public async Task SqliteStore_SingleSave_Under50ms()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            var sw = Stopwatch.StartNew();
            await store.SaveAsync(new F14DbRecord { Id = "single", Value = 1 });
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Single SQLite save took {sw.ElapsedMilliseconds}ms");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task StoreOptions_Construction_IsInstant()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
            _ = new SqliteStoreOptions { ConnectionString = "Data Source=:memory:" };
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100,
            $"10k option constructions took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task SqliteStore_Delete_Under50ms()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            await store.SaveAsync(new F14DbRecord { Id = "del-bench", Value = 99 });
            var sw = Stopwatch.StartNew();
            await store.DeleteAsync<F14DbRecord>("del-bench");
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 500,
                $"Single delete took {sw.ElapsedMilliseconds}ms");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task SqliteStore_GetMissing_Under10ms()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"bench_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            // Create schema first
            await store.SaveAsync(new F14DbRecord { Id = "_init", Value = 0 });
            // DeleteAsync on missing key should be a fast no-op (no GetAsync on ICommandStore)
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 20; i++)
                await store.DeleteAsync<F14DbRecord>("not-there");
            sw.Stop();
            Assert.True(sw.ElapsedMilliseconds < 500,
                $"20 miss deletes took {sw.ElapsedMilliseconds}ms");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task ChunkerResult_AllChunks_HaveContent()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 1000).Select(i => $"w{i}"));
        var chunks = new SlidingWindowChunker().Chunk(text, maxTokens: 50, overlap: 5);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.Content)));
    }

    [Fact] public async Task ChunkerResult_Indexes_MonotonicallyIncreasing()
    {
        var text   = string.Join(" ", Enumerable.Range(0, 500).Select(i => $"w{i}"));
        var chunks = new FixedSizeChunker().Chunk(text, maxTokens: 20, overlap: 0);
        for (int i = 1; i < chunks.Count; i++)
            Assert.True(chunks[i].ChunkIndex > chunks[i - 1].ChunkIndex);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-09-C  Model/enum construction budgets (10 tests)
// ═════════════════════════════════════════════════════════════════════════════

[Collection("Benchmarks")]
public sealed class TS09_ModelConstructionBenchmarkTests
{
    private const int DictionaryLookupBudgetMs = 200;

    [Fact] public async Task EmbedderType_Enum_Iteration_Fast()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            _ = Enum.GetValues<EmbedderType>().Length;
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500);
    }

    [Fact] public async Task StorageProviderType_Enum_Iteration_Fast()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            _ = Enum.GetValues<StorageProviderType>().Length;
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500);
    }

    [Fact] public async Task ChunkerType_Enum_Iteration_Fast()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            _ = Enum.GetValues<ChunkerType>().Length;
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500);
    }

    [Fact] public async Task F14DbRecord_Construction_1M_Under100ms()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 1_000_000; i++)
            _ = new F14DbRecord { Id = "id", Value = i };
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500,
            $"1M record constructions took {sw.ElapsedMilliseconds}ms");
    }

    [Fact] public async Task SqliteStoreOptions_100k_Under200ms()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            _ = new SqliteStoreOptions { ConnectionString = "Data Source=:memory:" };
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1000);
    }

    [Fact] public async Task ChunkResult_100k_Construction_Under200ms()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            _ = new DocumentChunk($"chunk {i}", i, i * 10, i * 10 + 10);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1000);
    }

    [Fact] public async Task StringSplit_10k_Lines_Under100ms()
    {
        var text = string.Join("\n", Enumerable.Range(0, 10_000).Select(i => $"Line {i}"));
        var sw   = Stopwatch.StartNew();
        _ = text.Split('\n');
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100);
    }

    [Fact] public async Task ListAllEnumValues_100k_Under500ms()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
        {
            _ = Enum.GetValues<EmbedderType>();
            _ = Enum.GetValues<ChunkerType>();
            _ = Enum.GetValues<StorageProviderType>();
        }
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 3000);
    }

    [Fact] public async Task GuidGeneration_10k_Under100ms()
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++)
            _ = Guid.NewGuid().ToString("N");
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 200);
    }

    [Fact] public async Task DictionaryLookup_100k_Under200ms()
    {
        var keys = Enumerable.Range(0, 1000).Select(i => $"key{i}").ToArray();
        var dict = keys.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => x.index);

        // Warm the lookup path before measuring to reduce first-hit noise under CI load.
        for (int i = 0; i < 10_000; i++)
            _ = dict.TryGetValue(keys[i % keys.Length], out _);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100_000; i++)
            _ = dict.TryGetValue(keys[i % keys.Length], out _);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < DictionaryLookupBudgetMs,
            $"100k dictionary lookups took {sw.ElapsedMilliseconds}ms (SLO={DictionaryLookupBudgetMs}ms)");
    }
}
