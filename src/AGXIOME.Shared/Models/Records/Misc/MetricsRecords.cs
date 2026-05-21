namespace AGXIOME.Shared.Models;

/// <summary>Aggregated token and timing metrics for a single agent run.</summary>
public record RunMetrics(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    TimeSpan Duration,
    int ToolCallCount,
    Dictionary<string, ModelMetrics> PerModel
);

/// <summary>Per-model metrics breakdown within a run, including cost estimation.</summary>
public record ModelMetrics(
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    TimeSpan InferenceTime,
    decimal EstimatedCost
);

/// <summary>Aggregated metrics across all runs in a session.</summary>
public record SessionMetrics(
    int TotalRuns,
    int TotalToolCalls,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalEstimatedCost,
    TimeSpan TotalDuration
);
