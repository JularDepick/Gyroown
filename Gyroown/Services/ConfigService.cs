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

    // Tier → MB mapping (tier 0 is disabled/1MB minimum to avoid division by zero)
    public static readonly int[] ChunkTiers = { 1, 2, 4, 8, 16, 32, 64 };
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
        catch (Exception ex) { LogService.Warn($"ConfigService.Load: {ex.Message}"); }
        return new CoreConfig();
    }

    public async Task SaveAsync(CoreConfig config)
    {
        if (_vaultKey == null) return;
        try
        {
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config, JsonConfig.Options));
            var blob = _enc.EncryptBlob(json, _vaultKey);
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            await File.WriteAllBytesAsync(_configPath, blob);
        }
        catch (Exception ex) { LogService.Warn($"ConfigService.SaveAsync: {ex.Message}"); }
    }

    /// <summary>Fire-and-forget save for synchronous call sites.</summary>
    public void Save(CoreConfig config) { if (_vaultKey != null) _ = SaveAsync(config); }

    /// <summary>Get chunk size in bytes for given tier.</summary>
    public static int ChunkSizeForTier(int tier) =>
        tier >= 0 && tier < ChunkTiers.Length ? ChunkTiers[tier] * 1024 * 1024 : 32 * 1024 * 1024;
}

public class CoreConfig
{
    public int ChunkTier { get; set; } = ConfigService.DefaultTier;
    public int MaxVersions { get; set; } = 10;
    public int AutoLockTimeout { get; set; } = 0; // seconds; 0 = disabled
    public bool GeneratePreviews { get; set; } = true; // encrypted thumbnails for images/videos
    public int LockoutFails { get; set; } = 0; // persisted failed attempt count
    public long LockoutUntilTicks { get; set; } = 0; // persisted lockout expiry (DateTime.Ticks, 0 = not locked)
}
