# P20 — Security

## File: `Security/CryptoProvider.cs`, `Security/AuditLogger.cs`

Namespace: `AGXIOME.Security`

## Classi

### `IAgxiomeCrypto`

```csharp
public interface IAgxiomeCrypto
{
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();
    byte[] Sign(byte[] data, byte[] privateKey);
    bool Verify(byte[] data, byte[] signature, byte[] publicKey);
    string EncryptJWE<T>(T payload, byte[] publicKey);
    T DecryptJWE<T>(string jwe, byte[] privateKey);
    string ComputeHMAC(byte[] data, byte[] key);
    bool VerifyHMAC(byte[] data, string hmac, byte[] key);
}

public sealed class CryptoProvider : IAgxiomeCrypto
{
    private static readonly ECCurve Curva = ECCurve.NamedCurves.nistP256;

    public (byte[], byte[]) GenerateKeyPair()
    {
        using var ecdsa = ECDsa.Create(Curva);
        var priv = ecdsa.ExportECPrivateKey();
        var pub = ecdsa.ExportSubjectPublicKeyInfo();
        return (priv, pub);
    }

    public byte[] Sign(byte[] data, byte[] privateKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(privateKey, out _);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    // HMAC-SHA256
    public string ComputeHMAC(byte[] data, byte[] key)
        => HMACSHA256.HashData(key, data).ToHexString();
}
```

### `AuditLogger`

```csharp
public sealed class AuditLogger : IAuditLogger
{
    private readonly ICommandStore _store;

    public async Task LogAsync(AuditEvent evt, CancellationToken ct)
    {
        // INSERT INTO audit_events
        // Retention: 90-365 giorni per tipo
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct)
    {
        // SELECT con filtri per tipo/actor/intervallo
    }
}

public record AuditEvent(string Id, string EventType, string ActorId, string? TargetId,
    string Action, object? Payload, string? ClientIp, bool Success, string? FailureReason, DateTime Timestamp);

public record AuditQuery(string? EventType = null, string? ActorId = null,
    DateTime? From = null, DateTime? To = null, int Limit = 100);
```

### Rate limiter

```csharp
public sealed class RateLimiter
{
    private readonly RedisCache _cache;

    // Sliding window counter
    public async Task<bool> IsAllowedAsync(string key, int maxRequests, TimeSpan window)
    {
        var count = await _cache.IncrementAsync($"ratelimit:{key}", window);
        return count <= maxRequests;
    }
}
```

### JWT Middleware

```csharp
public sealed class JwtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _issuer;
    private readonly string _audience;

    public async Task InvokeAsync(HttpContext context)
    {
        var token = context.Request.Headers["Authorization"]
            .FirstOrDefault()?.Replace("Bearer ", "");

        if (token != null)
        {
            var principal = ValidateToken(token);
            context.User = principal;
        }

        await _next(context);
    }
}
```

## Test

| Test | Descrizione |
|------|------------|
| Sign + Verify | Verifica passa con chiave corretta |
| HMAC compute + verify | Stesso risultato |
| JWE roundtrip | Decrypt produce payload originale |
| Audit log creato | DB record |
| Rate limiter blocca | Dopo N richieste, bloccato |
