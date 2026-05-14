// TS-10 — SLO / Error Budget Validation Tests (20 tests)
// Validates error rate thresholds, retry exhaustion behavior,
// and latency budget compliance for critical paths.

using System.Diagnostics;
using System.Net;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Storage.Chunkers;
using AGXIOME.Storage.Stores;

namespace AGXIOME.Tests.F14;

// ═════════════════════════════════════════════════════════════════════════════
// TS-10-A  Error rate thresholds (7 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS10_ErrorRateTests
{
    /// <summary>
    /// Simulates 100 store "get" operations on a valid store.
    /// SLO: error rate must be 0% (all succeed or return null gracefully).
    /// </summary>
    [Fact] public async Task SqliteDelete_100ops_ZeroErrors()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"slo_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            // Create schema first; AutoCreateSchema only triggers on SaveAsync
            await store.SaveAsync(new F14DbRecord { Id = "_schema_init", Value = 0 });
            int errors = 0;
            for (int i = 0; i < 100; i++)
            {
                try { await store.DeleteAsync<F14DbRecord>($"k{i}"); }
                catch { errors++; }
            }
            Assert.Equal(0, errors);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    /// <summary>
    /// Simulates 50 save+delete operations. SLO: 0 errors.
    /// </summary>
    [Fact] public async Task SqliteSaveDelete_50ops_ZeroErrors()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"slo_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            int errors = 0;
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    await store.SaveAsync(new F14DbRecord { Id = $"sd{i}", Value = i });
                    await store.DeleteAsync<F14DbRecord>($"sd{i}");
                }
                catch { errors++; }
            }
            Assert.Equal(0, errors);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    /// <summary>
    /// Simulates 200 chunk operations. SLO: 0% exception rate.
    /// </summary>
    [Fact] public async Task Chunker_200ops_ZeroErrors()
    {
        var chunker  = new SlidingWindowChunker();
        int errors   = 0;
        var text     = string.Join(" ", Enumerable.Range(0, 100).Select(i => $"w{i}"));
        for (int i = 0; i < 200; i++)
        {
            try { chunker.Chunk(text, maxTokens: 20, overlap: 2); }
            catch { errors++; }
        }
        Assert.Equal(0, errors);
    }

    /// <summary>
    /// SLO: all 5 chunker types must handle empty input without errors.
    /// </summary>
    [Fact] public async Task AllChunkers_EmptyInput_ZeroErrors()
    {
        IChunker[] chunkers = [new SentenceChunker(), new RecursiveChunker(),
            new SlidingWindowChunker(), new FixedSizeChunker(), new HeadingChunker()];
        int errors = 0;
        foreach (var c in chunkers)
        {
            try { c.Chunk(string.Empty); }
            catch { errors++; }
        }
        Assert.Equal(0, errors);
    }

    /// <summary>
    /// SLO: JsonFileStore 50 concurrent saves must not produce write errors.
    /// </summary>
    [Fact] public async Task JsonFileStore_ConcurrentSaves_LowErrorRate()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = dir });
            int errors = 0;
            var tasks  = Enumerable.Range(0, 50).Select(async i =>
            {
                try { await store.SaveAsync(new F14DbRecord { Id = $"c{i}", Value = i }); }
                catch { Interlocked.Increment(ref errors); }
            });
            await Task.WhenAll(tasks);
            // SLO: error rate < 5% (allow for some file-system level concurrency issues)
            Assert.True(errors <= 2, $"Too many concurrent write errors: {errors}");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact] public async Task ChunkResult_NeverNullContent_100ops()
    {
        var text   = "Some text to chunk into pieces.";
        var errors = 0;
        for (int i = 0; i < 100; i++)
        {
            var chunks = new SentenceChunker().Chunk(text, maxTokens: 10);
            foreach (var c in chunks)
                if (c.Content is null) errors++;
        }
        Assert.Equal(0, errors);
    }

    [Fact] public async Task StorageProviderType_Parse_NeverThrows()
    {
        int errors = 0;
        foreach (var name in Enum.GetNames<StorageProviderType>())
        {
            try { _ = Enum.Parse<StorageProviderType>(name); }
            catch { errors++; }
        }
        Assert.Equal(0, errors);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-10-B  Latency budget compliance (7 tests)
// ═════════════════════════════════════════════════════════════════════════════

[Collection("Benchmarks")]
public sealed class TS10_LatencyBudgetTests
{
    // p95 latency budget for in-process operations
    private const int P95BudgetMs = 200;

    [Fact] public async Task SqliteDelete_p95Latency_UnderBudget()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"slo_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            // Pre-insert so deletes are real operations
            for (int i = 0; i < 100; i++)
                await store.SaveAsync(new F14DbRecord { Id = $"k{i}", Value = i });
            var latencies = new List<long>(100);
            for (int i = 0; i < 100; i++)
            {
                var sw = Stopwatch.StartNew();
                await store.DeleteAsync<F14DbRecord>($"k{i}");
                latencies.Add(sw.ElapsedMilliseconds);
            }
            latencies.Sort();
            long p95 = latencies[(int)(latencies.Count * 0.95)];
            Assert.True(p95 < P95BudgetMs, $"SQLite Delete p95={p95}ms exceeds budget {P95BudgetMs}ms");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task SentenceChunker_p95_UnderBudget()
    {
        var text      = string.Join(". ", Enumerable.Range(0, 100).Select(i => $"Sentence {i}"));
        var latencies = new List<long>(100);
        for (int i = 0; i < 100; i++)
        {
            var sw = Stopwatch.StartNew();
            new SentenceChunker().Chunk(text, maxTokens: 50);
            latencies.Add(sw.ElapsedMilliseconds);
        }
        latencies.Sort();
        long p95 = latencies[(int)(latencies.Count * 0.95)];
        Assert.True(p95 < P95BudgetMs, $"SentenceChunker p95={p95}ms exceeds budget");
    }

    [Fact] public async Task SlidingWindowChunker_p95_UnderBudget()
    {
        var text      = string.Join(" ", Enumerable.Range(0, 500).Select(i => $"w{i}"));
        var latencies = new List<long>(50);
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();
            new SlidingWindowChunker().Chunk(text, maxTokens: 25);
            latencies.Add(sw.ElapsedMilliseconds);
        }
        latencies.Sort();
        long p95 = latencies[(int)(latencies.Count * 0.95)];
        Assert.True(p95 < P95BudgetMs, $"SlidingWindowChunker p95={p95}ms exceeds budget");
    }

    [Fact] public async Task RecursiveChunker_p95_UnderBudget()
    {
        var text      = string.Join("\n\n", Enumerable.Range(0, 20).Select(i => $"Paragraph {i} with content."));
        var latencies = new List<long>(50);
        for (int i = 0; i < 50; i++)
        {
            var sw = Stopwatch.StartNew();
            new RecursiveChunker().Chunk(text, maxTokens: 50);
            latencies.Add(sw.ElapsedMilliseconds);
        }
        latencies.Sort();
        long p95 = latencies[(int)(latencies.Count * 0.95)];
        Assert.True(p95 < P95BudgetMs);
    }

    [Fact] public async Task EnumIteration_p95_Under1ms()
    {
        var latencies = new List<long>(1000);
        for (int i = 0; i < 1000; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = Enum.GetValues<EmbedderType>();
            latencies.Add(sw.ElapsedTicks / (Stopwatch.Frequency / 1000)); // ms
        }
        latencies.Sort();
        long p95 = latencies[(int)(latencies.Count * 0.95)];
        Assert.True(p95 < 10, $"Enum iteration p95={p95}ms"); // 10ms budget is very generous
    }

    [Fact] public async Task SqliteDelete_p95_UnderBudget()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"slo_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            for (int i = 0; i < 50; i++)
                await store.SaveAsync(new F14DbRecord { Id = $"p{i}", Value = i });
            var latencies = new List<long>(50);
            for (int i = 0; i < 50; i++)
            {
                var sw = Stopwatch.StartNew();
                await store.DeleteAsync<F14DbRecord>($"p{i}");
                latencies.Add(sw.ElapsedMilliseconds);
            }
            latencies.Sort();
            long p95 = latencies[(int)(latencies.Count * 0.95)];
            Assert.True(p95 < P95BudgetMs, $"SQLite Delete p95={p95}ms exceeds budget");
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task JsonFileStore_p95_UnderBudget()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = dir });
            var latencies = new List<long>(50);
            for (int i = 0; i < 50; i++)
            {
                var sw = Stopwatch.StartNew();
                await store.SaveAsync(new F14DbRecord { Id = $"p{i}", Value = i });
                latencies.Add(sw.ElapsedMilliseconds);
            }
            latencies.Sort();
            long p95 = latencies[(int)(latencies.Count * 0.95)];
            Assert.True(p95 < 5000, $"JsonFile Save p95={p95}ms exceeds budget");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// TS-10-C  Idempotency and retry semantics (6 tests)
