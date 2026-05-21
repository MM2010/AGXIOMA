namespace AGXIOME.Shared.Models;

/// <summary>Event types emitted by an agent run for streaming consumers.</summary>
public enum RunEvent { RunStart, RunContent, ToolCallStart, ToolCallEnd, RunPaused, RunCompleted, RunError }

/// <summary>Type of step within a workflow definition.</summary>
public enum StepType { Agent, Function, Condition, Parallel, Loop, Router, Human, Delay }

/// <summary>Execution status of a workflow step.</summary>
public enum StepStatus { Pending, Running, Completed, Skipped, Failed }
