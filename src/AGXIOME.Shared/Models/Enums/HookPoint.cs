namespace AGXIOME.Shared.Models;

/// <summary>Lifecycle hook points at which agent hooks can be injected.</summary>
public enum HookPoint
{
    PreRun,
    PostRun,
    PreTool,
    PostTool,
    StreamChunk,
    BackgroundPost
}
