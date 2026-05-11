# 07 — Security

## Scopo

Zero Trust Architecture. Ogni messaggio, richiesta e transazione è verificata.
Crittografia end-to-end, audit obbligatorio, abuse control su ogni endpoint.

## Zero Trust principi

1. **Mai fidarsi, sempre verificare** — ogni richiesta autenticata e autorizzata
2. **Least privilege** — ogni agente ha solo i tool necessari
3. **Assume breach** — logging, auditing, alerting su ogni operazione
4. **Verify explicitly** — HMAC/JWE su ogni transazione rilevante

## Crittografia

### Asymmetric key exchange (ECC)

```csharp
public interface IAgxiomeCrypto
{
    // Key generation
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();
    
    // Sign & Verify
    byte[] Sign(byte[] data, byte[] privateKey);
    bool Verify(byte[] data, byte[] signature, byte[] publicKey);
    
    // Encrypt & Decrypt (JWE)
    string EncryptJWE<T>(T payload, byte[] publicKey);
    T DecryptJWE<T>(string jwe, byte[] privateKey);
    
    // HMAC
    string ComputeHMAC(byte[] data, byte[] key);
    bool VerifyHMAC(byte[] data, string hmac, byte[] key);
}
```

| Algoritmo | Uso | 
|-----------|-----|
| ECDsa (P-256) | Firma agent-to-agent, key exchange |
| AES-256-GCM | Payload encryption (symmetric session key) |
| HMAC-SHA256 | Piece verification, message integrity |
| JWE (JSON Web Encryption) | Encrypted envelope per sensitive data |

### JWE usage boundaries

| Cosa | JWE (encrypt) | JWS (signed only) |
|-----|--------------|-------------------|
| API request/response | ❌ | ✅ |
| Agent memory content | ✅ (sensitive) | ❌ |
| Function call parameters | ❌ | ✅ |
| Audit logs | ❌ | ✅ |
| Session tokens | ✅ | ❌ |
| Piece spawn data | ❌ | ✅ (HMAC) |

### Key lifecycle

```
GenerateKeyPair() ──► Store in vault (HSM / Azure Key Vault / env)
       │
       ▼
   [Rotate every 90 days]
       │
       ▼
   Old key: retained 7 days for delayed verification
   New key: active immediately
       │
       ▼
   Key revocation: remove from vault → all verifications fail
```

## Anti-replay

| Misura | Dettaglio |
|--------|-----------|
| `jti` (JWT ID) | Unique per messaggio |
| Nonce | Random 16 byte per richiesta |
| Timestamp window | ±5 secondi |
| Session binding | `sub` claim = agentId |
| Replay cache | Redis set {jti} con TTL 60s |

## Rate limiting & abuse control

```csharp
public class RateLimitPolicy
{
    public string Resource { get; set; } = "";  // "agent:run", "chat:send"
    public int MaxRequests { get; set; } = 100;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public bool BurstAllowed { get; set; } = false;
    public int MaxBurst { get; set; } = 10;
    public RateLimitAction OnExceeded { get; set; } = RateLimitAction.Throttle;
    public string? EscalationEndpoint { get; set; } // Ban dopo N violazioni
}

public enum RateLimitAction
{
    Throttle,       // Ritarda richiesta
    Drop,           // Scarta richiesta
    Block,          // Blocca per duration
    BlockAndAlert   // Blocca + alert operatore
}
```

| Endpoint | Rate limit default |
|----------|-------------------|
| Agent run (REST) | 30/min per agente |
| Agent run (SignalR) | 10/min per connessione |
| Tool execution | 100/min per agente |
| RAG search | 200/min per agente |
| Auth (login) | 5/min per IP |
| Knowledge ingest | 50/min per tenant |

## Audit logging

```csharp
public interface IAuditLogger
{
    Task LogAsync(AuditEvent evt, CancellationToken ct);
    Task<IReadOnlyList<AuditEvent>> QueryAsync(
        AuditQuery query, CancellationToken ct);
}

public record AuditEvent(
    string Id,
    string EventType,    // "agent.run", "tool.call", "auth.login"
    string ActorId,      // agentId, userId, system
    string? TargetId,    // risorsa bersaglio
    string Action,       // "CREATE", "READ", "UPDATE", "DELETE", "EXECUTE"
    object? Payload,
    string? ClientIp,
    bool Success,
    string? FailureReason,
    DateTime Timestamp
);
```

| Evento obbligatorio | Retention |
|--------------------|-----------|
| Auth (login/logout/refresh) | 1 anno |
| Agent session start/end | 90 giorni |
| Tool execution | 90 giorni |
| Knowledge ingest/delete | 90 giorni |
| Workflow execution | 1 anno |
| Rate-limit violation | 30 giorni |
| Crypto key rotation | 2 anni |
| Ban/escalation | 2 anni |

## CORS & CSP

```csharp
// Program.cs
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(config.Cors.AllowedOrigins)
     .WithMethods("GET", "POST", "OPTIONS")
     .WithHeaders("Authorization", "Content-Type", "X-Agxiome-*")
     .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
));

// Middleware CSP
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' wss://*.agxiome.io; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'";
    await next();
});
```

## Security audit CI gates

| Gate | Strumento | Fail criteria |
|------|----------|--------------|
| CSP/CORS regression | Custom test suite | Policy più permissiva |
| Dependency scan | `dotnet list package --vulnerable` | Critical CVE |
| Secrets scan | `trufflehog` / `git-secrets` | Qualsiasi match |
| SAST | `dotnet format --severity error` | Warning analysis |
| License compliance | `dotnet list package --include-transitive` | Copyleft license |

## Threat model checklist (per feature)

Ogni nuova feature che tocca auth, crypto, realtime o third-party richiede:

- [ ] Trust boundary documentato
- [ ] Dati sensibili identificati
- [ ] Encryption in-transit e at-rest
- [ ] AuthN/AuthZ flow
- [ ] Rate-limit specifico
- [ ] Audit eventi definiti
- [ ] Anti-replay coperto
- [ ] Rollback plan
