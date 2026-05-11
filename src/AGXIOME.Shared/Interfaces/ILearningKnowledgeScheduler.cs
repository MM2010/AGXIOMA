using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

public interface ILearningMachine
{
    void Configure(LearningConfig config);
    Task SaveUserMemoryAsync(string userId, string content, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(string userId, int limit = 20, CancellationToken ct = default);
    Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct = default);
    Task UpdateProfileAsync(string userId, UserProfile profile, CancellationToken ct = default);
    Task SaveEntityFactAsync(string entityId, string fact, CancellationToken ct = default);
    Task<IReadOnlyList<EntityMemory>> GetEntityMemoriesAsync(string entityId, CancellationToken ct = default);
    Task<SessionContext> GetSessionContextAsync(string sessionId, CancellationToken ct = default);
    Task UpdateSessionContextAsync(string sessionId, SessionContext context, CancellationToken ct = default);
    Task SaveLearningAsync(string insight, IReadOnlyList<string> topics, CancellationToken ct = default);
    Task<IReadOnlyList<LearnedKnowledge>> SearchLearningsAsync(string query, int topK = 5, CancellationToken ct = default);
    Task LogDecisionAsync(DecisionLogEntry entry, CancellationToken ct = default);
    Task ProcessAsync(string agentId, string input, string output, CancellationToken ct = default);
}

public interface IKnowledgeBase
{
    Task IngestAsync(Document document, CancellationToken ct = default);
    Task IngestBatchAsync(IAsyncEnumerable<Document> documents, CancellationToken ct = default);
    Task DeleteAsync(string documentId, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, KnowledgeSearchOptions? options = null, CancellationToken ct = default);
}

public interface IWorkflowScheduler
{
    Task<Schedule> CreateScheduleAsync(string name, string workflowId, string cronExpression, SchedulePayload? payload = null, CancellationToken ct = default);
    Task UpdateScheduleAsync(string scheduleId, string? cronExpression = null, bool? enabled = null, CancellationToken ct = default);
    Task DeleteScheduleAsync(string scheduleId, CancellationToken ct = default);
    Task<IReadOnlyList<Schedule>> ListSchedulesAsync(CancellationToken ct = default);
    Task TriggerAsync(string scheduleId, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleRun>> GetRunHistoryAsync(string scheduleId, int limit = 20, CancellationToken ct = default);
}

public interface IDependencyProvider
{
    T GetDependency<T>() where T : class;
    void RegisterDependency<T>(T instance) where T : class;
    void RegisterDependency<T>(Func<T> factory) where T : class;
}
