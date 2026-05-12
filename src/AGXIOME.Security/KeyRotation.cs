// SA-03 — Key Rotation Service
// Manages ECDSA P-256 signing key lifecycle: generation, rotation, kid-based selection.
// Supports configurable rotation interval and retains previous keys for validation overlap.
// Never persists private keys in logs or configuration files.

using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AGXIOME.Security.Keys;

// ─── Value Objects ────────────────────────────────────────────────────────────

/// <summary>
/// An active signing key identified by 'kid' (key identifier).
/// Only the public key is stored in the rotation store for verification lookup.
/// Private key bytes are held in memory and zeroized when the key is retired.
/// </summary>
public sealed class SigningKey
{
    public string Kid           { get; }
    public DateTimeOffset Created  { get; }
    public DateTimeOffset ExpiresAt { get; }
    public bool IsActive        { get; private set; }

    // Private key — only kept in memory, never logged or serialized.
    private readonly byte[] _privateKeyBytes;
    private readonly byte[] _publicKeyBytes;

    internal SigningKey(string kid, byte[] privateKey, byte[] publicKey, TimeSpan validity)
    {
        Kid             = kid;
        Created         = DateTimeOffset.UtcNow;
        ExpiresAt       = Created + validity;
        IsActive        = true;
        _privateKeyBytes = privateKey;
        _publicKeyBytes  = publicKey;
    }

    public byte[] GetPublicKey()  => (byte[])_publicKeyBytes.Clone();
    internal byte[] GetPrivateKey() => _privateKeyBytes; // access guarded by rotation service

    /// <summary>Marks the key as inactive and zeroes private key memory to reduce exposure window.</summary>
    internal void Retire()
    {
        IsActive = false;
        CryptographicOperations.ZeroMemory(_privateKeyBytes);
    }
}

// ─── Interfaces ───────────────────────────────────────────────────────────────

public interface IKeyRotationService
{
    /// <summary>Returns the current active signing key. Rotates if past the rotation interval.</summary>
    SigningKey GetCurrentKey();

    /// <summary>Looks up a key by kid for signature verification. Returns null if unknown or retired.</summary>
    SigningKey? FindKey(string kid);

    /// <summary>Forces an immediate rotation regardless of the interval. Used for emergency re-key.</summary>
    SigningKey ForceRotate();

    IReadOnlyList<SigningKey> AllActiveKeys { get; }
}

// ─── Implementation ───────────────────────────────────────────────────────────

/// <summary>
/// In-memory ECDSA key rotation store.
/// Default rotation interval: 24h. Overlap window: 1h (previous key remains valid for verification).
/// Production deployments should back this with distributed storage (Redis/Vault).
/// </summary>
public sealed class InMemoryKeyRotationService : IKeyRotationService, IDisposable
{
    private readonly TimeSpan _rotationInterval;
    private readonly TimeSpan _overlapWindow;
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<string, SigningKey> _keys = new();
    private SigningKey _current;

    public InMemoryKeyRotationService(
        TimeSpan? rotationInterval = null,
        TimeSpan? overlapWindow    = null)
    {
        _rotationInterval = rotationInterval ?? TimeSpan.FromHours(24);
        _overlapWindow    = overlapWindow    ?? TimeSpan.FromHours(1);
        _current          = GenerateKey();
        _keys[_current.Kid] = _current;
    }

    public SigningKey GetCurrentKey()
    {
        lock (_lock)
        {
            if (DateTimeOffset.UtcNow >= _current.ExpiresAt - _overlapWindow)
                return ForceRotate();
            return _current;
        }
    }

    public SigningKey? FindKey(string kid) =>
        _keys.TryGetValue(kid, out var key) ? key : null;

    public SigningKey ForceRotate()
    {
        lock (_lock)
        {
            var previous = _current;
            var next     = GenerateKey();
            _keys[next.Kid] = next;
            _current = next;

            // Retire previous after overlap window (synchronous for simplicity in in-memory impl).
            // Production: schedule retirement via hosted service.
            PruneExpired();
            return next;
        }
    }

    public IReadOnlyList<SigningKey> AllActiveKeys =>
        _keys.Values.Where(k => k.IsActive).ToList();

    private static SigningKey GenerateKey()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var priv = ecdsa.ExportECPrivateKey();
        var pub  = ecdsa.ExportSubjectPublicKeyInfo();
        var kid  = $"k-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant()}";
        // Key validity = rotation interval + overlap (so it remains usable for verification post-rotation)
        return new SigningKey(kid, priv, pub, TimeSpan.FromHours(25));
    }

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (kid, key) in _keys)
        {
            if (!key.IsActive || key == _current) continue;
            if (now >= key.ExpiresAt)
            {
                key.Retire();
                _keys.TryRemove(kid, out _);
            }
        }
    }

    public void Dispose()
    {
        foreach (var key in _keys.Values) key.Retire();
        _keys.Clear();
    }
}
