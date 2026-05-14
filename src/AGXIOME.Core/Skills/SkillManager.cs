using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Skills;

/// <summary>
/// Skill orchestrator. Manages skill lifecycle: discovery → registration → agent binding.
/// Supports local filesystem skills (Agno/Anthropic SKILL.md format),
/// remote registry (skills.sh style), and programmatic C# skills.
/// Progressive discovery: skills show summaries first, load full content on demand.
/// </summary>
public sealed class SkillManager : ISkillManager
{
    private readonly ConcurrentDictionary<string, IAgxiomeSkill> _skills = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _agentSkills = new();
    private readonly List<ISkillLoader> _loaders = new();
    private readonly ConcurrentDictionary<string, SkillPackage> _packages = new();

    public void AddLoader(ISkillLoader loader) => _loaders.Add(loader);

    // ── Registration ──

    public void RegisterSkill(IAgxiomeSkill skill) => _skills[skill.Id] = skill;

    public void RegisterFromPackage(SkillPackage package)
    {
        _packages[package.Id] = package;
        var skill = new PackageBackedSkill(package);
        _skills[package.Id] = skill;
    }

    public void UnregisterSkill(string skillId)
    {
        _skills.TryRemove(skillId, out _);
        _packages.TryRemove(skillId, out _);
    }

    public IAgxiomeSkill? GetSkill(string skillId) => _skills.GetValueOrDefault(skillId);

    public IReadOnlyList<IAgxiomeSkill> ListSkills(string? category = null)
        => _skills.Values.Where(s => category == null || s.Category == category).ToList();

    // ── Remote registry (skills.sh style) ──

    public async Task<IReadOnlyList<SkillManifest>> ListRemoteAsync(string registryUrl, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        var response = await http.GetAsync($"{registryUrl}/skills/index.json", ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<RemoteSkillIndex>(json);
        return result?.Skills ?? Array.Empty<SkillManifest>();
    }

    public async Task<IAgxiomeSkill> DownloadSkillAsync(string skillId, string registryUrl, CancellationToken ct = default)
    {
        using var http = new HttpClient();

        // 1. Download manifest
        var manifest = JsonSerializer.Deserialize<SkillPackage>(
            await http.GetStringAsync($"{registryUrl}/skills/{skillId}/manifest.json", ct));

        if (manifest == null)
            throw new InvalidOperationException($"Skill '{skillId}' not found in registry '{registryUrl}'");

        // 2. Register
        RegisterFromPackage(manifest with { Id = skillId });

        return _skills[skillId];
    }

    // ── Agent binding ──

    public void ApplySkillToAgent(IAgxiomeAgent agent, string skillId)
    {
        if (!_skills.TryGetValue(skillId, out var skill))
            throw new KeyNotFoundException($"Skill '{skillId}' not registered. Register it first or download it.");

        // 1. Resolve dependencies
        foreach (var depId in skill.RequiredSkills)
            ApplySkillToAgent(agent, depId);

        // 2. Register tools
        foreach (var toolId in skill.ToolIds)
        {
            try
            {
                var toolType = Type.GetType(toolId);
                if (toolType != null && Activator.CreateInstance(toolType) is IAgxiomeTool tool)
                    agent.RegisterTool(tool);
            }
            catch
            {
                // Tool assembly not available — skill may be in browse-only mode
            }
        }

        // 3. Track per-agent
        _agentSkills.AddOrUpdate(agent.Id,
            _ => new HashSet<string> { skillId },
            (_, set) => { set.Add(skillId); return set; });
    }

    public void RemoveSkillFromAgent(IAgxiomeAgent agent, string skillId)
    {
        if (_agentSkills.TryGetValue(agent.Id, out var skills))
            skills.Remove(skillId);
    }

    public IReadOnlyList<string> GetAgentSkillIds(string agentId)
        => _agentSkills.GetValueOrDefault(agentId)?.ToList() ?? new List<string>();

    // ── Helpers ──

    public async Task DiscoverFromLoadersAsync(CancellationToken ct = default)
    {
        foreach (var loader in _loaders)
        {
            var packages = await loader.DiscoverAsync(ct);
            foreach (var pkg in packages)
                RegisterFromPackage(pkg);
        }
    }
}

// ═══════════════════ LOCAL FILESYSTEM LOADER ═══════════════════

/// <summary>
/// Loads skills from a local directory following Agno/Anthropic format:
/// my-skill/
/// ├── SKILL.md           # Instructions with YAML frontmatter
/// ├── scripts/           # Optional executable scripts
/// └── references/        # Optional reference documentation
/// </summary>
public sealed class LocalSkillsLoader : ISkillLoader
{
    public string Name => "Local";
    private readonly string _basePath;

