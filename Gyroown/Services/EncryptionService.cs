using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gyroown.Services;

public class EncryptionService : IEncryptionService
{
    private const int AesKeySize = 32, AesNonceSize = 12, AesTagSize = 16, RsaKeySize = 2048;
    private const int Pbkdf2Iterations = 100_000, UserKeySize = 32;

    public byte[] DeriveUserKey(string p, byte[] s) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(p), s, Pbkdf2Iterations, HashAlgorithmName.SHA256, UserKeySize);

    public (byte[] PrivateKey, byte[] PublicKey) GenerateVaultKeyPair()
    { using var r = RSA.Create(RsaKeySize); return (r.ExportRSAPrivateKey(), r.ExportRSAPublicKey()); }

    public byte[] EncryptVaultKeyPair((byte[] PrivateKey, byte[] PublicKey) kp, byte[] uk)
    { var p = new byte[8 + kp.PrivateKey.Length + kp.PublicKey.Length]; BitConverter.GetBytes(kp.PrivateKey.Length).CopyTo(p, 0); kp.PrivateKey.CopyTo(p, 4); BitConverter.GetBytes(kp.PublicKey.Length).CopyTo(p, 4 + kp.PrivateKey.Length); kp.PublicKey.CopyTo(p, 8 + kp.PrivateKey.Length); return AesEnc(p, uk); }

    public (byte[] PrivateKey, byte[] PublicKey) DecryptVaultKeyPair(byte[] e, byte[] uk)
    { var p = AesDec(e, uk); var pl = BitConverter.ToInt32(p, 0); var pr = new byte[pl]; Array.Copy(p, 4, pr, 0, pl); var pul = BitConverter.ToInt32(p, 4 + pl); var pu = new byte[pul]; Array.Copy(p, 8 + pl, pu, 0, pul); return (pr, pu); }

    // ── Unified blob encryption (data files + meta files both use the vault key pair) ──
    // Format: [4B headerLen][N B RSA-OAEP encrypted header][4B bodyLen][M B AES-GCM ciphertext]
    // Header = JSON { aesKey, aesNonce, originalLength }

    public byte[] EncryptBlob(byte[] plainData, byte[] privateKey)
    {
        using var rsa = RSA.Create(RsaKeySize); rsa.ImportRSAPrivateKey(privateKey, out _);
        var aesKey = RandomNumberGenerator.GetBytes(AesKeySize);
        var aesNonce = RandomNumberGenerator.GetBytes(AesNonceSize);
        // AES-GCM encrypt
        var cipher = new byte[plainData.Length + AesTagSize];
        using var aes = new AesGcm(aesKey, AesTagSize);
        aes.Encrypt(aesNonce, plainData, cipher.AsSpan(0, plainData.Length), cipher.AsSpan(plainData.Length, AesTagSize));
        // RSA encrypt header
        var hdr = new BlobHeader { AesKey = Convert.ToBase64String(aesKey), AesNonce = Convert.ToBase64String(aesNonce), OriginalLength = plainData.Length };
        var hdrJson = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(hdr, JsonConfig.Options));
        var hdrEnc = rsa.Encrypt(hdrJson, RSAEncryptionPadding.OaepSHA256);
        // Write: [4B headerLen][header][4B bodyLen][body]
        var result = new byte[4 + hdrEnc.Length + 4 + cipher.Length];
        BitConverter.GetBytes(hdrEnc.Length).CopyTo(result, 0);
        hdrEnc.CopyTo(result, 4);
        BitConverter.GetBytes(cipher.Length).CopyTo(result, 4 + hdrEnc.Length);
        cipher.CopyTo(result, 8 + hdrEnc.Length);
        return result;
    }

    public byte[] DecryptBlob(byte[] encryptedBlob, byte[] privateKey)
    {
        if (encryptedBlob.Length < 8)
            throw new CryptographicException("Corrupted encrypted blob");

        using var rsa = RSA.Create(RsaKeySize); rsa.ImportRSAPrivateKey(privateKey, out _);
        var hdrLen = BitConverter.ToInt32(encryptedBlob, 0);
        if (hdrLen < 0 || hdrLen > encryptedBlob.Length - 8)
            throw new CryptographicException("Corrupted encrypted blob");
        var hdrEnc = encryptedBlob[4..(4 + hdrLen)];
        var bodyLen = BitConverter.ToInt32(encryptedBlob, 4 + hdrLen);
        if (bodyLen < 0 || bodyLen > encryptedBlob.Length - 8 - hdrLen)
            throw new CryptographicException("Corrupted encrypted blob");
        var cipher = encryptedBlob[(8 + hdrLen)..(8 + hdrLen + bodyLen)];
        // RSA decrypt header
        var hdrJson = Encoding.UTF8.GetString(rsa.Decrypt(hdrEnc, RSAEncryptionPadding.OaepSHA256));
        var hdr = JsonSerializer.Deserialize<BlobHeader>(hdrJson, JsonConfig.Options)
            ?? throw new CryptographicException("Corrupted encrypted blob: invalid header JSON");
        var aesKey = Convert.FromBase64String(hdr.AesKey);
        var aesNonce = Convert.FromBase64String(hdr.AesNonce);
        // AES-GCM decrypt
        var plain = new byte[cipher.Length - AesTagSize];
        using var aes = new AesGcm(aesKey, AesTagSize);
        aes.Decrypt(aesNonce, cipher.AsSpan(0, plain.Length), cipher.AsSpan(plain.Length, AesTagSize), plain);
        return plain;
    }

    // ── Stream-based interface (keeps public API compatible) ──
    public async Task EncryptFileAsync(Stream inStream, Stream outStream, byte[] privateKey,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(); await inStream.CopyToAsync(ms, ct);
        var blob = EncryptBlob(ms.ToArray(), privateKey);
        await outStream.WriteAsync(blob, ct); await outStream.FlushAsync(ct);
        progress?.Report(1.0);
    }

    public async Task DecryptFileAsync(Stream inStream, Stream outStream, byte[] privateKey,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var ms = new MemoryStream(); await inStream.CopyToAsync(ms, ct);
        var plain = DecryptBlob(ms.ToArray(), privateKey);
        await outStream.WriteAsync(plain, ct); await outStream.FlushAsync(ct);
        progress?.Report(1.0);
    }

    // ── Insurance ──

    public (byte[] PublicKey, byte[] PrivateKey) GenerateInsuranceKeyPair()
    { using var r = RSA.Create(RsaKeySize); return (r.ExportRSAPublicKey(), r.ExportRSAPrivateKey()); }

    public byte[] EncryptVaultKeyForInsurance((byte[] PrivateKey, byte[] PublicKey) coreKey, byte[] insPub)
    {
        using var rsa = RSA.Create(RsaKeySize); rsa.ImportRSAPublicKey(insPub, out _);
        var p = new byte[8 + coreKey.PrivateKey.Length + coreKey.PublicKey.Length];
        BitConverter.GetBytes(coreKey.PrivateKey.Length).CopyTo(p, 0);
        coreKey.PrivateKey.CopyTo(p, 4);
        BitConverter.GetBytes(coreKey.PublicKey.Length).CopyTo(p, 4 + coreKey.PrivateKey.Length);
        coreKey.PublicKey.CopyTo(p, 8 + coreKey.PrivateKey.Length);
        return rsa.Encrypt(p, RSAEncryptionPadding.OaepSHA256);
    }

    public (byte[] PrivateKey, byte[] PublicKey) DecryptVaultKeyFromInsurance(byte[] enc, byte[] insPriv)
    {
        using var rsa = RSA.Create(RsaKeySize); rsa.ImportRSAPrivateKey(insPriv, out _);
        var p = rsa.Decrypt(enc, RSAEncryptionPadding.OaepSHA256);
        var pl = BitConverter.ToInt32(p, 0); var pr = new byte[pl]; Array.Copy(p, 4, pr, 0, pl);
        var pul = BitConverter.ToInt32(p, 4 + pl); var pu = new byte[pul]; Array.Copy(p, 8 + pl, pu, 0, pul);
        return (pr, pu);
    }

    // ── AES helpers (for vault-key.enc) ──
    static byte[] AesEnc(byte[] p, byte[] k) { var n = RandomNumberGenerator.GetBytes(12); var c = new byte[p.Length]; var t = new byte[16]; using var a = new AesGcm(k, 16); a.Encrypt(n, p, c, t); var r = new byte[12 + c.Length + 16]; Array.Copy(n, 0, r, 0, 12); Array.Copy(c, 0, r, 12, c.Length); Array.Copy(t, 0, r, 12 + c.Length, 16); return r; }
    static byte[] AesDec(byte[] e, byte[] k) { var n = e[..12]; var t = e[^16..]; var c = e[12..^16]; var p = new byte[c.Length]; using var a = new AesGcm(k, 16); a.Decrypt(n, c, t, p); return p; }

    class BlobHeader { public string AesKey { get; set; } = ""; public string AesNonce { get; set; } = ""; public long OriginalLength { get; set; } }
}



