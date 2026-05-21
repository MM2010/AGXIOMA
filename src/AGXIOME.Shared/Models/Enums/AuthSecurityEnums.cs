namespace AGXIOME.Shared.Models;

// ─────────────────────────────────────────────────────────────────────────────
// Auth / Security types
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Authentication scheme used to secure the AGXIOME API surface.</summary>
public enum AuthSchemeType
{
    Jwt = 0,
    ApiKey = 1,
    OAuth2 = 2,
    JweEncrypted = 3,
}

/// <summary>RBAC role assigned to a principal for access-control decisions.</summary>
public enum RbacRole
{
    Admin = 0,
    Operator = 1,
    Developer = 2,
    ReadOnly = 3,
}

/// <summary>RBAC action that a principal may be granted or denied on a resource.</summary>
public enum RbacAction
{
    Read = 0,
    Write = 1,
    Execute = 2,
    Delete = 3,
    Admin = 4,
}

/// <summary>RBAC resource type that policies are applied to.</summary>
public enum RbacResource
{
    Agent = 0,
    Session = 1,
    Team = 2,
    Workflow = 3,
    Knowledge = 4,
    Tool = 5,
    Metrics = 6,
    Config = 7,
}
