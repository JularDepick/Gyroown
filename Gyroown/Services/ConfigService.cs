using System.Text;
using System.Text.Json;

namespace Gyroown.Services;

/// <summary>
/// Encrypted core configuration (config.gyrojson).
/// Uses vault key pair encryption, same format as data files.
/// </summary>
public class ConfigService
{
    private readonly string _configPath;
    private readonly EncryptionService _enc = new();
    private byte[]? _vaultKey;

    // Tier → MB mapping
    public static readonly int[] ChunkTiers = { 0, 2, 4, 8, 16, 32, 64 };
    public const int DefaultTier = 5; // 32 MB

    public ConfigService()
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".Gyroown", "config.gyrojson");
    }

    public void Initialize(byte[] vaultKey)
    {
        _vaultKey = vaultKey;
    }

    public CoreConfig Load()
    {
        try
        {
            if (File.Exists(_configPath) && _vaultKey != null)
            {
                var blob = File.ReadAllBytes(_configPath);
                var json = _enc.DecryptBlob(blob, _vaultKey);
                return JsonSerializer.Deserialize<CoreConfig>(Encoding.UTF8.GetString(json), JsonConfig.Options)
                       ?? new CoreConfig();
            }
        }
        catch { }
        return new CoreConfig();
    }

    public void Save(CoreConfig config)
    {
        if (_vaultKey == null) return;
        var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config, JsonConfig.Options));
        var blob = _enc.EncryptBlob(json, _vaultKey);
        Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        File.WriteAllBytes(_configPath, blob);
    }

    /// <summary>Get chunk size in bytes for given tier.</summary>
    public static int ChunkSizeForTier(int tier) =>
        tier >= 1 && tier < ChunkTiers.Length ? ChunkTiers[tier] * 1024 * 1024 : 32 * 1024 * 1024;
}

public class CoreConfig
{
    public int ChunkTier { get; set; } = ConfigService.DefaultTier;
}
