namespace AGXIOME.Shared.Models;

/// <summary>A culture/knowledge entry defining agent behaviour rules and organizational norms.</summary>
public record CultureEntry(
    string Id,
    string Title,
    string Content,
    string Category,
    string? Source = null,
    int Priority = 0,
    DateTime CreatedAt = default,
    DateTime? UpdatedAt = null
);

/// <summary>Reference to a skill for agent configuration.</summary>
public record SkillRef(string SkillId, string? Source = null, Dictionary<string, object>? Params = null);