    public LocalSkillsLoader(string basePath) => _basePath = basePath;

    public async Task<IReadOnlyList<SkillPackage>> DiscoverAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_basePath))
            return Array.Empty<SkillPackage>();

        var packages = new List<SkillPackage>();
        foreach (var dir in Directory.GetDirectories(_basePath))
        {
            var skillMd = Path.Combine(dir, "SKILL.md");
            if (!File.Exists(skillMd)) continue;

            var package = await ParseSkillFolderAsync(dir, ct);
            if (package != null) packages.Add(package);
        }
        return packages;
    }

    public async Task<SkillPackage?> LoadAsync(string skillId, CancellationToken ct = default)
    {
        var path = Path.Combine(_basePath, skillId);
        if (!Directory.Exists(path)) return null;
        return await ParseSkillFolderAsync(path, ct);
    }

    private static async Task<SkillPackage?> ParseSkillFolderAsync(string dir, CancellationToken ct)
    {
        var skillMd = Path.Combine(dir, "SKILL.md");
        if (!File.Exists(skillMd)) return null;

        var content = await File.ReadAllTextAsync(skillMd, ct);

        // Parse YAML frontmatter (between --- markers)
        var (frontmatter, description) = ParseFrontmatter(content);
        var scriptsDir = Path.Combine(dir, "scripts");
        var refsDir = Path.Combine(dir, "references");

        return new SkillPackage(
            Id: frontmatter.GetValueOrDefault("id") ?? Path.GetFileName(dir),
            Name: frontmatter.GetValueOrDefault("name") ?? Path.GetFileName(dir),
            Description: description,
            Version: frontmatter.GetValueOrDefault("version") ?? "1.0.0",
            Category: frontmatter.GetValueOrDefault("category"),
            Author: frontmatter.GetValueOrDefault("author"),
            ManifestPath: skillMd,
            ScriptsDir: Directory.Exists(scriptsDir) ? scriptsDir : null,
            ReferencesDir: Directory.Exists(refsDir) ? refsDir : null,
            Tags: (frontmatter.GetValueOrDefault("tags") ?? "").Split(',', StringSplitOptions.TrimEntries),
            RequiredSkills: (frontmatter.GetValueOrDefault("requires") ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        );
    }

    /// <summary>Parse YAML frontmatter from SKILL.md. Public for reuse.</summary>
    public static (Dictionary<string, string> Frontmatter, string Description) ParseFrontmatter(string content)
    {
        var frontmatter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var match = Regex.Match(content, @"^---\s*\n(.*?)\n---\s*\n(.*)", RegexOptions.Singleline);
        if (!match.Success)
            return (frontmatter, content);

        foreach (var line in match.Groups[1].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                frontmatter[parts[0]] = parts[1].Trim(' ', '"');
        }

        return (frontmatter, match.Groups[2].Value.Trim());
    }

    /// <summary>Discover from a skills directory by convention (./skills/).</summary>
    public static LocalSkillsLoader FromConvention()
        => new LocalSkillsLoader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "skills"));
}

// ═══════════════════ REMOTE SKILL INDEX ═══════════════════

internal record RemoteSkillIndex(IReadOnlyList<SkillManifest> Skills);

// ═══════════════════ PACKAGE-BACKED SKILL ═══════════════════

/// <summary>IAgxiomeSkill backed by a SkillPackage on disk.</summary>
internal sealed class PackageBackedSkill : IAgxiomeSkill
{
    private readonly SkillPackage _pkg;
    public string Id => _pkg.Id;
    public string Name => _pkg.Name;
    public string Description => _pkg.Description;
    public string? Version => _pkg.Version;
    public string? Category => _pkg.Category;
    public string? BaseSkillId => null;
    public IReadOnlyList<string> ToolIds => Array.Empty<string>();
    public IReadOnlyList<string> RequiredSkills => _pkg.RequiredSkills;
    public string? KnowledgeBaseId => null;
    public Dictionary<string, object> DefaultConfig => new();

    public IReadOnlyList<SkillScript> Scripts
    {
        get
        {
            if (_pkg.ScriptsDir == null) return Array.Empty<SkillScript>();
            return Directory.GetFiles(_pkg.ScriptsDir)
                .Select(f => new SkillScript(
                    Path.GetFileNameWithoutExtension(f),
                    f,
                    Path.GetExtension(f) switch { ".py" => "python", ".sh" => "shell", ".cs" => "csharp", ".js" => "javascript", _ => "text" }
                )).ToList();
        }
    }

