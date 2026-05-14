using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

public record AgentConfig
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string? Name { get; init; }
    public string? Role { get; set; }
    public string? SystemPrompt { get; init; }
    public int MaxIterations { get; init; } = 20;
    public MaxIterationsMode MaxIterationsMode { get; init; } = MaxIterationsMode.Fixed;
    public AutoStopCriteria? AutoStopCriteria { get; init; }
    public int MaxHistorySize { get; init; } = 50;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public LLMWIKYConfig ReasoningEngine { get; init; } = new();
    public StorageConfig Persistence { get; init; } = new();
    public bool EnableStreaming { get; init; } = false;
    public bool EnableRetry { get; init; } = true;
    public List<ToolRegistration> Tools { get; init; } = new();
    public List<SkillRef> Skills { get; init; } = new();
    public string? AgentTemplate { get; init; }
}

public record LLMWIKYConfig
{
    /// <summary>Typed provider — avoids hardcoded strings in routing.
    /// Serialize as enum name string for config files; compare by value in code.</summary>
    public LLMProviderType Provider { get; init; } = LLMProviderType.OpenAI;
    public string Model { get; init; } = "gpt-4o";
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? ApiVersion { get; init; }
    public string? OrganizationId { get; init; }
    public double DefaultTemperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 4096;
    public int EmbeddingDimensions { get; init; } = 1536;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public Dictionary<string, object> ProviderOptions { get; init; } = new();
    public bool EnableAdaptiveRateLimit { get; init; } = true;
    public int MaxTokensPerMinute { get; init; } = 100_000;
    public int MaxRequestsPerMinute { get; init; } = 500;
    public List<LLMWIKYConfig>? FallbackProviders { get; init; }
    public bool EnableCostTracking { get; init; } = true;
}

public record MarkdownMemoryConfig
{
    public string BasePath { get; init; } = "./memory";
    public bool UseKnowledgeBase { get; init; } = false;
    public string FilePattern { get; init; } = "*.md";
}

public record StorageConfig
{
    /// <summary>Typed default storage provider.</summary>
    public StorageProviderType DefaultProvider { get; init; } = StorageProviderType.PostgreSQL;
    public Dictionary<string, ProviderConfig> Providers { get; init; } = new();
    public MemoryBackend MemoryBackend { get; init; } = MemoryBackend.InMemory;
    public MarkdownMemoryConfig? MarkdownMemory { get; init; }
}

public record ProviderConfig
{
    public string ConnectionString { get; init; } = "";
    public int MaxPoolSize { get; init; } = 100;
    public int CommandTimeoutSeconds { get; init; } = 30;
    public bool EnableLogging { get; init; } = false;
}

public record TeamConfig
{
    public string Name { get; init; }
    public TeamMode Mode { get; init; } = TeamMode.Coordinate;
    public int MaxIterations { get; init; } = 20;
    public TimeSpan AgentTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan GlobalTimeout { get; init; } = TimeSpan.FromSeconds(120);
    public bool ContinueOnPartialFailure { get; init; } = true;
    public int MaxRetriesPerAgent { get; init; } = 3;
    public bool CacheCallables { get; init; } = true;
}

public record LearningConfig
{
    public LearningMode UserMemoryMode { get; init; } = LearningMode.Always;
    public LearningMode UserProfileMode { get; init; } = LearningMode.Always;
    public LearningMode EntityMemoryMode { get; init; } = LearningMode.Agentic;
    public LearningMode SessionContextMode { get; init; } = LearningMode.Always;
    public LearningMode LearnedKnowledgeMode { get; init; } = LearningMode.Agentic;
    public LearningMode DecisionLogMode { get; init; } = LearningMode.Always;
    public bool EnableOptimization { get; init; } = true;
    public int MaxMemoriesBeforeOptimization { get; init; } = 50;
}

public record GovernanceConfig
{
    public bool EnableHITL { get; init; } = true;
    public bool EnableAuditHooks { get; init; } = true;
    public bool EnableBackgroundEvals { get; init; } = false;
}
