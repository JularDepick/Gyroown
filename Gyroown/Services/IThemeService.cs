namespace Gyroown.Services;

/// <summary>
/// (stub)
/// </summary>
public enum AppTheme
{
    Default,    // (stub)
    Light,
    Dark
}

/// <summary>
/// (stub) — (stub)
/// (stub)
/// </summary>
public interface IThemeService
{
    /// <summary>(stub)</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>(stub) "#0078D4"）</summary>
    string AccentColor { get; }

    /// <summary>(stub)</summary>
    void SetTheme(AppTheme theme);

    /// <summary>(stub)</summary>
    void SetAccentColor(string hexColor);

    /// <summary>(stub)</summary>
    IReadOnlyList<AppTheme> GetAvailableThemes();

    /// <summary>(stub)</summary>
    event EventHandler? ThemeChanged;
}
