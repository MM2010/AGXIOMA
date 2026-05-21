using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

/// <summary>Storage backend configuration for an agent or team, including per-provider connection strings.</summary>
public record StorageConfig
{
    /// <summary>Typed default storage provider.</summary>
    public StorageProviderType DefaultProvider { get; init; } = StorageProviderType.PostgreSQL;
    public Dictionary<string, ProviderConfig> Providers { get; init; } = new();
    public MemoryBackend MemoryBackend { get; init; } = MemoryBackend.InMemory;
    public MarkdownMemoryConfig? MarkdownMemory { get; init; }
}

/// <summary>Connection and pool settings for a named storage provider instance.</summary>
public record ProviderConfig
{
    public string ConnectionString { get; init; } = "";
    public int MaxPoolSize { get; init; } = 100;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public bool EnableLogging { get; init; } = false;
}

/// <summary>Configuration for the markdown-file-based memory backend.</summary>
public record MarkdownMemoryConfig
{
    public string BasePath { get; init; } = "./memory";
    public bool UseKnowledgeBase { get; init; } = false;
    public string FilePattern { get; init; } = "*.md";
}
