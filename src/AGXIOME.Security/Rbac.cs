// SA-01 — Role-Based Access Control
// 4 built-in roles: Admin, Operator, Viewer, Service.
// Policy is declarative: compose via RbacPolicyBuilder, check via IRbacPolicy.
// Zero Trust: every request must be authorized before reaching domain logic.

namespace AGXIOME.Security.Rbac;

// ─── Enums ───────────────────────────────────────────────────────────────────

public enum AgxiomeRole
{
    Admin    = 0,   // full control — create/delete agents, manage keys, config
    Operator = 1,   // run agents, manage sessions, view logs
    Viewer   = 2,   // read-only access to runs, sessions, metrics
    Service  = 3    // machine-to-machine: scoped to a specific agent/toolkit
}

public enum AgxiomePermission
{
    // Agent
    AgentCreate  = 100,
    AgentRead    = 101,
    AgentUpdate  = 102,
    AgentDelete  = 103,
    AgentRun     = 104,
    AgentClone   = 105,
    // Session
    SessionCreate = 200,
    SessionRead   = 201,
    SessionDelete = 202,
    // Team
    TeamCreate  = 300,
    TeamRead    = 301,
    TeamUpdate  = 302,
    TeamDelete  = 303,
    // Workflow
    WorkflowCreate  = 400,
    WorkflowRead    = 401,
    WorkflowExecute = 402,
    WorkflowDelete  = 403,
    // Knowledge
    KnowledgeIngest  = 500,
    KnowledgeSearch  = 501,
    KnowledgeDelete  = 502,
    // Admin
    AdminHealth  = 900,
    AdminMetrics = 901,
    AdminLogs    = 902,
    AdminConfig  = 903,
    AdminUsers   = 904
}

// ─── Interfaces ───────────────────────────────────────────────────────────────

public interface IRbacPolicy
{
    bool IsAuthorized(AgxiomePrincipal principal, AgxiomePermission permission);
    bool HasRole(AgxiomePrincipal principal, AgxiomeRole role);
    IReadOnlyList<AgxiomePermission> GetPermissions(AgxiomeRole role);
}

// ─── Value Objects ────────────────────────────────────────────────────────────

/// <summary>
/// Represents an authenticated identity with one or more roles and optional scopes.
/// Scopes limit Service-role principals to specific agent/toolkit identifiers.
/// </summary>
public sealed record AgxiomePrincipal(
    string SubjectId,           // pseudonymized or hashed user/machine identity
    IReadOnlyList<AgxiomeRole> Roles,
    IReadOnlyList<string> Scopes);  // e.g. ["agent:demo-agent", "toolkit:slack"]

// ─── Default Policy ───────────────────────────────────────────────────────────

/// <summary>
/// Built-in policy mapping roles to permissions.
/// Admin ⊃ Operator ⊃ Viewer; Service has narrowly scoped run/read permissions.
/// </summary>
public sealed class DefaultRbacPolicy : IRbacPolicy
{
    private static readonly IReadOnlyDictionary<AgxiomeRole, IReadOnlyList<AgxiomePermission>> _rolePermissions =
        new Dictionary<AgxiomeRole, IReadOnlyList<AgxiomePermission>>
        {
            [AgxiomeRole.Admin] = new[]
            {
                AgxiomePermission.AgentCreate,  AgxiomePermission.AgentRead,  AgxiomePermission.AgentUpdate,
                AgxiomePermission.AgentDelete,  AgxiomePermission.AgentRun,   AgxiomePermission.AgentClone,
                AgxiomePermission.SessionCreate, AgxiomePermission.SessionRead, AgxiomePermission.SessionDelete,
                AgxiomePermission.TeamCreate,   AgxiomePermission.TeamRead,   AgxiomePermission.TeamUpdate,
                AgxiomePermission.TeamDelete,
                AgxiomePermission.WorkflowCreate, AgxiomePermission.WorkflowRead,
                AgxiomePermission.WorkflowExecute, AgxiomePermission.WorkflowDelete,
                AgxiomePermission.KnowledgeIngest, AgxiomePermission.KnowledgeSearch, AgxiomePermission.KnowledgeDelete,
                AgxiomePermission.AdminHealth, AgxiomePermission.AdminMetrics,
                AgxiomePermission.AdminLogs,   AgxiomePermission.AdminConfig, AgxiomePermission.AdminUsers
            },
            [AgxiomeRole.Operator] = new[]
            {
                AgxiomePermission.AgentRead,    AgxiomePermission.AgentUpdate, AgxiomePermission.AgentRun,
                AgxiomePermission.AgentClone,
                AgxiomePermission.SessionCreate, AgxiomePermission.SessionRead,
                AgxiomePermission.TeamRead,     AgxiomePermission.TeamUpdate,
                AgxiomePermission.WorkflowRead, AgxiomePermission.WorkflowExecute,
                AgxiomePermission.KnowledgeIngest, AgxiomePermission.KnowledgeSearch,
                AgxiomePermission.AdminHealth,  AgxiomePermission.AdminMetrics, AgxiomePermission.AdminLogs
            },
            [AgxiomeRole.Viewer] = new[]
            {
                AgxiomePermission.AgentRead,
                AgxiomePermission.SessionRead,
                AgxiomePermission.TeamRead,
                AgxiomePermission.WorkflowRead,
                AgxiomePermission.KnowledgeSearch,
                AgxiomePermission.AdminHealth,  AgxiomePermission.AdminMetrics
            },
            [AgxiomeRole.Service] = new[]
            {
                AgxiomePermission.AgentRead,    AgxiomePermission.AgentRun,
                AgxiomePermission.SessionCreate, AgxiomePermission.SessionRead,
                AgxiomePermission.KnowledgeSearch,
                AgxiomePermission.AdminHealth
            }
        };

