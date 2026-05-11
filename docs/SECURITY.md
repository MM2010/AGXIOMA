# Security — AGXIOME

Crittografia, HMAC, JWT, audit logging.

---

## Crypto (ECDsa P-256)

```csharp
var crypto = new CryptoProvider();

// Genera chiavi
var (privateKey, publicKey) = crypto.GenerateKeyPair();

// Firma
var data = "messaggio importante"u8.ToArray();
var signature = crypto.Sign(data, privateKey);

// Verifica
var isValid = crypto.Verify(data, signature, publicKey); // true

// Con chiave sbagliata
var (wrongPriv, _) = crypto.GenerateKeyPair();
var wrongSig = crypto.Sign(data, wrongPriv);
var isValid2 = crypto.Verify(data, wrongSig, publicKey); // false
```

## HMAC

```csharp
var key = "secret-key"u8.ToArray();
var data = "payload"u8.ToArray();

var hmac = crypto.ComputeHMAC(data, key);      // hex string
var match = crypto.VerifyHMAC(data, hmac, key); // true
```

## Audit Logger

```csharp
var audit = new AuditLogger();

await audit.LogAsync(new AuditEvent(
    Id: "evt-1",
    EventType: "agent.run",
    ActorId: "agent-42",
    TargetId: null,
    Action: "EXECUTE",
    Payload: new { prompt = "analyze data" },
    ClientIp: "192.168.1.1",
    Success: true,
    FailureReason: null,
    Timestamp: DateTime.UtcNow
));

var results = await audit.QueryAsync(new AuditQuery(
    EventType: "agent.run",
    From: DateTime.UtcNow.AddDays(-1)
));
```

## JWT Middleware

```csharp
app.UseMiddleware<JwtMiddleware>();

// Il middleware estrae il token Bearer, valida JWT, imposta HttpContext.User
```

## Best practice

1. **API key mai hardcoded** — sempre da environment variables
2. **Stack trace mai esposto** — solo `UserMessage` via `AgxiomeException`
3. **Tutto auditato** — ogni operazione critica loggata
4. **Retry limitato** — max 3 tentativi con backoff esponenziale
5. **Rate limiting** — 429 classificato e gestito automaticamente
6. **Checksum** — ogni payload X-Link verificato con HMAC-SHA256
