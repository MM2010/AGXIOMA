/// <summary>Defines the possible states of a workflow: Draft, Running, Paused, Completed, Failed.</summary>
namespace AGXIOME.Shared.Models;

public enum WorkflowStatus
{
    Created,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
