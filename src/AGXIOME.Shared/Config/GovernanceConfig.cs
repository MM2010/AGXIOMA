namespace AGXIOME.Shared.Config;

/// <summary>Configuration enabling governance features such as HITL, audit hooks, and background evaluations.</summary>
public record GovernanceConfig
{
    public bool EnableHITL { get; init; } = true;
    public bool EnableAuditHooks { get; init; } = true;
    public bool EnableBackgroundEvals { get; init; } = false;
}
