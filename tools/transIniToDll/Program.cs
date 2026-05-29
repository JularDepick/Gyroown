// IniToResourceTool — Converts .ini translation files to DLL embedded resources.
// Usage:
//   IniToResourceTool embed <lang-code> [path-to-ini]
//   IniToResourceTool validate <path-to-ini>
//   IniToResourceTool report

if (args.Length < 1)
{
    PrintUsage();
    return 1;
}

var repoRoot = FindRepoRoot();
if (repoRoot == null)
{
    Console.Error.WriteLine("Error: Could not find repository root (Gyroown.slnx not found).");
    return 1;
}

return args[0].ToLower() switch
{
    "embed" => RunEmbed(args.Skip(1).ToArray(), repoRoot),
    "validate" => RunValidate(args.Skip(1).ToArray(), repoRoot),
    "report" => RunReport(repoRoot),
    _ => PrintUsage()
};

static int PrintUsage()
{
    Console.WriteLine("IniToResourceTool — .ini translation to DLL embedded resource converter");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  IniToResourceTool embed <lang-code> [path-to-ini]   Copy .ini to Resources/Loc/ for embedding");
    Console.WriteLine("  IniToResourceTool validate <path-to-ini>            Validate .ini against zh-CN baseline");
    Console.WriteLine("  IniToResourceTool report                            Show embedded vs .ini-only status");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  IniToResourceTool embed ja-JP lang/ja-JP.ini");
    Console.WriteLine("  IniToResourceTool validate lang/fr-FR.ini");
    Console.WriteLine("  IniToResourceTool report");
    return 0;
}

static string? FindRepoRoot()
{
    var dir = Directory.GetCurrentDirectory();
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir, "Gyroown.slnx")))
            return dir;
        dir = Directory.GetParent(dir)?.FullName;
    }
    return null;
}

static int RunEmbed(string[] args, string repoRoot)
{
    if (args.Length < 1)
    {
        Console.Error.WriteLine("Usage: IniToResourceTool embed <lang-code> [path-to-ini]");
        return 1;
    }

    var langCode = args[0];
    var iniPath = args.Length > 1 ? args[1] : Path.Combine(repoRoot, "Gyroown", "lang", $"{langCode}.ini");

    if (!File.Exists(iniPath))
    {
        Console.Error.WriteLine($"Error: File not found: {iniPath}");
        return 1;
    }

    // Validate the .ini file
    var parsed = ParseIni(iniPath);
    if (parsed == null)
    {
        Console.Error.WriteLine($"Error: Failed to parse {iniPath}");
        return 1;
    }

    // Check __meta__ section
    if (parsed.TryGetValue("__meta__", out var meta))
    {
        if (meta.TryGetValue("LangCode", out var code) && code != langCode)
        {
            Console.Error.WriteLine($"Warning: __meta__.LangCode '{code}' does not match argument '{langCode}'");
        }
    }
    else
    {
        Console.Error.WriteLine("Warning: No [__meta__] section found in .ini file");
    }

    // Copy to Resources/Loc/
    var destDir = Path.Combine(repoRoot, "Gyroown", "Resources", "Loc");
    Directory.CreateDirectory(destDir);
    var destPath = Path.Combine(destDir, $"{langCode}.ini");
    File.Copy(iniPath, destPath, overwrite: true);

    Console.WriteLine($"Copied: {iniPath} -> {destPath}");
    Console.WriteLine();
    Console.WriteLine("Add the following to Gyroown.csproj <ItemGroup>:");
    Console.WriteLine($"  <EmbeddedResource Include=\"Resources\\Loc\\{langCode}.ini\" />");
    Console.WriteLine();
    Console.WriteLine("Then rebuild: dotnet build");

    return 0;
}

