# 14 — Skills System

## Scopo

Skill = pacchetto distribuibile di tool + prompt + knowledge + configurazione.
Skills possono essere derivate, composte, scaricate da registry remoti (skills.sh style).

## Architettura Skills

```
                    ┌──────────────────────────┐
                    │   Skill Registry         │
                    │  (locale + remoto)       │
                    └───────────┬──────────────┘
                                │
          ┌─────────────────────┼─────────────────────┐
          │                     │                     │
          ▼                     ▼                     ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│  Base Skill      │  │  Derived Skill   │  │  Composed Skill  │
│  (pure virtual)  │  │  (extends base)  │  │  (merge N skill) │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

## Contratti

```csharp
public interface IAgxiomeSkill
{
    string Id { get; }                         // "research.web"
    string Name { get; }                       // "Web Research"
    string Description { get; }
    string? Version { get; }                   // SemVer
    string? Category { get; }                  // "research", "coding", "data"
    string? BaseSkillId { get; }               // Per derivazione
    IReadOnlyList<string> ToolIds { get; }     // Tool type names o registrations
    IReadOnlyList<string> RequiredSkills { get; } // Dipendenze
    string? SystemPromptExtension { get; }     // Prompt supplementare
    string? KnowledgeBaseId { get; }           // Knowledge base associata
    Dictionary<string, object> DefaultConfig { get; }
}

public interface ISkillManager
{
    // Registry
    void RegisterSkill(IAgxiomeSkill skill);
    void UnregisterSkill(string skillId);
    IAgxiomeSkill? GetSkill(string skillId);
    IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null);

    // Remote (stile skills.sh)
    Task<IAgxiomeSkill> DownloadSkillAsync(string skillId, string source, CancellationToken ct);
    Task<IReadOnlyList<SkillManifest>> ListRemoteSkillsAsync(string registryUrl, CancellationToken ct);

    // Applicazione
    void ApplySkillToAgent(IAgxiomeAgent agent, string skillId);
    void RemoveSkillFromAgent(IAgxiomeAgent agent, string skillId);
    IReadOnlyList<IAgxiomeSkill> GetAgentSkills(string agentId);
}

public record SkillManifest(
    string Id,
    string Name,
    string Description,
    string Version,
    string? Category,
    string? Author,
    Uri? DownloadUrl,
    IReadOnlyList<string> Tags
);
```

## Skill derivazione

```
                    ┌─────────────────────┐
                    │   BaseResearchSkill │  ← Tool: web_search, knowledge_search
                    │                     │     Prompt: "You are a researcher"
                    └──────────┬──────────┘
                               │ extends
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
    ┌────────────────┐ ┌────────────────┐ ┌────────────────┐
    │ WebResearch    │ │ AcademicRes.   │ │ MarketRes.     │
    │ + web_scrape   │ │ + pdf_reader   │ │ + stock_tool   │
    │ + http_request │ │ + citation_tool│ │ + chart_tool   │
    │ Prompt: focus  │ │ Prompt: format │ │ Prompt: data   │
    │ "web sources"  │ │ "academic"     │ │ "market"       │
    └────────────────┘ └────────────────┘ └────────────────┘
```

```csharp
// ── Esempio derivazione ──
public class WebResearchSkill : IAgxiomeSkill
{
    public string Id => "research.web.v2";
    public string Name => "Web Research";
    public string Description => "Ricerca web con scraping e sintesi";
    public string? Version => "2.0.0";
    public string? Category => "research";
    public string? BaseSkillId => "research.base"; // Deriva da base
    public IReadOnlyList<string> ToolIds => new[]
    {
        "AGXIOME.Tools.WebSearchTool",
        "AGXIOME.Tools.WebScrapeTool",
        "AGXIOME.Tools.HttpRequestTool"
    };
    public IReadOnlyList<string> RequiredSkills => new[] { "research.base" };
    public string? SystemPromptExtension =>
        "You have web research capabilities. Use web_search for queries, " +
        "web_scrape to extract content from URLs, and http_request for APIs. " +
        "Always cite your sources with URLs.";
    public string? KnowledgeBaseId => "kb-web-research";
    public Dictionary<string, object> DefaultConfig => new()
    {
        ["web_search.max_results"] = 10,
        ["web_scrape.max_size_kb"] = 512
    };
}
```

## Skill composition

```csharp
public class ComposedSkill : IAgxiomeSkill
{
    public string Id { get; }
    public string Name => $"Composed: {string.Join(" + ", _skills.Select(s => s.Name))}";
    public string Description => "Skill composta da multiple skills";
    public string? Version => "1.0.0";
    public string? Category => null;
    public string? BaseSkillId => null;
    public IReadOnlyList<string> ToolIds => _skills.SelectMany(s => s.ToolIds).Distinct().ToList();
    public IReadOnlyList<string> RequiredSkills => _skills.Select(s => s.Id).ToList();
    public string? SystemPromptExtension =>
        string.Join("\n", _skills.Select(s => s.SystemPromptExtension).Where(p => p != null));
    public string? KnowledgeBaseId => null;
    public Dictionary<string, object> DefaultConfig =>
        _skills.SelectMany(s => s.DefaultConfig).ToDictionary(kv => kv.Key, kv => kv.Value);

