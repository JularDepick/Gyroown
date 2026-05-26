namespace Gyroown.Services;

/// <summary>
/// (stub) — (stub) + (stub)
/// (stub) → userKey → (stub) → (stub) / (stub)
/// (stub)
/// </summary>
public interface IEncryptionService
{
    // ── (stub) ──

    /// <summary>(stub) + (stub) userKey（(stub)</summary>
    byte[] DeriveUserKey(string password, byte[] salt);

    // ── (stub) ──

    /// <summary>(stub)</summary>
    (byte[] PrivateKey, byte[] PublicKey) GenerateVaultKeyPair();

    /// <summary>用 userKey (stub) → (stub) auth\vault-key.enc</summary>
    byte[] EncryptVaultKeyPair((byte[] PrivateKey, byte[] PublicKey) keyPair, byte[] userKey);

    /// <summary>用 userKey (stub) auth\vault-key.enc → (stub)</summary>
    (byte[] PrivateKey, byte[] PublicKey) DecryptVaultKeyPair(byte[] encryptedKeyPair, byte[] userKey);

    // ── (stub) ──

    /// <summary>(stub)</summary>
    Task EncryptFileAsync(Stream inStream, Stream outStream, byte[] privateKey,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>(stub)</summary>
    Task DecryptFileAsync(Stream inStream, Stream outStream, byte[] publicKey,
        IProgress<double>? progress = null, CancellationToken ct = default);
}
