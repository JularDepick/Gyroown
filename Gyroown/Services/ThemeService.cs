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
    private AppTheme _current = AppTheme.Default;
    private string _accent = "#0078D4";
    private string _language = "zh-CN";

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
            ".Gyroown", "settings.json");
        Load();
    }

    public void SetTheme(AppTheme theme)
    { if (_current == theme) return; _current = theme; Save(); ThemeChanged?.Invoke(this, EventArgs.Empty); }

    public void SetAccentColor(string hex)
    { if (_accent == hex) return; _accent = hex; Save(); ThemeChanged?.Invoke(this, EventArgs.Empty); }

    public void SetLanguage(string lang)
    { if (_language == lang) return; _language = lang; Save(); }

    public IReadOnlyList<AppTheme> GetAvailableThemes() =>
        new[] { AppTheme.Default, AppTheme.Light, AppTheme.Dark };

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var d = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_settingsFile), JsonConfig.Options);
                if (d != null) { _current = Enum.TryParse<AppTheme>(d.Theme, out var t) ? t : AppTheme.Default; _accent = d.Accent ?? "#0078D4"; _language = d.Language ?? "zh-CN"; }
            }
        }
        catch { }
    }

    private void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!); File.WriteAllText(_settingsFile, JsonSerializer.Serialize(new SettingsData { Theme = _current.ToString(), Accent = _accent, Language = _language }, JsonConfig.Options)); }
        catch { }
    }

    private class SettingsData { public string? Theme { get; set; } public string? Accent { get; set; } public string? Language { get; set; } }
}