static int RunValidate(string[] args, string repoRoot)
{
    if (args.Length < 1)
    {
        Console.Error.WriteLine("Usage: IniToResourceTool validate <path-to-ini>");
        return 1;
    }

    var iniPath = args[0];
    if (!File.Exists(iniPath))
    {
        Console.Error.WriteLine($"Error: File not found: {iniPath}");
        return 1;
    }

    // Parse target file
    var parsed = ParseIni(iniPath);
    if (parsed == null)
    {
        Console.Error.WriteLine($"Error: Failed to parse {iniPath}");
        return 1;
    }

    // Parse zh-CN baseline
    var baselinePath = Path.Combine(repoRoot, "Gyroown", "lang", "zh-CN.ini");
    var baseline = ParseIni(baselinePath);
    if (baseline == null)
    {
        Console.Error.WriteLine($"Error: Failed to parse baseline: {baselinePath}");
        return 1;
    }

    // Check __meta__
    if (parsed.TryGetValue("__meta__", out var meta))
    {
        Console.WriteLine($"LangCode:  {meta.GetValueOrDefault("LangCode", "(missing)")}");
        Console.WriteLine($"AppVersion: {meta.GetValueOrDefault("AppVersion", "(missing)")}");
    }
    else
    {
        Console.WriteLine("Warning: No [__meta__] section found");
    }

    // Compare sections and keys
    var baselineSections = baseline.Keys.Where(k => k != "__meta__").ToHashSet();
    var parsedSections = parsed.Keys.Where(k => k != "__meta__").ToHashSet();

    var missingSections = baselineSections.Except(parsedSections).ToList();
    var extraSections = parsedSections.Except(baselineSections).ToList();

    int totalKeys = 0, matchedKeys = 0, missingKeys = 0;

    foreach (var section in baselineSections)
    {
        if (!parsed.TryGetValue(section, out var parsedDict))
        {
            missingKeys += baseline[section].Count;
            totalKeys += baseline[section].Count;
            continue;
        }

        foreach (var key in baseline[section].Keys)
        {
            totalKeys++;
            if (parsedDict.ContainsKey(key))
                matchedKeys++;
            else
                missingKeys++;
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Sections: {parsedSections.Count} / {baselineSections.Count}");
    if (missingSections.Count > 0)
        Console.WriteLine($"  Missing: {string.Join(", ", missingSections)}");
    if (extraSections.Count > 0)
        Console.WriteLine($"  Extra:   {string.Join(", ", extraSections)}");

    Console.WriteLine();
    Console.WriteLine($"Keys: {matchedKeys} / {totalKeys} matched");
    if (missingKeys > 0)
        Console.WriteLine($"  Missing: {missingKeys}");

    var coverage = totalKeys > 0 ? (matchedKeys * 100.0 / totalKeys) : 0;
    Console.WriteLine();
    Console.WriteLine($"Coverage: {coverage:F1}%");

    if (missingKeys > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Missing keys:");
        foreach (var section in baselineSections)
        {
            if (!parsed.TryGetValue(section, out var parsedDict))
            {
                Console.WriteLine($"  [{section}] (entire section missing)");
                continue;
            }
            foreach (var key in baseline[section].Keys)
            {
                if (!parsedDict.ContainsKey(key))
                    Console.WriteLine($"  [{section}] {key}");
            }
        }
    }

    return missingKeys > 0 ? 1 : 0;
}

static int RunReport(string repoRoot)
{
    var langDir = Path.Combine(repoRoot, "Gyroown", "lang");
    var resDir = Path.Combine(repoRoot, "Gyroown", "Resources", "Loc");

    var iniFiles = Directory.Exists(langDir)
        ? Directory.GetFiles(langDir, "*.ini").Select(Path.GetFileNameWithoutExtension).ToHashSet()
        : new HashSet<string?>();

    var embeddedFiles = Directory.Exists(resDir)
        ? Directory.GetFiles(resDir, "*.ini").Select(Path.GetFileNameWithoutExtension).ToHashSet()
        : new HashSet<string?>();

    Console.WriteLine("Language Status:");
    Console.WriteLine($"{"Code",-10} {"INI",-8} {"Embedded",-10}");
    Console.WriteLine(new string('-', 28));

    foreach (var (code, name) in new[] {
        ("zh-CN", "Simplified Chinese"),
        ("zh-TW", "Traditional Chinese"),
        ("en-US", "English (US)"),
        ("en-GB", "English (UK)"),
        ("ja-JP", "Japanese"),
        ("ko-KR", "Korean"),
        ("fr-FR", "French")
    })
    {
        var hasIni = iniFiles.Contains(code) ? "Yes" : "No";
        var hasEmbedded = embeddedFiles.Contains(code) ? "Yes" : "No";
        Console.WriteLine($"{code,-10} {hasIni,-8} {hasEmbedded,-10}");
    }

    Console.WriteLine();
    Console.WriteLine($"External .ini files: {iniFiles.Count}");
    Console.WriteLine($"Embedded resources: {embeddedFiles.Count}");

    return 0;
}

static Dictionary<string, Dictionary<string, string>>? ParseIni(string path)
{
    if (!File.Exists(path)) return null;

    try
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        string? cur = null;

        foreach (var line in File.ReadAllLines(path))
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
                    result[cur][t[..eq].Trim()] = t[(eq + 1)..].Trim();
            }
        }

        return result.Count > 0 ? result : null;
    }
    catch { return null; }
}