    private readonly List<IAgxiomeSkill> _skills;

    public ComposedSkill(string id, List<IAgxiomeSkill> skills)
    {
        Id = id;
        _skills = skills;
    }
}

// Uso:
var composed = new ComposedSkill("data-intelligence", new List<IAgxiomeSkill>
{
    new WebResearchSkill(),
    new DataAnalysisSkill(),
    new ReportingSkill()
});
manager.RegisterSkill(composed);
```

## Remote skill registry (skills.sh style)

```csharp
public class RemoteSkillRegistry
{
    private readonly HttpClient _http;
    private readonly string _registryUrl;

    public RemoteSkillRegistry(string registryUrl)
    {
        _registryUrl = registryUrl;
        _http = new HttpClient();
    }

    public async Task<IReadOnlyList<SkillManifest>> ListAsync(CancellationToken ct)
    {
        return await _http.GetFromJsonAsync<List<SkillManifest>>(
            $"{_registryUrl}/skills/index.json", ct) ?? new();
    }

    public async Task<IAgxiomeSkill> DownloadAsync(
        string skillId, CancellationToken ct)
    {
        // Formato: JSON con definizione skill + tool assembly inclusi
        var manifest = await _http.GetFromJsonAsync<SkillManifest>(
            $"{_registryUrl}/skills/{skillId}/manifest.json", ct);

        // Carica tool assembly
        var toolAssembly = await _http.GetByteArrayAsync(
            $"{_registryUrl}/skills/{skillId}/tools.dll", ct);
        var assembly = Assembly.Load(toolAssembly);

        // Crea istanza
        return DeserializeSkill(manifest, assembly);
    }
}

// Formato skills/index.json sul registry remoto:
{
  "skills": [
    {
      "id": "research.web.v2",
      "name": "Web Research",
      "description": "Web search + scraping + synthesis",
      "version": "2.0.0",
      "category": "research",
      "author": "AGXIOME",
      "download_url": "https://skills.agxiome.io/v1/research.web.v2/",
      "tags": ["web", "search", "scraping", "rag"]
    },
    {
      "id": "data.analyst.v1",
      "name": "Data Analyst",
      "description": "SQL query + statistics + charting",
      "version": "1.0.0",
      "category": "data",
      "author": "AGXIOME",
      "download_url": "https://skills.agxiome.io/v1/data.analyst.v1/",
      "tags": ["data", "sql", "analysis", "charts"]
    }
  ]
}
```

## Skill lifecycle

```
RegisterSkill(skill)
       │
       ▼
Validate: toolIds esistono? requiredSkills risolte?
       │
       ▼
Store in SkillRegistry (concorrente, thread-safe)
       │
       ▼
ApplySkillToAgent(agent, skillId):
  1. Resolve dipendenze (requiredSkills)
  2. Per ogni toolId: RegisterFunction<T>() sull'agente
  3. Aggiungi SystemPromptExtension al prompt
  4. Carica KnowledgeBaseId (se presente)
  5. Applica DefaultConfig all'agente
       │
       ▼
RemoveSkillFromAgent(agent, skillId):
  1. Unregister tools
  2. Rimuovi prompt extension
  3. Preserva stato agente
```

## Skill package format (file system)

```
skills/
  research.base/
    manifest.json
    tools/
      WebSearchTool.dll
      KnowledgeSearchTool.dll
    prompts/
      system_prompt.txt
    knowledge/
      index.json
  data.analyst.v1/
    manifest.json
    tools/
      DatabaseQueryTool.dll
      StatisticalAnalysisTool.dll
      ChartTool.dll
    prompts/
      system_prompt.txt
```

## Security skill

- Skill download: checksum SHA256 verificato
- Tool assembly caricato in sandbox (AssemblyLoadContext isolato)
- Permessi minimi: skill dichiara categorie tool necessarie
- Firma: skill firmate ECDsa per verified publisher
- Rate-limit: per-skill, non per-tool

## Skill marketplace (futuro)

```
skills.agxiome.io/v1/
  ├── index.json
  ├── research.web.v2/
  │   ├── manifest.json
  │   ├── tools.dll
  │   ├── tools.dll.sha256
  │   ├── tools.dll.sig (ECDsa)
  │   ├── prompts/
  │   └── knowledge/
  ├── data.analyst.v1/
  └── coding.reviewer.v3/
```