    public bool IsAuthorized(AgxiomePrincipal principal, AgxiomePermission permission)
    {
        foreach (var role in principal.Roles)
        {
            if (_rolePermissions.TryGetValue(role, out var perms) && perms.Contains(permission))
                return true;
        }
        return false;
    }

    public bool HasRole(AgxiomePrincipal principal, AgxiomeRole role) =>
        principal.Roles.Contains(role);

    public IReadOnlyList<AgxiomePermission> GetPermissions(AgxiomeRole role) =>
        _rolePermissions.TryGetValue(role, out var perms) ? perms : Array.Empty<AgxiomePermission>();
}

// ─── Policy Builder ───────────────────────────────────────────────────────────

/// <summary>
/// Fluent builder for custom RBAC policies.
/// Example: new RbacPolicyBuilder().Allow(AgxiomeRole.Viewer, AgxiomePermission.AgentCreate).Build()
/// </summary>
public sealed class RbacPolicyBuilder
{
    private readonly Dictionary<AgxiomeRole, HashSet<AgxiomePermission>> _grants = new();

    public RbacPolicyBuilder Allow(AgxiomeRole role, AgxiomePermission permission)
    {
        if (!_grants.TryGetValue(role, out var set))
        {
            set = [];
            _grants[role] = set;
        }
        set.Add(permission);
        return this;
    }

    public RbacPolicyBuilder AllowAll(AgxiomeRole role, IEnumerable<AgxiomePermission> permissions)
    {
        foreach (var p in permissions) Allow(role, p);
        return this;
    }

    /// <summary>
    /// Starts from DefaultRbacPolicy grants and applies additions on top.
    /// </summary>
    public RbacPolicyBuilder WithDefaults()
    {
        var defaultPolicy = new DefaultRbacPolicy();
        foreach (AgxiomeRole role in Enum.GetValues<AgxiomeRole>())
            AllowAll(role, defaultPolicy.GetPermissions(role));
        return this;
    }

    public IRbacPolicy Build() => new CustomRbacPolicy(
        _grants.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<AgxiomePermission>)kv.Value.ToList()));
}

file sealed class CustomRbacPolicy(IReadOnlyDictionary<AgxiomeRole, IReadOnlyList<AgxiomePermission>> grants)
    : IRbacPolicy
{
    public bool IsAuthorized(AgxiomePrincipal principal, AgxiomePermission permission)
    {
        foreach (var role in principal.Roles)
        {
            if (grants.TryGetValue(role, out var perms) && perms.Contains(permission))
                return true;
        }
        return false;
    }

    public bool HasRole(AgxiomePrincipal principal, AgxiomeRole role) =>
        principal.Roles.Contains(role);

    public IReadOnlyList<AgxiomePermission> GetPermissions(AgxiomeRole role) =>
        grants.TryGetValue(role, out var perms) ? perms : Array.Empty<AgxiomePermission>();
}
