namespace Gyroown.Services;

/// <summary>
/// Application theme enum.
/// </summary>
public enum AppTheme
{
    Default,    // Follow system theme
    Light,
    Dark
}

/// <summary>
/// Theme service interface — manages theme, accent color, and language persistence.
/// Settings stored in ~/.Gyroown/settings.gyrojson (encrypted with vault key).
/// </summary>
public interface IThemeService
{
    /// <summary>Current application theme.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>Current accent color hex string (default "#0078D4").</summary>
    string AccentColor { get; }

    /// <summary>Current language code.</summary>
    string Language { get; }

    /// <summary>Initialize with vault key and load encrypted settings. Call after vault unlock.</summary>
    void Initialize(byte[] vaultKey);

    /// <summary>Set application theme, persist and fire ThemeChanged.</summary>
    void SetTheme(AppTheme theme);

    /// <summary>Set accent color (hex), persist and fire ThemeChanged.</summary>
    void SetAccentColor(string hexColor);

    /// <summary>Set language code, persist and fire ThemeChanged.</summary>
    void SetLanguage(string lang);

    /// <summary>Get all available themes.</summary>
    IReadOnlyList<AppTheme> GetAvailableThemes();

    /// <summary>Fired when theme, accent, or language changes.</summary>
    event EventHandler? ThemeChanged;
}
