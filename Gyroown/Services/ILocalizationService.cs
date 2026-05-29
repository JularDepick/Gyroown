namespace Gyroown.Services;

/// <summary>
/// Localization service interface — loads translated strings from INI language files, supports runtime language switching.
/// </summary>
public interface ILocalizationService
{
    /// <summary>Get a translated string by section and key; returns [section.key] fallback if not found.</summary>
    string Get(string section, string key);

    /// <summary>Current language code (e.g. "zh-CN", "en-US").</summary>
    string CurrentLanguage { get; }

    /// <summary>Switch language: reload the corresponding INI file and fire the LanguageChanged event.</summary>
    void SetLanguage(string langCode);

    /// <summary>Get a list of all available language codes.</summary>
    IReadOnlyList<string> GetAvailableLanguages();

    /// <summary>Language change event, used to notify UI to refresh displayed text.</summary>
    event EventHandler? LanguageChanged;
}
