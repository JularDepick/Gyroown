namespace Gyroown;

/// <summary>
/// Application version, metadata, and supported languages.
/// </summary>
public static class AppInfo
{
    public const string Name = "Gyroown";
    public const string Version = "0.1.1";
    public const string VersionPrefix = "v";
    public const string FullVersion = $"{VersionPrefix}{Version}";
    public const string Author = "JularDepick";
    public const string Repository = "https://github.com/JularDepick/Gyroown";
    public const string Description = "Offline encrypted file vault";

    /// <summary>Default language code (used when no user preference is set).</summary>
    public const string DefaultLanguage = "zh-CN";

    /// <summary>Supported language codes and display names.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> SupportedLanguages = new[]
    {
        ("zh-CN", "Simplified Chinese"),
        ("zh-TW", "Traditional Chinese"),
        ("en-US", "English (US)"),
        ("en-GB", "English (UK)"),
        ("ja-JP", "Japanese"),
        ("ko-KR", "Korean"),
        ("fr-FR", "French"),
    };
}
