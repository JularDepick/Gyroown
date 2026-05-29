using System.Text;
using System.Text.Json;

namespace Gyroown.Services;

/// <summary>
/// Accent color preset definition.
/// </summary>
public class AccentPreset
{
    public string Name { get; init; } = "";
    public string Hex { get; init; } = "#0078D4";
}

public class ThemeService : IThemeService
{
    private readonly string _settingsFile;
    private readonly EncryptionService _enc = new();
    private byte[]? _vaultKey;
    private AppTheme _current = AppTheme.Default;
    private string _accent = "#0078D4";
    private string _language = AppInfo.DefaultLanguage;

    public AppTheme CurrentTheme => _current;
    public string AccentColor => _accent;
    public string Language => _language;
    public event EventHandler? ThemeChanged;

    public static IReadOnlyList<AccentPreset> AccentPresets { get; } = new[]
    {
        new AccentPreset { Name = "Blue",     Hex = "#0078D4" },
        new AccentPreset { Name = "Teal",     Hex = "#038387" },
        new AccentPreset { Name = "Green",    Hex = "#10893E" },
        new AccentPreset { Name = "Orange",   Hex = "#CA5010" },
        new AccentPreset { Name = "Purple",   Hex = "#8764B8" },
        new AccentPreset { Name = "Pink",     Hex = "#E3008C" },
        new AccentPreset { Name = "Red",      Hex = "#D13438" },
        new AccentPreset { Name = "Graphite", Hex = "#4A5459" },
    };

    public ThemeService()
    {
        _settingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".Gyroown", "settings.gyrojson");
    }

    /// <summary>
    /// Initialize with vault key and load encrypted settings.
    /// Call after vault is unlocked. Triggers ThemeChanged if settings differ from defaults.
    /// </summary>
    public void Initialize(byte[] vaultKey)
    {
        _vaultKey = vaultKey;
        var oldTheme = _current;
        var oldAccent = _accent;
        var oldLang = _language;
        Load();
        if (_language != oldLang)
            Loc.Service.SetLanguage(_language);
        if (_current != oldTheme || _accent != oldAccent)
            ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetTheme(AppTheme theme)
    { if (_current == theme) return; _current = theme; Save(); ThemeChanged?.Invoke(this, EventArgs.Empty); }

    public void SetAccentColor(string hex)
    { if (_accent == hex) return; _accent = hex; Save(); ThemeChanged?.Invoke(this, EventArgs.Empty); }

    public void SetLanguage(string lang)
    { if (_language == lang) return; _language = lang; Save(); ThemeChanged?.Invoke(this, EventArgs.Empty); }

    public IReadOnlyList<AppTheme> GetAvailableThemes() =>
        new[] { AppTheme.Default, AppTheme.Light, AppTheme.Dark };

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsFile) && _vaultKey != null)
            {
                var blob = File.ReadAllBytes(_settingsFile);
                var json = _enc.DecryptBlob(blob, _vaultKey);
                var d = JsonSerializer.Deserialize<SettingsData>(Encoding.UTF8.GetString(json), JsonConfig.Options);
                if (d != null)
                {
                    _current = Enum.TryParse<AppTheme>(d.Theme, out var t) ? t : AppTheme.Default;
                    _accent = d.Accent ?? "#0078D4";
                    _language = d.Language ?? AppInfo.DefaultLanguage;
                }
            }
        }
        catch (Exception ex) { LogService.Warn($"ThemeService.Load: {ex.Message}"); }
    }

    private async Task SaveAsync()
    {
        if (_vaultKey == null) return;
        try
        {
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new SettingsData { Theme = _current.ToString(), Accent = _accent, Language = _language },
                JsonConfig.Options));
            var blob = _enc.EncryptBlob(json, _vaultKey);
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            await File.WriteAllBytesAsync(_settingsFile, blob);
        }
        catch (Exception ex) { LogService.Warn($"ThemeService.SaveAsync: {ex.Message}"); }
    }

    private void Save() { if (_vaultKey != null) _ = SaveAsync(); }

    private class SettingsData
    {
        public string? Theme { get; set; }
        public string? Accent { get; set; }
        public string? Language { get; set; }
    }
}
