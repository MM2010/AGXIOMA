using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;
using System.Collections.Concurrent;

namespace AGXIOME.Tests.F25;

[Trait("Phase", "F25")]
[Trait("Component", "LearningMachine")]
public class F25_TS01_LearningMachine_HappyPath
{
    private static ILearningMachine Build()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        return lm;
    }

    [Fact]
    public async Task SaveAndGetUserMemory_ReturnsSaved()
    {
        var lm = Build();
        await lm.SaveUserMemoryAsync("u1", "Loves functional programming");
        var mems = await lm.GetUserMemoriesAsync("u1", 20);
        Assert.Contains(mems, m => m.Content == "Loves functional programming");
    }

    [Fact]
    public async Task SaveUserMemory_MultipleEntries_ReturnsAll()
    {
        var lm = Build();
        await lm.SaveUserMemoryAsync("u1", "A");
        await lm.SaveUserMemoryAsync("u1", "B");
        await lm.SaveUserMemoryAsync("u1", "C");
        var mems = await lm.GetUserMemoriesAsync("u1", 20);
        Assert.Equal(3, mems.Count);
    }

    [Fact]
    public async Task GetUserMemory_Limit_RespectsTopK()
    {
        var lm = Build();
        for (int i = 0; i < 10; i++)
            await lm.SaveUserMemoryAsync("u1", $"Entry {i}");
        var mems = await lm.GetUserMemoriesAsync("u1", 3);
        Assert.Equal(3, mems.Count);
    }

    [Fact]
    public async Task GetUserMemory_UnknownUser_ReturnsEmpty()
    {
        var lm = Build();
        var mems = await lm.GetUserMemoriesAsync("nobody", 20);
        Assert.Empty(mems);
    }

    [Fact]
    public async Task GetOrCreateProfile_NewUser_CreatesDefault()
    {
        var lm = Build();
        var profile = await lm.GetOrCreateProfileAsync("newuser");
        Assert.NotNull(profile);
        Assert.Equal("newuser", profile.UserId);
    }

    [Fact]
    public async Task UpdateProfile_ChangesPersisted()
    {
        var lm = Build();
        var profile = await lm.GetOrCreateProfileAsync("u1");
        var updated = profile with { PreferredName = "TestUser" };
        await lm.UpdateProfileAsync("u1", updated);
        var reloaded = await lm.GetOrCreateProfileAsync("u1");
        Assert.Equal("TestUser", reloaded.PreferredName);
    }

    [Fact]
    public async Task SaveEntityFact_ThenGetEntityMemories_ReturnsIt()
    {
        var lm = Build();
        await lm.SaveEntityFactAsync("e1", "Is a .NET expert");
        var mems = await lm.GetEntityMemoriesAsync("e1");
        Assert.Contains(mems, m => m.Content == "Is a .NET expert");
    }

    [Fact]
    public async Task GetEntityMemories_UnknownEntity_ReturnsEmpty()
    {
        var lm = Build();
        var mems = await lm.GetEntityMemoriesAsync("unknown");
        Assert.Empty(mems);
    }

    [Fact]
    public async Task SessionContext_Default_NotNull()
    {
        var lm = Build();
        var ctx = await lm.GetSessionContextAsync("s1");
        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task UpdateSessionContext_PersistsChanges()
    {
        var lm = Build();
        var ctx = await lm.GetSessionContextAsync("s1");
        var updated = ctx with { Summary = "Test summary" };
        await lm.UpdateSessionContextAsync("s1", updated);
        var reloaded = await lm.GetSessionContextAsync("s1");
        Assert.Equal("Test summary", reloaded.Summary);
    }

    [Fact]
    public async Task SaveLearning_ThenSearch_ReturnsResult()
    {
        var lm = Build();
        await lm.SaveLearningAsync("C# is statically typed", new[] { "csharp", "dotnet" });
        var results = await lm.SearchLearningsAsync("C#", 5);
        Assert.Contains(results, r => r.Content.Contains("C#"));
    }

    [Fact]
    public async Task SearchLearnings_NoMatch_ReturnsEmpty()
    {
        var lm = Build();
        var results = await lm.SearchLearningsAsync("nonexistent", 5);
        Assert.Empty(results);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotThrow()
    {
        var lm = Build();
        await lm.ProcessAsync("agent1", "What is C#?", "C# is a language");
    }

    [Fact]
    public async Task MultipleUsers_DoNotInterfere()
    {
        var lm = Build();
        await lm.SaveUserMemoryAsync("u1", "U1 data");
        await lm.SaveUserMemoryAsync("u2", "U2 data");
        var u1 = await lm.GetUserMemoriesAsync("u1", 20);
        var u2 = await lm.GetUserMemoriesAsync("u2", 20);
        Assert.Single(u1);
        Assert.Single(u2);
        Assert.DoesNotContain(u1, m => m.Content == "U2 data");
    }

    [Fact]
    public async Task Config_DefaultValues_AreSane()
    {
        var cfg = new LearningConfig();
        Assert.Equal(LearningMode.Always, cfg.UserMemoryMode);
        Assert.Equal(LearningMode.Always, cfg.UserProfileMode);
        Assert.Equal(LearningMode.Agentic, cfg.EntityMemoryMode);
        Assert.True(cfg.EnableOptimization);
        Assert.Equal(50, cfg.MaxMemoriesBeforeOptimization);
    }

    [Fact]
    public async Task FullRoundTrip_AllStores()
    {
        var lm = Build();
        await lm.SaveUserMemoryAsync("u1", "Memory 1");
        await lm.SaveUserMemoryAsync("u1", "Memory 2");
        await lm.SaveEntityFactAsync("e1", "Entity fact");
        await lm.SaveLearningAsync("Insight", new[] { "topic" });
        await lm.ProcessAsync("a1", "in", "out");

        var mems = await lm.GetUserMemoriesAsync("u1", 20);
        var entities = await lm.GetEntityMemoriesAsync("e1");
        var learnings = await lm.SearchLearningsAsync("Insight", 5);

        Assert.Equal(2, mems.Count);
        Assert.Single(entities);
        Assert.Single(learnings);
    }
}

[Trait("Phase", "F25")]
[Trait("Component", "LearningMachine")]
public class F25_TS02_LearningMachine_Offensive
{
    [Fact]
    public async Task SaveUserMemory_EmptyContent_DoesNotThrow()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        await lm.SaveUserMemoryAsync("u1", "");
        var mems = await lm.GetUserMemoriesAsync("u1", 20);
        Assert.Contains(mems, m => m.Content == "");
    }

    [Fact]
    public async Task SaveUserMemory_NullContent_DoesNotThrow()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        await lm.SaveUserMemoryAsync("u1", null!);
        var mems = await lm.GetUserMemoriesAsync("u1", 20);
        Assert.Contains(mems, m => m.Content == null);
    }

    [Fact]
    public async Task GetOrCreateProfile_EmptyUserId_DoesNotThrow()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        var profile = await lm.GetOrCreateProfileAsync("");
        Assert.NotNull(profile);
    }

    [Fact]
    public async Task SaveEntityFact_EmptyFact_DoesNotThrow()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        await lm.SaveEntityFactAsync("e1", "");
        var mems = await lm.GetEntityMemoriesAsync("e1");
        Assert.NotEmpty(mems);
    }

    [Fact]
    public async Task ProcessAsync_NullInput_DoesNotThrow()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        await lm.ProcessAsync("a1", null!, null!);
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotCorrupt()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(new LearningConfig());
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var idx = i;
            tasks.Add(lm.SaveUserMemoryAsync("u1", $"Concurrent {idx}"));
        }
        await Task.WhenAll(tasks);
        var mems = await lm.GetUserMemoriesAsync("u1", 100);
        Assert.Equal(50, mems.Count);
    }

    [Fact]
    public async Task Configure_WithNull_DoesNotThrow()
    {
        var lm = new InMemoryLearningMachine();
        lm.Configure(null!);
        await lm.SaveUserMemoryAsync("u1", "test");
        var mems = await lm.GetUserMemoriesAsync("u1", 20);
        Assert.NotEmpty(mems);
    }
}

