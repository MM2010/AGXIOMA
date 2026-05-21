using AGXIOME.Shared.Models;

namespace AGXIOME.Shared.Config;

/// <summary>Configuration controlling which memory types are actively learned and when.</summary>
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
