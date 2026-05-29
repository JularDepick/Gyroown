namespace Gyroown.Services;

/// <summary>
/// Encryption service interface — key derivation + asymmetric/symmetric hybrid encryption.
/// Flow: password -> PBKDF2 derive userKey -> RSA/AES-GCM encrypt/decrypt -> file encrypt/decrypt.
/// See EncryptionService for implementation details.
/// </summary>
public interface IEncryptionService
{
    // ── Key Derivation ──

    /// <summary>Derive a 32-byte userKey from password + salt using PBKDF2-SHA256 (100K iterations).</summary>
    byte[] DeriveUserKey(string password, byte[] salt);

    // ── Vault Key Pair ──

    /// <summary>Generate an RSA-2048 key pair for encrypting files in the vault.</summary>
    (byte[] PrivateKey, byte[] PublicKey) GenerateVaultKeyPair();

    /// <summary>Encrypt the key pair with userKey via AES-GCM, serialize to auth\vault-key.enc.</summary>
    byte[] EncryptVaultKeyPair((byte[] PrivateKey, byte[] PublicKey) keyPair, byte[] userKey);

    /// <summary>Decrypt auth\vault-key.enc with userKey via AES-GCM, restore the key pair.</summary>
    (byte[] PrivateKey, byte[] PublicKey) DecryptVaultKeyPair(byte[] encryptedKeyPair, byte[] userKey);

    // ── File Encryption / Decryption ──

    /// <summary>Streaming encryption: RSA-OAEP encrypts AES key header + AES-GCM encrypts file content.</summary>
    Task EncryptFileAsync(Stream inStream, Stream outStream, byte[] privateKey,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Streaming decryption: reads hybrid encrypted blob, RSA decrypts header for AES key, then AES-GCM decrypts content.</summary>
    Task DecryptFileAsync(Stream inStream, Stream outStream, byte[] privateKey,
        IProgress<double>? progress = null, CancellationToken ct = default);
}
