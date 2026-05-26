namespace Gyroown.Services;

/// <summary>
/// (stub) — 从 INI (stub)
/// </summary>
public interface ILocalizationService
{
    /// <summary>(stub) section (stub) key (stub)</summary>
    string Get(string section, string key);

    /// <summary>(stub) "zh-CN", "en-US"）</summary>
    string CurrentLanguage { get; }

    /// <summary>(stub)</summary>
    void SetLanguage(string langCode);

    /// <summary>(stub)</summary>
    IReadOnlyList<string> GetAvailableLanguages();

    /// <summary>(stub)UI (stub)</summary>
    event EventHandler? LanguageChanged;
}
