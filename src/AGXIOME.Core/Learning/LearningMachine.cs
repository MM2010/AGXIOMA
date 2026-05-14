using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Learning;

public sealed class LearningMachine : ILearningMachine
{
    private readonly LearningConfig _config;

    public LearningMachine(LearningConfig config) => _config = config;

    public void Configure(LearningConfig config) { /* update config */ }

    public async Task ProcessAsync(string agentId, string input, string output, CancellationToken ct = default)
    {
        // Always-mode: auto-save
        if (_config.UserMemoryMode == LearningMode.Always)
            await SaveUserMemoryAsync("user", $"Observed from conversation: {output}", ct);
        if (_config.UserProfileMode == LearningMode.Always)
            await UpdateProfileAsync("user", new UserProfile("user", null, null, new()), ct);
    }

    public Task SaveUserMemoryAsync(string userId, string content, CancellationToken ct = default)
    {
        // Persist to learning store (PG in production)
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(string userId, int limit = 20, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MemoryEntry>>(Array.Empty<MemoryEntry>());

    public Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct = default)
        => Task.FromResult(new UserProfile(userId, null, null, new()));

    public Task UpdateProfileAsync(string userId, UserProfile profile, CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveEntityFactAsync(string entityId, string fact, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<EntityMemory>> GetEntityMemoriesAsync(string entityId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EntityMemory>>(Array.Empty<EntityMemory>());

    public Task<SessionContext> GetSessionContextAsync(string sessionId, CancellationToken ct = default)
        => Task.FromResult(new SessionContext(sessionId, LearningSessionMode.Summary, null));

    public Task UpdateSessionContextAsync(string sessionId, SessionContext context, CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveLearningAsync(string insight, IReadOnlyList<string> topics, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<LearnedKnowledge>> SearchLearningsAsync(string query, int topK = 5, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LearnedKnowledge>>(Array.Empty<LearnedKnowledge>());

    public Task LogDecisionAsync(DecisionLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}
