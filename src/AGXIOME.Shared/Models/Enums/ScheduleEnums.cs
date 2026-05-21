namespace AGXIOME.Shared.Models;

/// <summary>Lifecycle status of a recurring schedule definition.</summary>
public enum ScheduleStatus { Active, Paused, Completed, Failed }

/// <summary>Execution status of a single schedule run instance.</summary>
public enum ScheduleRunStatus { Running, Completed, Failed, Cancelled }
