using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

/// <summary>Root configuration record for an agent instance, defining its identity, LLM, persistence, and behaviour.</summary>
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