    public IReadOnlyList<SkillDocRef> References
    {
        get
        {
            if (_pkg.ReferencesDir == null) return Array.Empty<SkillDocRef>();
            return Directory.GetFiles(_pkg.ReferencesDir)
                .Select(f => new SkillDocRef(Path.GetFileNameWithoutExtension(f), f))
                .ToList();
        }
    }

    public string? SystemPromptExtension
    {
        get
        {
            if (!File.Exists(_pkg.ManifestPath)) return null;
            var content = File.ReadAllText(_pkg.ManifestPath);
            var (_, desc) = LocalSkillsLoader.ParseFrontmatter(content);
            return desc;
        }
    }

    public PackageBackedSkill(SkillPackage pkg) => _pkg = pkg;
}

// ═══════════════════ IN-MEMORY SKILL BUILDER ═══════════════════

/// <summary>Fluent builder for programmatic C# skills.</summary>
public sealed class AgxiomeSkillBuilder
{
    private readonly string _id;
    private string _name = "";
    private string _description = "";
    private string? _version;
    private string? _category;
    private string? _baseSkillId;
    private readonly List<string> _toolIds = new();
    private readonly List<string> _requiredSkills = new();
    private string? _systemPromptExtension;
    private string? _knowledgeBaseId;
    private readonly Dictionary<string, object> _config = new();
    private readonly List<SkillScript> _scripts = new();
    private readonly List<SkillDocRef> _references = new();

    public AgxiomeSkillBuilder(string id) => _id = id;

    public AgxiomeSkillBuilder WithName(string name) { _name = name; return this; }
    public AgxiomeSkillBuilder WithDescription(string desc) { _description = desc; return this; }
    public AgxiomeSkillBuilder WithVersion(string v) { _version = v; return this; }
    public AgxiomeSkillBuilder WithCategory(string c) { _category = c; return this; }
    public AgxiomeSkillBuilder Extends(string baseId) { _baseSkillId = baseId; return this; }
    public AgxiomeSkillBuilder AddTool<T>() where T : IAgxiomeTool { _toolIds.Add(typeof(T).AssemblyQualifiedName!); return this; }
    public AgxiomeSkillBuilder AddTool(string typeName) { _toolIds.Add(typeName); return this; }
    public AgxiomeSkillBuilder Requires(string skillId) { _requiredSkills.Add(skillId); return this; }
    public AgxiomeSkillBuilder WithPrompt(string prompt) { _systemPromptExtension = prompt; return this; }
    public AgxiomeSkillBuilder WithKnowledgeBase(string kbId) { _knowledgeBaseId = kbId; return this; }
    public AgxiomeSkillBuilder WithConfig(string key, object value) { _config[key] = value; return this; }
    public AgxiomeSkillBuilder AddScript(string name, string path, string lang) { _scripts.Add(new(name, path, lang)); return this; }
    public AgxiomeSkillBuilder AddReference(string name, string path) { _references.Add(new(name, path)); return this; }

    public IAgxiomeSkill Build() => new BuiltSkill(
        _id, _name, _description, _version, _category, _baseSkillId,
        _toolIds, _requiredSkills, _systemPromptExtension, _knowledgeBaseId,
        _config, _scripts, _references
    );
}

internal sealed class BuiltSkill : IAgxiomeSkill
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string? Version { get; }
    public string? Category { get; }
    public string? BaseSkillId { get; }
    public IReadOnlyList<string> ToolIds { get; }
    public IReadOnlyList<string> RequiredSkills { get; }
    public string? SystemPromptExtension { get; }
    public string? KnowledgeBaseId { get; }
    public Dictionary<string, object> DefaultConfig { get; }
    public IReadOnlyList<SkillScript> Scripts { get; }
    public IReadOnlyList<SkillDocRef> References { get; }

    public BuiltSkill(string id, string name, string desc, string? ver, string? cat, string? baseId,
        List<string> tools, List<string> reqs, string? prompt, string? kb,
        Dictionary<string, object> cfg, List<SkillScript> scripts, List<SkillDocRef> refs)
    {
        Id = id; Name = name; Description = desc; Version = ver; Category = cat;
        BaseSkillId = baseId; ToolIds = tools; RequiredSkills = reqs;
        SystemPromptExtension = prompt; KnowledgeBaseId = kb; DefaultConfig = cfg;
        Scripts = scripts; References = refs;
    }
}