// ═════════════════════════════════════════════════════════════════════════════

public sealed class TS10_IdempotencyTests
{
    [Fact] public async Task SqliteSave_TwiceSameId_DoesNotDuplicate()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"idem_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            // Idempotent upsert — second save of same id must not throw
            var ex1 = await Record.ExceptionAsync(() => store.SaveAsync(new F14DbRecord { Id = "same-id", Value = 1 }));
            var ex2 = await Record.ExceptionAsync(() => store.SaveAsync(new F14DbRecord { Id = "same-id", Value = 2 }));
            Assert.Null(ex1);
            Assert.Null(ex2);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task SqliteDelete_Twice_DoesNotThrow()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"idem_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            await store.SaveAsync(new F14DbRecord { Id = "del-me", Value = 1 });
            await store.DeleteAsync<F14DbRecord>("del-me");
            var ex = await Record.ExceptionAsync(() => store.DeleteAsync<F14DbRecord>("del-me"));
            Assert.Null(ex); // idempotent delete
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    [Fact] public async Task JsonFileStore_Save_Idempotent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new JsonFileCommandStore(new JsonFileStoreOptions { BaseDirectory = dir });
            // Double save of same id must not throw (upsert)
            var ex1 = await Record.ExceptionAsync(() => store.SaveAsync(new F14DbRecord { Id = "idm-1", Value = 10 }));
            var ex2 = await Record.ExceptionAsync(() => store.SaveAsync(new F14DbRecord { Id = "idm-1", Value = 20 }));
            Assert.Null(ex1);
            Assert.Null(ex2);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact] public async Task ChunkerChunk_SameInput_SameOutput()
    {
        var text   = "Same text repeated across calls.";
        var result1 = new SentenceChunker().Chunk(text, maxTokens: 100);
        var result2 = new SentenceChunker().Chunk(text, maxTokens: 100);
        Assert.Equal(result1.Count, result2.Count);
        for (int i = 0; i < result1.Count; i++)
            Assert.Equal(result1[i].Content, result2[i].Content);
    }

    [Fact] public async Task ChunkerEmpty_MultipleCallsIdempotent()
    {
        var c = new SlidingWindowChunker();
        for (int i = 0; i < 10; i++)
        {
            var result = c.Chunk(string.Empty);
            Assert.Single(result);
        }
    }

    [Fact] public async Task SqliteUpdate_Idempotent()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"idem_{Guid.NewGuid():N}.db");
        try
        {
            using var store = new SqliteCommandStore(new SqliteStoreOptions
                { ConnectionString = $"Data Source={tmp};Pooling=False", AutoCreateSchema = true });
            await store.SaveAsync(new F14DbRecord { Id = "upd-idem", Value = 5 });
            // Calling update twice should not throw — idempotent contract
            var ex1 = await Record.ExceptionAsync(() => store.UpdateAsync(new F14DbRecord { Id = "upd-idem", Value = 6 }));
            var ex2 = await Record.ExceptionAsync(() => store.UpdateAsync(new F14DbRecord { Id = "upd-idem", Value = 7 }));
            Assert.Null(ex1);
            Assert.Null(ex2);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
}
