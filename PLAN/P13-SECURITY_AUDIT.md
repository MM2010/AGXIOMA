# P13 — Security & Audit

> **Priority**: 🟡 P1 | **Dipende da**: P09 (Core) | **Usato da**: P08 (Runtime)
> **Target**: RBAC, audit trail, key rotation, CSP hardening

---

## Task

| # | Task | Descrizione | Effort |
|---|------|-------------|--------|
| SA-01 | **RBAC** | Role-based access control (admin/user/viewer) | 2gg |
| SA-02 | **Audit Trail** | Log ogni azione critica con chi/cosa/quando | 1gg |
| SA-03 | **Key Rotation** | Rotazione automatica chiavi JWT/ECDsa | 1gg |
| SA-04 | **API Key Auth** | Autenticazione via API key per headless | 1gg |
| SA-05 | **Rate Limiting** | Per endpoint, per utente, per IP | 1gg |
| SA-06 | **Input Sanitization** | Validazione input crossing .NET↔JS | 1gg |
| SA-07 | **CSP Hardening** | Content-Security-Policy stretta | 0.5gg |
| SA-08 | **Secrets Scanner** | Blocca credenziali in log/output | 1gg |

### Pattern: RBAC

```csharp
public enum Role { Admin, Developer, Operator, Viewer }

public class RbacMiddleware
{
    private static readonly Dictionary<string, Role[]> EndpointRoles = new()
    {
        ["POST:/api/v1/agents"] = new[] { Role.Admin, Role.Developer },
        ["DELETE:/api/v1/agents/{id}"] = new[] { Role.Admin },
        ["GET:/api/v1/agents"] = new[] { Role.Admin, Role.Developer, Role.Operator, Role.Viewer },
    };
}
```

### Pattern: Audit Trail

```csharp
public record AuditEntry
{
    public string Action { get; init; }    // "agent.run"
    public string ActorId { get; init; }   // user/wallet hash
    public string ResourceId { get; init; } // agent_123
    public string? Details { get; init; }  // pseudonimized
    public DateTime Timestamp { get; init; }
    public AuditOutcome Outcome { get; init; }
    public string CorrelationId { get; init; }
}
```

**Totale task: 8 | File: ~12 | Test: ~50**
