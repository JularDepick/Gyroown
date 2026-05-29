using System.Reflection;

namespace Gyroown.Services;

/// <summary>
/// Localization implementation — INI parser with embedded resource fallback.
/// Loading priority: external .ini file > DLL embedded resource > field name degradation.
/// Supports all languages defined in AppInfo.SupportedLanguages.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _strings = new();
    private readonly Dictionary<string, Dictionary<string, string>> _fallback = new();
    private string _currentLang = AppInfo.DefaultLanguage;

    public string CurrentLanguage => _currentLang;
    public event EventHandler? LanguageChanged;

    public LocalizationService() => LoadLanguage(AppInfo.DefaultLanguage);

    public string Get(string section, string key)
    {
        if (_strings.TryGetValue(section, out var dict) && dict.TryGetValue(key, out var val)) return val;
        if (_fallback.TryGetValue(section, out var fb) && fb.TryGetValue(key, out var fbVal)) return fbVal;
        return $"[{section}.{key}]";
    }

    public IReadOnlyList<string> GetAvailableLanguages() =>
        AppInfo.SupportedLanguages.Select(l => l.Code).ToList();

    public void SetLanguage(string langCode)
    {
        if (!LoadLanguage(langCode)) return;
        _currentLang = langCode;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool LoadLanguage(string langCode)
    {
        if (langCode.Contains("..") || langCode.Contains('/') || langCode.Contains('\\'))
        {
            LogService.Warn($"Localization: rejected langCode with path traversal characters: '{langCode}'");
            return false;
        }

        _strings.Clear();
        _fallback.Clear();

        // Primary: external .ini file overrides embedded
        var parsed = ParseIniFromFile(langCode) ?? ParseIniFromEmbedded(langCode);
        if (parsed != null)
        {
            foreach (var (sec, dict) in parsed)
                _strings[sec] = dict;
        }

        // Fallback: always load default language as safety net
        if (langCode != AppInfo.DefaultLanguage)
        {
            var fb = ParseIniFromFile(AppInfo.DefaultLanguage)
                  ?? ParseIniFromEmbedded(AppInfo.DefaultLanguage);
            if (fb == null && AppInfo.DefaultLanguage != "en-US")
                fb = ParseIniFromEmbedded("en-US"); // last-resort embedded fallback
            if (fb != null)
                foreach (var (sec, dict) in fb)
                    _fallback[sec] = dict;
            else
                LogService.Warn("Localization: no embedded fallback found for default language");
        }
        return true;
    }

    private static Dictionary<string, Dictionary<string, string>>? ParseIniFromFile(string langCode)
    {
        var langDir = Path.Combine(AppContext.BaseDirectory, "lang");
        var file = Path.Combine(langDir, $"{langCode}.ini");
        if (!File.Exists(file))
        {
            var srcDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "lang");
            file = Path.Combine(srcDir, $"{langCode}.ini");
        }
        if (!File.Exists(file)) return null;

        try
        {
            using var reader = new StreamReader(file, System.Text.Encoding.UTF8);
            return ParseIniFromReader(reader, langCode);
        }
        catch { return null; }
    }

    private static Dictionary<string, Dictionary<string, string>>? ParseIniFromEmbedded(string langCode)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Gyroown.Resources.Loc.{langCode}.ini";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        try
        {
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            return ParseIniFromReader(reader, langCode);
        }
        catch { return null; }
    }

    private static Dictionary<string, Dictionary<string, string>>? ParseIniFromReader(TextReader reader, string langCode)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        string? cur = null;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t) || t.StartsWith(';')) continue;

            if (t.StartsWith('[') && t.EndsWith(']'))
            {
                cur = t[1..^1];
                if (!result.ContainsKey(cur)) result[cur] = new();
            }
            else if (cur != null)
            {
                var eq = t.IndexOf('=');
                if (eq > 0)
                {
                    var key = t[..eq].Trim();
                    var value = t[(eq + 1)..].Trim();
                    result[cur][key] = value;

                    // Validate AppVersion in __meta__ section
                    if (cur == "__meta__" && key == "AppVersion" && value != AppInfo.Version)
                    {
                        LogService.Warn($"Localization: {langCode}.ini AppVersion '{value}' does not match app version '{AppInfo.Version}'");
                    }
                }
            }
        }

        return result.Count > 0 ? result : null;
    }
}
