using System.Security.Cryptography;

namespace Gyroown.Services;

/// <summary>
/// Simple XOR-based encryption for the picture password background image.
/// Not cryptographically strong — just prevents casual plaintext access.
/// Independent of vault key and user password.
/// </summary>
public static class ImageProtection
{
    private const string KeyFileName = ".imgkey";
    private const string ImageFileName = "image.pwimg";

    private static string KeyPath => Path.Combine(VaultService.AuthDir, KeyFileName);
    private static string ImagePath => Path.Combine(VaultService.AuthDir, ImageFileName);

    public static bool HasStoredImage => File.Exists(ImagePath);

    public static void SaveImage(byte[] imageData)
    {
        var key = GetOrCreateKey();
        Directory.CreateDirectory(VaultService.AuthDir);
        using var fs = File.Create(ImagePath);
        Encrypt(imageData, key, fs);
        VaultService.ProtectAuthDir();
    }

    public static byte[]? LoadImage()
    {
        if (!File.Exists(ImagePath)) return null;
        var key = GetOrCreateKey();
        var enc = File.ReadAllBytes(ImagePath);
        if (enc.Length < 17) return null; // need at least 16B salt + 1B data
        return Decrypt(enc, key);
    }

    public static void DeleteImage()
    {
        try { if (File.Exists(ImagePath)) File.Delete(ImagePath); } catch { }
    }

    static byte[] GetOrCreateKey()
    {
        if (File.Exists(KeyPath)) return File.ReadAllBytes(KeyPath);
        var key = RandomNumberGenerator.GetBytes(32);
        Directory.CreateDirectory(VaultService.AuthDir);
        File.WriteAllBytes(KeyPath, key);
        VaultService.ProtectAuthDir();
        return key;
    }

    static void Encrypt(byte[] data, byte[] key, Stream output)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        output.Write(salt, 0, 16);
        for (int i = 0; i < data.Length; i++)
            output.WriteByte((byte)(data[i] ^ key[(i + salt[i % 16]) % key.Length]));
    }

    static byte[] Decrypt(byte[] enc, byte[] key)
    {
        var salt = enc[..16];
        var data = new byte[enc.Length - 16];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)(enc[i + 16] ^ key[(i + salt[i % 16]) % key.Length]);
        return data;
    }
}