// ── In-Memory Implementation ──

public class InMemoryLearningMachine : ILearningMachine
{
    private readonly ConcurrentDictionary<string, List<MemoryEntry>> _userMemories = new();
    private readonly ConcurrentDictionary<string, UserProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, List<EntityMemory>> _entityMemories = new();
    private readonly ConcurrentDictionary<string, SessionContext> _sessionContexts = new();
    private readonly List<LearnedKnowledge> _learnedKnowledge = new();
    private readonly object _lkLock = new();
    private LearningConfig? _config;

    public void Configure(LearningConfig config) { _config = config; }

    public Task SaveUserMemoryAsync(string userId, string content, CancellationToken ct = default)
    {
        var entry = new MemoryEntry(
            Id: Guid.NewGuid().ToString(),
            UserId: userId,
            Content: content,
            Topics: Array.Empty<string>(),
            CreatedAt: DateTime.UtcNow);
        _userMemories.AddOrUpdate(userId,
            _ => new() { entry },
            (_, list) => { list.Add(entry); return list; });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(
        string userId, int limit = 20, CancellationToken ct = default)
    {
        if (!_userMemories.TryGetValue(userId, out var list))
            return Task.FromResult<IReadOnlyList<MemoryEntry>>(Array.Empty<MemoryEntry>());
        return Task.FromResult<IReadOnlyList<MemoryEntry>>(
            list.OrderByDescending(m => m.CreatedAt).Take(limit).ToList());
    }

    public Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct = default)
    {
        var profile = _profiles.GetOrAdd(userId,
            _ => new UserProfile(userId, null, null, new Dictionary<string, object>()));
        return Task.FromResult(profile);
    }

