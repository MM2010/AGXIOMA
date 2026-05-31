using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Interfaces;

/// <summary>Culture system for sharing universal knowledge across agents.</summary>
public interface ICultureSystem
{
    Task AddCulturalKnowledgeAsync(CultureEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<CultureEntry>> SearchCultureAsync(string query, int topK = 5, CancellationToken ct = default);
    Task<IReadOnlyList<CultureEntry>> GetAllAsync(string? category = null, CancellationToken ct = default);
    Task UpdateFromInteractionAsync(string agentId, string interaction, CancellationToken ct = default);
}
