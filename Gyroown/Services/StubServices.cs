namespace Gyroown.Services;

/// <summary>
/// Localization implementation — INI file parser with runtime language switching.
/// </summary>
public class StubLocalizationService : ILocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _strings = new();
    private string _currentLang = "zh-CN";

    public string CurrentLanguage => _currentLang;
    public event EventHandler? LanguageChanged;

    public StubLocalizationService() => LoadLanguage("zh-CN");

    public string Get(string section, string key)
    {
        if (_strings.TryGetValue(section, out var dict) && dict.TryGetValue(key, out var val)) return val;
        return $"[{section}.{key}]";
    }

    public void SetLanguage(string langCode) { LoadLanguage(langCode); _currentLang = langCode; LanguageChanged?.Invoke(this, EventArgs.Empty); }
    public IReadOnlyList<string> GetAvailableLanguages() => new[] { "zh-CN", "en-US" };

    private void LoadLanguage(string langCode)
    {
        _strings.Clear();
        var langDir = Path.Combine(AppContext.BaseDirectory, "lang");
        var file = Path.Combine(langDir, $"{langCode}.ini");
        if (!File.Exists(file)) { var srcDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "lang"); file = Path.Combine(srcDir, $"{langCode}.ini"); }
        if (!File.Exists(file)) return;

        string? cur = null;
        foreach (var line in File.ReadAllLines(file))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t) || t.StartsWith(';')) continue;
            if (t.StartsWith('[') && t.EndsWith(']')) { cur = t[1..^1]; if (!_strings.ContainsKey(cur)) _strings[cur] = new(); }
            else if (cur != null) { var eq = t.IndexOf('='); if (eq > 0) _strings[cur][t[..eq].Trim()] = t[(eq + 1)..].Trim(); }
        }
    }
}
