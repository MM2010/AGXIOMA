namespace AGXIOME.Shared.Models;

/// <summary>A streaming run output event carrying content, tool call, or HITL requirement data.</summary>
public record RunOutputEvent(
    RunEvent Event,
    string? Content = null,
    ToolCallResult? ToolCall = null,
    HITLRequirement? Requirement = null
);
