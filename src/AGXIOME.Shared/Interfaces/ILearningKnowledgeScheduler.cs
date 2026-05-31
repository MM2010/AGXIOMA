using AGXIOME.Shared.Models;
using AGXIOME.Shared.Config;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Core learning subsystem that builds agent knowledge across sessions.
/// Supports user memory (unstructured observations), user profiles (structured facts),
/// entity memory (facts/events/relationships), session context, learned cross-user
/// knowledge, and decision logging.
/// </summary>
/// <remarks>
/// <para><b>Architecture</b>: The learning machine sits alongside the agent.
/// After each run, <see cref="ProcessAsync"/> is called to extract insights
/// from the conversation. The learning mode (always / agentic / propose) is
/// controlled via <see cref="LearningConfig"/> passed to <see cref="Configure"/>.</para>
/// <para><b>Storage</b>: All data is persisted to the configured database provider
/// via the session/command store. In-memory fallback is available for testing.</para>
/// <para><b>Thread safety</b>: All methods are thread-safe. Concurrent writes to
/// the same user/entity are serialized internally.</para>
/// </remarks>
/// <example>
/// <code>
/// var lm = new PostgresLearningMachine(connectionString);
/// lm.Configure(new LearningConfig { UserMemoryMode = LearningMode.Always });
/// await lm.SaveUserMemoryAsync("user-1", "Prefers dark mode");
/// var memories = await lm.GetUserMemoriesAsync("user-1", 10);
/// </code>
/// </example>
public interface ILearningMachine
{
    /// <summary>
    /// Configures the learning subsystem. Must be called before any other method.
    /// Can be called multiple times to adjust modes at runtime.
    /// </summary>
    /// <param name="config">The learning configuration. If null, defaults are applied.</param>
    void Configure(LearningConfig config);

    /// <summary>
    /// Saves an unstructured observation about a user (preference, behavior, interest).
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="content">The observation text.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveUserMemoryAsync(string userId, string content, CancellationToken ct = default);

    /// <summary>
    /// Retrieves recent user memories, ordered newest-first.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="limit">Maximum entries to return (1..500). Default 20.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<MemoryEntry>> GetUserMemoriesAsync(string userId, int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Creates or retrieves the structured profile for a user.
    /// Profiles contain explicit fields (name, preferred name) and custom key-value pairs.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UserProfile> GetOrCreateProfileAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Updates the structured profile for a user. Overwrites the existing profile.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="profile">The new profile data.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateProfileAsync(string userId, UserProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Stores a fact about an entity (person, project, organization, etc.).
    /// </summary>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="fact">The fact text.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveEntityFactAsync(string entityId, string fact, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all memories (facts, events, relationships) stored for an entity.
    /// </summary>
    /// <param name="entityId">The entity identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<EntityMemory>> GetEntityMemoriesAsync(string entityId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current session context (summary, goals, planning mode).
    /// Creates a default context if one does not exist for the session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SessionContext> GetSessionContextAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Updates the session context (summary text, goals, mode).
    /// </summary>
    Task UpdateSessionContextAsync(string sessionId, SessionContext context, CancellationToken ct = default);

    /// <summary>
    /// Saves a cross-user insight/learning with associated topics.
    /// This becomes searchable by all agents via <see cref="SearchLearningsAsync"/>.
    /// </summary>
    /// <param name="insight">The insight text.</param>
    /// <param name="topics">Associated topic tags for categorization and search.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveLearningAsync(string insight, IReadOnlyList<string> topics, CancellationToken ct = default);

    /// <summary>
    /// Searches learned knowledge by content and topic tags.
    /// </summary>
    /// <param name="query">Search query (matches content and topic names).</param>
    /// <param name="topK">Maximum results to return. Default 5.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<LearnedKnowledge>> SearchLearningsAsync(string query, int topK = 5, CancellationToken ct = default);

    /// <summary>
    /// Logs a decision made by an agent, including the reasoning and outcome.
    /// Useful for audit trails, debugging, and learning from past decisions.
    /// </summary>
    Task LogDecisionAsync(DecisionLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Main learning trigger. Called after each agent run to auto-extract insights.
    /// The implementation inspects the input/output pair and extracts user preferences,
    /// entity facts, and learned knowledge according to the configured learning modes.
    /// </summary>
    /// <param name="agentId">The agent that produced the interaction.</param>
    /// <param name="input">The user input (prompt).</param>
    /// <param name="output">The agent output (response).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessAsync(string agentId, string input, string output, CancellationToken ct = default);
}

/// <summary>
/// Knowledge base for document ingestion and retrieval-augmented generation (RAG).
/// Documents are chunked, embedded, and indexed for semantic search.
/// </summary>
public interface IKnowledgeBase
{
    /// <summary>Ingests a single document into the knowledge base.</summary>
    /// <param name="document">The document to ingest.</param>
    Task IngestAsync(Document document, CancellationToken ct = default);

    /// <summary>Ingests a batch of documents. Order is preserved.</summary>
    Task IngestBatchAsync(IAsyncEnumerable<Document> documents, CancellationToken ct = default);

    /// <summary>Deletes a document and all its chunks from the knowledge base.</summary>
    Task DeleteAsync(string documentId, CancellationToken ct = default);

    /// <summary>
    /// Searches the knowledge base for chunks relevant to the query.
    /// Uses semantic (vector) search with optional keyword pre-filtering.
    /// </summary>
    /// <returns>Ranked list of matching chunks, highest relevance first.</returns>
    Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(string query, KnowledgeSearchOptions? options = null, CancellationToken ct = default);
}

/// <summary>
/// Cron-based workflow scheduler. Creates recurring execution schedules
/// for agent workflows with optional payload overrides.
/// </summary>
public interface IWorkflowScheduler
{
    /// <summary>Creates a new recurring schedule.</summary>
    Task<Schedule> CreateScheduleAsync(string name, string workflowId, string cronExpression, SchedulePayload? payload = null, CancellationToken ct = default);

    /// <summary>Updates an existing schedule (cron expression, enabled/disabled).</summary>
    Task UpdateScheduleAsync(string scheduleId, string? cronExpression = null, bool? enabled = null, CancellationToken ct = default);

    /// <summary>Deletes a schedule permanently.</summary>
    Task DeleteScheduleAsync(string scheduleId, CancellationToken ct = default);

    /// <summary>Lists all registered schedules.</summary>
    Task<IReadOnlyList<Schedule>> ListSchedulesAsync(CancellationToken ct = default);

    /// <summary>Triggers an immediate execution of the schedule, outside its normal cron timing.</summary>
    Task TriggerAsync(string scheduleId, CancellationToken ct = default);

    /// <summary>Retrieves recent execution history for a schedule, newest first.</summary>
    Task<IReadOnlyList<ScheduleRun>> GetRunHistoryAsync(string scheduleId, int limit = 20, CancellationToken ct = default);
}

/// <summary>
/// Lightweight dependency injection container scoped to agent/team/workflow context.
/// Provides resolution and registration of transient dependencies without a full DI container.
/// </summary>
public interface IDependencyProvider
{
    /// <summary>Resolves a registered dependency.</summary>
    T GetDependency<T>() where T : class;

    /// <summary>Registers a singleton instance.</summary>
    void RegisterDependency<T>(T instance) where T : class;

    /// <summary>Registers a factory that creates a new instance on each resolution.</summary>
    void RegisterDependency<T>(Func<T> factory) where T : class;
}
