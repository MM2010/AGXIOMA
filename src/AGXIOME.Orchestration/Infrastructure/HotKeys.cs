namespace AGXIOME.Orchestration.Infrastructure;

/// <summary>
/// Centralised key-name factory for the hot store.
/// All key patterns are defined here — change prefix once, applies everywhere.
/// </summary>
internal static class HotKeys
{
    private const string Prefix = "agxiome:orch";

    // ── Plan keys ─────────────────────────────────────────────────────────
    public static string PlanMeta(Guid planId)   => $"{Prefix}:plan:{planId}:meta";
    public static string PlanStep(Guid planId)   => $"{Prefix}:plan:{planId}:step";
    public static string PlanStatus(Guid planId) => $"{Prefix}:plan:{planId}:status";
    public static string PlanTasks(Guid planId)  => $"{Prefix}:plan:{planId}:tasks";

    // ── Task keys ─────────────────────────────────────────────────────────
    public static string TaskSpec(Guid taskId)   => $"{Prefix}:task:{taskId}:spec";
    public static string TaskResult(Guid taskId) => $"{Prefix}:task:{taskId}:result";
    public static string TaskStatus(Guid taskId) => $"{Prefix}:task:{taskId}:status";
    public static string TaskJobId(Guid taskId)  => $"{Prefix}:task:{taskId}:jobid";

    // ── Session keys ──────────────────────────────────────────────────────
    public static string SessionPlans(string sessionId) => $"{Prefix}:session:{sessionId}:plans";

    // ── Pub/sub channels ──────────────────────────────────────────────────
    public static string NotifyChannel(Guid planId) => $"{Prefix}:notify:{planId}";
}
