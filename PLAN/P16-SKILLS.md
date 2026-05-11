# P16 — Skills System

## File: `Skills/AgxiomeSkill.cs`, `Skills/SkillManager.cs`

Namespace: `AGXIOME.Core.Skills`

## Classi

### `IAgxiomeSkill`

```csharp
public interface IAgxiomeSkill
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string? Version { get; }
    string? Category { get; }
    string? BaseSkillId { get; }
    IReadOnlyList<string> ToolIds { get; }
    string? SystemPromptExtension { get; }
    string? KnowledgeBaseId { get; }
    Dictionary<string, object> DefaultConfig { get; }
}
```

### `SkillManager : ISkillManager`

```csharp
public sealed class SkillManager : ISkillManager
{
    private readonly ConcurrentDictionary<string, IAgxiomeSkill> _skills = new();

    public void RegisterSkill(IAgxiomeSkill skill) => _skills[skill.Id] = skill;
    public IAgxiomeSkill? GetSkill(string id) => _skills.GetValueOrDefault(id);
    public IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null)
        => _skills.Values.Where(s => category == null || s.Category == category).ToList();

    public async Task<IAgxiomeSkill> DownloadSkillAsync(string id, string source, CancellationToken ct)
    {
        // HTTP GET {source}/skills/{id}/manifest.json
        // Scarica assembly tool
        // Carica in AssemblyLoadContext isolato
        // Return IAgxiomeSkill
    }

    public void ApplySkillToAgent(IAgxiomeAgent agent, string skillId)
    {
        var skill = GetSkill(skillId) ?? throw new KeyNotFoundException(skillId);
        // 1. Resolve dipendenze
        // 2. Per ogni toolId: agent.RegisterFunction<T>()
        // 3. Aggiungi SystemPromptExtension
        // 4. Carica KnowledgeBase
    }
}
```

### `RemoteSkillRegistry`

```csharp
public class RemoteSkillRegistry
{
    private readonly HttpClient _http;

    public async Task<IReadOnlyList<SkillManifest>> ListAsync(string registryUrl, CancellationToken ct)
    {
        return await _http.GetFromJsonAsync<List<SkillManifest>>(
            $"{registryUrl}/skills/index.json", ct) ?? new();
    }
}

public record SkillManifest(string Id, string Name, string Description, string Version,
    string? Category, string? Author, Uri? DownloadUrl, IReadOnlyList<string> Tags);
```

## Test

| Test | Descrizione |
|------|------------|
| Register + GetSkill | Skill retrieved |
| Apply skill to agent | Tools registrati, prompt esteso |
| Remote download | Skill scaricata e caricata |
| Skill composition | ToolIds merged |