    public Task UpdateProfileAsync(string userId, UserProfile profile, CancellationToken ct = default)
    {
        _profiles[userId] = profile;
        return Task.CompletedTask;
    }

    public Task SaveEntityFactAsync(string entityId, string fact, CancellationToken ct = default)
    {
        var mem = new EntityMemory(
            Id: Guid.NewGuid().ToString(),
            EntityId: entityId,
            Type: EntityMemoryType.Fact,
            Content: fact);
        _entityMemories.AddOrUpdate(entityId,
            _ => new() { mem },
            (_, list) => { list.Add(mem); return list; });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EntityMemory>> GetEntityMemoriesAsync(
        string entityId, CancellationToken ct = default)
    {
        if (!_entityMemories.TryGetValue(entityId, out var list))
            return Task.FromResult<IReadOnlyList<EntityMemory>>(Array.Empty<EntityMemory>());
        return Task.FromResult<IReadOnlyList<EntityMemory>>(list.ToList());
    }

    public Task<SessionContext> GetSessionContextAsync(string sessionId, CancellationToken ct = default)
    {
        var ctx = _sessionContexts.GetOrAdd(sessionId,
            _ => new SessionContext(sessionId, LearningSessionMode.Summary, null, null, DateTime.UtcNow));
        return Task.FromResult(ctx);
    }

    public Task UpdateSessionContextAsync(string sessionId, SessionContext context, CancellationToken ct = default)
    {
        _sessionContexts[sessionId] = context;
        return Task.CompletedTask;
    }

    public Task SaveLearningAsync(string insight, IReadOnlyList<string> topics, CancellationToken ct = default)
    {
        lock (_lkLock)
        {
            _learnedKnowledge.Add(new LearnedKnowledge(
                Id: Guid.NewGuid().ToString(),
                Content: insight,
                Embedding: null,
                Topics: topics));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LearnedKnowledge>> SearchLearningsAsync(
        string query, int topK = 5, CancellationToken ct = default)
    {
        lock (_lkLock)
        {
            var results = _learnedKnowledge
                .Where(l => l.Content.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || l.Topics.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Take(topK)
                .ToList();
            return Task.FromResult<IReadOnlyList<LearnedKnowledge>>(results);
        }
    }

    public Task LogDecisionAsync(DecisionLogEntry entry, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ProcessAsync(string agentId, string input, string output, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
