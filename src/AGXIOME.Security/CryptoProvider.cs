using System.Security.Cryptography;
using AGXIOME.Shared.Interfaces;
using AGXIOME.Shared.Models;
using System.Collections.Concurrent;

namespace AGXIOME.Security;

public sealed class CryptoProvider : IAgxiomeCrypto
{
    private static readonly ECCurve Curva = ECCurve.NamedCurves.nistP256;

    public (byte[], byte[]) GenerateKeyPair()
    {
        using var ecdsa = ECDsa.Create(Curva);
        return (ecdsa.ExportECPrivateKey(), ecdsa.ExportSubjectPublicKeyInfo());
    }

    public byte[] Sign(byte[] data, byte[] privateKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(privateKey, out _);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256);
    }

    public bool Verify(byte[] data, byte[] signature, byte[] publicKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
        return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }

    public string ComputeHMAC(byte[] data, byte[] key)
        => Convert.ToHexString(HMACSHA256.HashData(key, data));

    public bool VerifyHMAC(byte[] data, string hmac, byte[] key)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(hmac),
            HMACSHA256.HashData(key, data));
}

public sealed class AuditLogger : IAuditLogger
{
    private readonly List<AuditEvent> _events = new();

    public Task LogAsync(AuditEvent evt, CancellationToken ct = default)
    {
        _events.Add(evt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var q = _events.AsEnumerable();
        if (query.EventType != null) q = q.Where(e => e.EventType == query.EventType);
        if (query.ActorId != null) q = q.Where(e => e.ActorId == query.ActorId);
        if (query.From.HasValue) q = q.Where(e => e.Timestamp >= query.From.Value);
        if (query.To.HasValue) q = q.Where(e => e.Timestamp <= query.To.Value);
        return Task.FromResult<IReadOnlyList<AuditEvent>>(q.TakeLast(query.Limit).ToList());
    }
}
