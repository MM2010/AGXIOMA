namespace AGXIOME.Shared.Models;

/// <summary>Agent execution lifecycle status.</summary>
public enum AgentStatus
{
    Idle,
    Thinking,
    ToolCall,
    Streaming,
    Paused,     // HITL pause
    Error,
    Completed
}
