using System.Text.RegularExpressions;
using AGXIOME.Shared.Config;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;

namespace AGXIOME.Core.Storage;

/// <summary>
/// MarkdownMemoryStore — implements IAgentMemory using Markdown files on disk.
/// Each agent interaction is saved as a .md file with YAML frontmatter.
/// Supports full-text grep search and optional KnowledgeBase semantic search.
/// Parity with Swarms: markdown-based memory with RAG.
/// </summary>
public sealed class MarkdownMemoryStore : IAgentMemory
{
    private readonly string _basePath;
    private readonly bool _useKnowledgeBase;
    private readonly IKnowledgeBase? _knowledgeBase;
    private readonly string _filePattern;

    public MarkdownMemoryStore(MarkdownMemoryConfig config, IKnowledgeBase? knowledgeBase = null)
    {
        _basePath = config.BasePath;
        _useKnowledgeBase = config.UseKnowledgeBase;
        _knowledgeBase = knowledgeBase;
        _filePattern = config.FilePattern;
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>Get the base directory for stored memory files.</summary>
    public string BasePath => _basePath;

    public async Task SaveInteractionAsync(AgentInteraction interaction, CancellationToken ct = default)
    {
        var agentDir = Path.Combine(_basePath, Sanitize(interaction.AgentId));
        Directory.CreateDirectory(agentDir);

        var timestamp = interaction.CreatedAt?.ToString("yyyyMMdd-HHmmss") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"{timestamp}_{Sanitize(interaction.Role)}.md";
        var filePath = Path.Combine(agentDir, fileName);

        var content = GenerateMarkdown(interaction);
        await File.WriteAllTextAsync(filePath, content, ct);
    }

    public Task<IReadOnlyList<AgentInteraction>> GetHistoryAsync(string agentId, int limit = 50, DateTime? since = null, CancellationToken ct = default)
    {
        var agentDir = Path.Combine(_basePath, Sanitize(agentId));
        if (!Directory.Exists(agentDir))
            return Task.FromResult<IReadOnlyList<AgentInteraction>>(Array.Empty<AgentInteraction>());

        var files = Directory.GetFiles(agentDir, "*.md")
            .OrderByDescending(f => f)
            .Take(limit)
            .ToList();

        var interactions = new List<AgentInteraction>();
        foreach (var file in files)
        {
            var parsed = ParseMarkdownFile(file);
            if (parsed != null)
                interactions.Add(parsed);
        }

        return Task.FromResult<IReadOnlyList<AgentInteraction>>(interactions);
    }

    public Task ClearAsync(string agentId, CancellationToken ct = default)
    {
        var agentDir = Path.Combine(_basePath, Sanitize(agentId));
        if (Directory.Exists(agentDir))
        {
            foreach (var file in Directory.GetFiles(agentDir, "*.md"))
                File.Delete(file);
        }
        return Task.CompletedTask;
    }

    public Task<T?> GetStateAsync<T>(string key, CancellationToken ct = default)
    {
        var stateFile = Path.Combine(_basePath, "state", $"{Sanitize(key)}.md");
        if (!File.Exists(stateFile))
            return Task.FromResult<T?>(default);

        var content = File.ReadAllText(stateFile);
        try
        {
            var result = System.Text.Json.JsonSerializer.Deserialize<T>(content);
            return Task.FromResult(result);
        }
        catch
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetStateAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var stateDir = Path.Combine(_basePath, "state");
        Directory.CreateDirectory(stateDir);
        var stateFile = Path.Combine(stateDir, $"{Sanitize(key)}.md");
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        File.WriteAllText(stateFile, json);
        return Task.CompletedTask;
    }

    /// <summary>Load all memory files from a directory.</summary>
    public async Task<List<AgentInteraction>> LoadMarkdownAsync(string? directory = null, CancellationToken ct = default)
    {
        var dir = directory ?? _basePath;
        if (!Directory.Exists(dir))
            return new List<AgentInteraction>();

        var files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories);
        var results = new List<AgentInteraction>();
        foreach (var file in files)
        {
            var parsed = ParseMarkdownFile(file);
            if (parsed != null)
                results.Add(parsed);
        }
        return await Task.FromResult(results);
    }

    /// <summary>Save an interaction directly as a markdown file at the given path.</summary>
    public async Task SaveAsMarkdownAsync(string filePath, AgentInteraction interaction, CancellationToken ct = default)
    {
        var content = GenerateMarkdown(interaction);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(filePath, content, ct);
    }

    /// <summary>Full-text search across all markdown memory files using grep.</summary>
    public async Task<List<string>> SearchByGrepAsync(string query, CancellationToken ct = default)
    {
        if (!Directory.Exists(_basePath))
            return new List<string>();

        var results = new List<string>();
        var files = Directory.GetFiles(_basePath, "*.md", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file, ct);
            if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                results.Add(file);
        }
        return results;
    }

    /// <summary>Semantic search via KnowledgeBase if configured.</summary>
    public async Task<IReadOnlyList<KnowledgeChunk>> SearchSemanticAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        if (_knowledgeBase == null || !_useKnowledgeBase)
            return Array.Empty<KnowledgeChunk>();

        return await _knowledgeBase.SearchAsync(query, new KnowledgeSearchOptions { TopK = topK }, ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string GenerateMarkdown(AgentInteraction interaction)
    {
        var agentId = Sanitize(interaction.AgentId);
        var role = interaction.Role;
        var content = interaction.Content ?? "";
        var createdAt = interaction.CreatedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o");
        var toolCalls = interaction.ToolCallsJson ?? "";

        return $@"---
agent: {agentId}
role: {role}
created_at: {createdAt}
tool_calls: {toolCalls}
---

{content}
";
    }

    private static AgentInteraction? ParseMarkdownFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var match = Regex.Match(content, @"^---\s*\n(.+?)\n---\s*\n(.*)", RegexOptions.Singleline);
            if (!match.Success) return null;

            var frontmatter = match.Groups[1].Value;
            var body = match.Groups[2].Value.Trim();

            string agentId = "", role = "";
            string? toolCalls = null;
            DateTime? createdAt = null;

            foreach (var line in frontmatter.Split('\n'))
            {
                if (line.StartsWith("agent: ")) agentId = line["agent: ".Length..].Trim();
                else if (line.StartsWith("role: ")) role = line["role: ".Length..].Trim();
                else if (line.StartsWith("created_at: "))
                {
                    if (DateTime.TryParse(line["created_at: ".Length..].Trim(), out var dt))
                        createdAt = dt;
                }
                else if (line.StartsWith("tool_calls: "))
                    toolCalls = line["tool_calls: ".Length..].Trim();
            }

            return new AgentInteraction(agentId, role, body, toolCalls, createdAt ?? DateTime.UtcNow);
        }
        catch
        {
            return null;
        }
    }

    private static string Sanitize(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(input.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
