namespace Gyroown.Services;

/// <summary>
/// Static localization helper — provides concise access to localized strings
/// throughout the application without requiring DI injection in every control.
/// </summary>
public static class Loc
{
    private static ILocalizationService _service = new StubLocalizationService();

    public static ILocalizationService Service
    {
        get => _service;
        set
        {
            if (_service != value)
            {
                if (_service is StubLocalizationService)
                    _service = value;
                else
                    _service.LanguageChanged -= OnLanguageChanged;
                _service = value;
                _service.LanguageChanged += OnLanguageChanged;
            }
        }
    }

    public static string Get(string section, string key) => _service.Get(section, key);

    public static string Format(string section, string key, params object[] args) =>
        string.Format(_service.Get(section, key), args);

    public static event EventHandler? LanguageChanged;

    private static void OnLanguageChanged(object? sender, EventArgs e) =>
        LanguageChanged?.Invoke(null, e);
}
