namespace AGXIOME.Core.Marketplace;

/// <summary>Local agent marketplace — loads agents from directories, JSON/YAML configs, and markdown files.</summary>
public sealed class AgentMarketplace
{
    private readonly string _registryPath;
    private readonly Dictionary<string, MarketplaceEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public AgentMarketplace(string registryPath)
    {
        _registryPath = registryPath ?? throw new ArgumentNullException(nameof(registryPath));
        if (!Directory.Exists(_registryPath))
            Directory.CreateDirectory(_registryPath);
    }

    public IReadOnlyDictionary<string, MarketplaceEntry> Entries => _entries;

    /// <summary>Load all entries from the registry directory.</summary>
    public void LoadRegistry()
    {
        _entries.Clear();

        // Load JSON files
        foreach (var file in Directory.GetFiles(_registryPath, "*.json"))
            LoadJsonEntry(file);

        // Load YAML files
        foreach (var file in Directory.GetFiles(_registryPath, "*.yaml"))
            LoadYamlEntry(file);

        // Load markdown files
        foreach (var file in Directory.GetFiles(_registryPath, "*.md"))
            LoadMarkdownEntry(file);
    }

    /// <summary>Register an entry programmatically.</summary>
    public void Register(MarketplaceEntry entry)
    {
        _entries[entry.Name] = entry;
    }

    /// <summary>Find an entry by name.</summary>
    public MarketplaceEntry? Find(string name)
    {
        return _entries.TryGetValue(name, out var entry) ? entry : null;
    }

    /// <summary>Search entries by keyword.</summary>
    public List<MarketplaceEntry> Search(string keyword)
    {
        var lower = keyword.ToLowerInvariant();
        return _entries.Values
            .Where(e => e.Name.Contains(lower, StringComparison.OrdinalIgnoreCase)
                     || (e.Description?.Contains(lower, StringComparison.OrdinalIgnoreCase) ?? false)
                     || (e.Tags?.Any(t => t.Contains(lower, StringComparison.OrdinalIgnoreCase)) ?? false))
            .ToList();
    }

    private void LoadJsonEntry(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var entry = System.Text.Json.JsonSerializer.Deserialize<MarketplaceEntry>(json);
            if (entry != null && !string.IsNullOrEmpty(entry.Name))
                _entries[entry.Name] = entry;
        }
        catch { /* skip invalid files */ }
    }

    private void LoadYamlEntry(string filePath)
    {
        // YAML support can be added with a YAML library
        // For now, skip YAML files
    }

    private void LoadMarkdownEntry(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            _entries[name] = new MarketplaceEntry(name, "markdown", content, "Markdown prompt");
        }
        catch { /* skip invalid files */ }
    }
}

/// <summary>Single entry in the marketplace registry.</summary>
public record MarketplaceEntry(
    string Name,
    string Type,       // "agent", "tool", "prompt", "skill", "markdown"
    string? Content = null,
    string? Description = null,
    string[]? Tags = null,
    string? Version = null,
    string? Author = null
);

/// <summary>Registry helper — loads marketplace entries from a directory.</summary>
public static class MarketplaceRegistry
{
    public static AgentMarketplace LoadFromDirectory(string path)
    {
        var marketplace = new AgentMarketplace(path);
        marketplace.LoadRegistry();
        return marketplace;
    }
}

/// <summary>Load prompts from JSON, YAML, or markdown files.</summary>
public static class PromptLoader
{
    /// <summary>Load a prompt from a file.</summary>
    public static string LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Prompt file not found", filePath);

        return File.ReadAllText(filePath);
    }

    /// <summary>Load a prompt from a JSON file with a specific key.</summary>
    public static string? LoadFromJson(string filePath, string key = "prompt")
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(filePath));
            if (doc.RootElement.TryGetProperty(key, out var prop))
                return prop.GetString();
        }
        catch { /* ignore parse errors */ }
        return null;
    }
}
