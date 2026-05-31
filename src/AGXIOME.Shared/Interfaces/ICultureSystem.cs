using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>
/// Culture system for sharing universal knowledge, best practices, coding standards,
/// and domain knowledge across all agents in the organization.
/// </summary>
/// <remarks>
/// <para><b>Purpose</b>: Unlike <see cref="ILearningMachine"/> which builds per-user/per-entity knowledge,
/// the culture system stores organization-wide principles and best practices that every agent
/// should be aware of. Think of it as the "company handbook" for AI agents.</para>
/// <para><b>Categories</b>: Entries are grouped by category (principle, best_practice, style_guide,
/// domain_knowledge) for targeted retrieval.</para>
/// <para><b>Auto-update</b>: <see cref="UpdateFromInteractionAsync"/> allows periodic analysis
/// of agent interactions to suggest new culture entries or updates to existing ones.</para>
/// </remarks>
/// <example>
/// <code>
/// var cs = new PostgresCultureSystem(connectionString);
/// await cs.AddCulturalKnowledgeAsync(new CultureEntry(
///     "solid", "SOLID Principles",
///     "Single Responsibility, Open-Closed, Liskov Substitution, Interface Segregation, Dependency Inversion",
///     "principle", Source: "Architect", Priority: 10));
///
/// var results = await cs.SearchCultureAsync("SOLID");
/// // Returns the SOLID principles entry
/// </code>
/// </example>
public interface ICultureSystem
{
    /// <summary>
    /// Adds a new culture entry or updates an existing one with the same <see cref="CultureEntry.Id"/>.
    /// </summary>
    /// <param name="entry">The culture entry to store.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddCulturalKnowledgeAsync(CultureEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Searches culture entries by title and content (case-insensitive substring match).
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="topK">Maximum results to return. Default 5.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching culture entries, ordered by priority (highest first).</returns>
    Task<IReadOnlyList<CultureEntry>> SearchCultureAsync(string query, int topK = 5, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all culture entries, optionally filtered by category.
    /// </summary>
    /// <param name="category">Optional category filter (principle, best_practice, style_guide, domain_knowledge).
    /// If null, returns all entries.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<CultureEntry>> GetAllAsync(string? category = null, CancellationToken ct = default);

    /// <summary>
    /// Called periodically to analyze agent interactions and suggest new or updated culture entries.
    /// Implementation is provider-specific and may be a no-op for in-memory stores.
    /// </summary>
    /// <param name="agentId">The agent whose interactions to analyze.</param>
    /// <param name="interaction">The interaction text to learn from.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateFromInteractionAsync(string agentId, string interaction, CancellationToken ct = default);
}
