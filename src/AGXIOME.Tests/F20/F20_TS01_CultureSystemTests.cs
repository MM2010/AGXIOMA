using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Tests.F20;

[Trait("Phase", "F20")]
[Trait("Component", "CultureSystem")]
public class F20_TS01_CultureSystem_HappyPath
{
    private static ICultureSystem Build() => new InMemoryCultureSystem();

    [Fact]
    public async Task AddAndSearchCulture_ReturnsEntry()
    {
        var cs = Build();
        var entry = new CultureEntry(
            Id: "c1",
            Title: "SOLID Principles",
            Content: "Single responsibility, Open-closed, Liskov substitution, Interface segregation, Dependency inversion",
            Category: "principle",
            Source: "Architect",
            Priority: 10,
            CreatedAt: DateTime.UtcNow);
        await cs.AddCulturalKnowledgeAsync(entry);
        var results = await cs.SearchCultureAsync("SOLID", 5);
        Assert.Contains(results, r => r.Title == "SOLID Principles");
    }

    [Fact]
    public async Task GetAll_ByCategory_FiltersCorrectly()
    {
        var cs = Build();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c1", "A", "a", "principle", Priority: 1));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c2", "B", "b", "best_practice", Priority: 1));
        var principles = await cs.GetAllAsync("principle");
        Assert.Single(principles);
        Assert.Equal("A", principles[0].Title);
    }

    [Fact]
    public async Task GetAll_NoCategory_ReturnsAll()
    {
        var cs = Build();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c1", "A", "a", "principle", Priority: 1));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c2", "B", "b", "best_practice", Priority: 1));
        var all = await cs.GetAllAsync(null);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task SearchCulture_NoMatch_ReturnsEmpty()
    {
        var cs = Build();
        var results = await cs.SearchCultureAsync("zzzzz", 5);
        Assert.Empty(results);
    }

    [Fact]
    public async Task UpdateFromInteraction_DoesNotThrow()
    {
        var cs = Build();
        await cs.UpdateFromInteractionAsync("agent1", "User asked about SOLID");
    }

    [Fact]
    public async Task AddMultiple_Categories_ReturnsCorrectCounts()
    {
        var cs = Build();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c1", "P1", "a", "principle", Priority: 1));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c2", "P2", "b", "principle", Priority: 2));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c3", "BP1", "c", "best_practice", Priority: 1));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c4", "DK1", "d", "domain_knowledge", Priority: 3));
        Assert.Equal(2, (await cs.GetAllAsync("principle")).Count);
        Assert.Single(await cs.GetAllAsync("best_practice"));
        Assert.Single(await cs.GetAllAsync("domain_knowledge"));
        Assert.Equal(4, (await cs.GetAllAsync(null)).Count);
    }

    [Fact]
    public async Task SearchCulture_MatchesTitleAndContent()
    {
        var cs = Build();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c1", "C# Coding Standards", "Use PascalCase for methods", "style_guide", Priority: 5));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c2", "Error Handling", "Never swallow exceptions", "best_practice", Priority: 5));
        var byTitle = await cs.SearchCultureAsync("Coding", 5);
        var byContent = await cs.SearchCultureAsync("swallow", 5);
        Assert.Single(byTitle);
        Assert.Single(byContent);
    }
}

[Trait("Phase", "F20")]
[Trait("Component", "CultureSystem")]
public class F20_TS02_CultureSystem_Offensive
{
    [Fact]
    public async Task AddEntry_NullId_DoesNotThrow()
    {
        var cs = new InMemoryCultureSystem();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry(null!, "T", "C", "cat"));
    }

    [Fact]
    public async Task SearchCulture_EmptyQuery_ReturnsAll()
    {
        var cs = new InMemoryCultureSystem();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c1", "A", "a", "cat"));
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c2", "B", "b", "cat"));
        var results = await cs.SearchCultureAsync("", 5);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetAll_UnknownCategory_ReturnsEmpty()
    {
        var cs = new InMemoryCultureSystem();
        await cs.AddCulturalKnowledgeAsync(new CultureEntry("c1", "A", "a", "principle"));
        var results = await cs.GetAllAsync("nonexistent");
        Assert.Empty(results);
    }

    [Fact]
    public async Task ConcurrentAdds_DoNotCorrupt()
    {
        var cs = new InMemoryCultureSystem();
        var tasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            var idx = i;
            tasks.Add(cs.AddCulturalKnowledgeAsync(new CultureEntry($"c{idx}", $"Title{idx}", $"Content{idx}", "test")));
        }
        await Task.WhenAll(tasks);
        var all = await cs.GetAllAsync(null);
        Assert.Equal(50, all.Count);
    }

    [Fact]
    public async Task UpdateFromInteraction_NullAgentId_DoesNotThrow()
    {
        var cs = new InMemoryCultureSystem();
        await cs.UpdateFromInteractionAsync(null!, "test interaction");
    }
}

// ── In-Memory Implementation ──

public class InMemoryCultureSystem : ICultureSystem
{
    private readonly List<CultureEntry> _entries = new();
    private readonly object _lock = new();

    public Task AddCulturalKnowledgeAsync(CultureEntry entry, CancellationToken ct = default)
    {
        lock (_lock) { _entries.Add(entry); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CultureEntry>> SearchCultureAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(query))
                return Task.FromResult<IReadOnlyList<CultureEntry>>(_entries.Take(topK).ToList());
            var results = _entries
                .Where(e => e.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || e.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(topK)
                .ToList();
            return Task.FromResult<IReadOnlyList<CultureEntry>>(results);
        }
    }

    public Task<IReadOnlyList<CultureEntry>> GetAllAsync(string? category = null, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var results = category is null
                ? _entries.ToList()
                : _entries.Where(e => e.Category == category).ToList();
            return Task.FromResult<IReadOnlyList<CultureEntry>>(results);
        }
    }

    public Task UpdateFromInteractionAsync(string agentId, string interaction, CancellationToken ct = default)
    {
        // In-memory: learning from interaction is implementation-defined
        return Task.CompletedTask;
    }
}
