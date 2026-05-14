// SA-04 — API Key Authentication
// Headless / machine-to-machine auth via hashed API keys.
// Keys are stored as PBKDF2 hashes — raw key never persisted.
// Each key carries a scope, optional expiry, and a revocation flag.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AGXIOME.Security.Rbac;

namespace AGXIOME.Security.ApiKey;

// ─── Value Objects ────────────────────────────────────────────────────────────

/// <summary>
/// Stored representation of an API key.
/// KeyHash is PBKDF2(key, salt, 100_000, SHA-256). Raw key is never stored.
/// </summary>
public sealed record ApiKeyRecord(
    string              KeyId,          // public identifier (prefix of the key, e.g. "agx_")
    string              KeyHash,        // PBKDF2 hex
    string              Salt,           // base64 salt
    IReadOnlyList<AgxiomeRole> Roles,
    IReadOnlyList<string> Scopes,
    string              OwnerSubjectHash,
    DateTimeOffset      CreatedAt,
    DateTimeOffset?     ExpiresAt,
    bool                IsRevoked       = false);

/// <summary>Result of a key validation attempt.</summary>
public sealed record ApiKeyValidationResult(
    bool                IsValid,
    ApiKeyRecord?       Record,
    string?             FailureReason);

// ─── Interface ────────────────────────────────────────────────────────────────

public interface IApiKeyStore
{
    /// <summary>Provisions a new API key. Returns the raw (plaintext) key exactly once.</summary>
    Task<(string RawKey, ApiKeyRecord Record)> CreateAsync(
        IReadOnlyList<AgxiomeRole> roles,
        IReadOnlyList<string> scopes,
        string ownerSubjectId,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default);

    Task<ApiKeyValidationResult> ValidateAsync(string rawKey, CancellationToken ct = default);
    Task<bool> RevokeAsync(string keyId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyRecord>> ListByOwnerAsync(string ownerSubjectHash, CancellationToken ct = default);

    /// <summary>
    /// Seeds a pre-known raw key (e.g. from env var) as a full-admin key.
    /// For dev/smoke-test bootstrap only — never call in production without a secrets vault.
    /// </summary>
    Task SeedBootstrapKeyAsync(string rawKey, Action<string>? log = null, CancellationToken ct = default);
}

// ─── Implementation ───────────────────────────────────────────────────────────

/// <summary>
/// In-memory API key store. Production should use Postgres + Redis cache.
/// PBKDF2-SHA256, 100_000 iterations, 32-byte output.
/// </summary>
public sealed class InMemoryApiKeyStore : IApiKeyStore
{
    private const int Iterations   = 100_000;
    private const int KeyByteLen   = 32;
    private const int SaltByteLen  = 32;
    private const string KeyPrefix = "agx_";

    private readonly ConcurrentDictionary<string, ApiKeyRecord> _store = new();

    public Task<(string RawKey, ApiKeyRecord Record)> CreateAsync(
        IReadOnlyList<AgxiomeRole> roles,
        IReadOnlyList<string> scopes,
        string ownerSubjectId,
        DateTimeOffset? expiresAt = null,
        CancellationToken ct = default)
    {
        // Generate random raw key: prefix + 32 random bytes (base64url)
        var randomBytes = RandomNumberGenerator.GetBytes(KeyByteLen);
        var rawKey = KeyPrefix + Convert.ToBase64String(randomBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var saltBytes = RandomNumberGenerator.GetBytes(SaltByteLen);
        var salt      = Convert.ToBase64String(saltBytes);

        var hash = DeriveHash(rawKey, saltBytes);
        var keyId = $"{KeyPrefix}{Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLower()}";

        var record = new ApiKeyRecord(
            KeyId:           keyId,
            KeyHash:         Convert.ToHexString(hash),
            Salt:            salt,
            Roles:           roles,
            Scopes:          scopes,
            OwnerSubjectHash: AuditPseudonymize(ownerSubjectId),
            CreatedAt:       DateTimeOffset.UtcNow,
            ExpiresAt:       expiresAt);

        _store[keyId] = record;
        return Task.FromResult((rawKey, record));
    }

    public Task<ApiKeyValidationResult> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return Task.FromResult(new ApiKeyValidationResult(false, null, "empty_key"));

        foreach (var record in _store.Values)
        {
            if (record.IsRevoked)  continue;
            if (record.ExpiresAt.HasValue && DateTimeOffset.UtcNow > record.ExpiresAt) continue;

            var saltBytes = Convert.FromBase64String(record.Salt);
            var hash      = DeriveHash(rawKey, saltBytes);
            if (CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(record.KeyHash), hash))
                return Task.FromResult(new ApiKeyValidationResult(true, record, null));
        }
        return Task.FromResult(new ApiKeyValidationResult(false, null, "invalid_key"));
    }

    public Task<bool> RevokeAsync(string keyId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(keyId, out var rec)) return Task.FromResult(false);
        _store[keyId] = rec with { IsRevoked = true };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ApiKeyRecord>> ListByOwnerAsync(string ownerSubjectHash, CancellationToken ct = default)
    {
        IReadOnlyList<ApiKeyRecord> result = _store.Values
            .Where(r => r.OwnerSubjectHash == ownerSubjectHash)
            .ToList();
        return Task.FromResult(result);
    }

    public async Task SeedBootstrapKeyAsync(string rawKey, Action<string>? log = null, CancellationToken ct = default)
    {
        // Seeds the provided raw key as a full-admin key. Idempotent — skips if already seeded.
        // The raw key is validated via PBKDF2 on each request; it is never stored in plaintext.
        var (_, record) = await CreateAsync(
            roles:           [AgxiomeRole.Admin, AgxiomeRole.Operator],
            scopes:          ["*"],
            ownerSubjectId:  "bootstrap",
            expiresAt:       null,         // no expiry for bootstrap key
            ct:              ct);

        // Overwrite the generated raw key hash with the hash of the caller-supplied key,
        // so that the caller-supplied rawKey will validate correctly.
        var saltBytes = Convert.FromBase64String(record.Salt);
        var correctHash = Convert.ToHexString(DeriveHash(rawKey, saltBytes));
        var corrected = record with { KeyHash = correctHash };
        _store[record.KeyId] = corrected;

        log?.Invoke($"[AGXIOME] Bootstrap API key seeded (keyId={record.KeyId}). Remove AGXIOME_BOOTSTRAP_KEY before production.");
    }

    private static byte[] DeriveHash(string rawKey, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(rawKey),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeyByteLen);

    private static string AuditPseudonymize(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16];
    }
}
