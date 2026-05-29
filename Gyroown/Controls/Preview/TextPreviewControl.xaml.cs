using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;
using Gyroown.Services;

namespace Gyroown.Controls.Preview;

public sealed partial class TextPreviewControl : UserControl
{
    private byte[]? _rawBytes;
    private string _currentEncoding = "UTF-8";
    private bool _wordWrap = true;
    private string _language = "text";
    private bool _suppressEvents;

    public TextPreviewControl()
    {
        InitializeComponent();
        InitEncodingCombo();
        InitLabels();
    }

    public void LoadText(byte[] rawBytes, string? fileNameHint = null)
    {
        _rawBytes = rawBytes;
        _language = DetectLanguage(fileNameHint);
        LangLabel.Text = _language.ToUpperInvariant();
        RenderText();
    }

    // ── Encoding ──

    void InitEncodingCombo()
    {
        _suppressEvents = true;
        EncodingCombo.Items.Add(new ComboBoxItem { Content = "UTF-8", Tag = "utf-8" });
        EncodingCombo.Items.Add(new ComboBoxItem { Content = "GBK", Tag = "gbk" });
        EncodingCombo.Items.Add(new ComboBoxItem { Content = "ASCII", Tag = "ascii" });
        EncodingCombo.Items.Add(new ComboBoxItem { Content = "ISO-8859-1", Tag = "iso-8859-1" });
        EncodingCombo.Items.Add(new ComboBoxItem { Content = "UTF-16 LE", Tag = "utf-16" });
        EncodingCombo.SelectedIndex = 0;
        _suppressEvents = false;
    }

    void InitLabels()
    {
        EncodingLabel.Text = Loc.Get("Viewer", "Encoding");
        WrapLabel.Text = Loc.Get("Viewer", "WordWrap");
    }

    void OnEncodingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        if (EncodingCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _currentEncoding = tag;
            RenderText();
        }
    }

    void OnWrapToggled(object sender, RoutedEventArgs e)
    {
        _wordWrap = WrapToggle.IsChecked == true;
        CodeContent.TextWrapping = _wordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        CodeScroller.HorizontalScrollBarVisibility = _wordWrap
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    void RenderText()
    {
        if (_rawBytes == null) return;

        string text;
        try
        {
            var enc = _currentEncoding switch
            {
                "utf-8" => System.Text.Encoding.UTF8,
                "gbk" => System.Text.Encoding.GetEncoding("GBK"),
                "ascii" => System.Text.Encoding.ASCII,
                "iso-8859-1" => System.Text.Encoding.GetEncoding("ISO-8859-1"),
                "utf-16" => System.Text.Encoding.Unicode,
                _ => System.Text.Encoding.UTF8
            };
            text = enc.GetString(_rawBytes);
        }
        catch
        {
            text = System.Text.Encoding.UTF8.GetString(_rawBytes);
        }

        var lines = text.Split('\n');
        StatusText.Text = $"{lines.Length} lines | {text.Length} chars | {_currentEncoding.ToUpperInvariant()}";

        // Render line numbers
        LineNumbers.Children.Clear();
        for (int i = 0; i < lines.Length; i++)
        {
            var num = new TextBlock
            {
                Text = (i + 1).ToString(),
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New, monospace"),
                FontSize = 13,
                LineHeight = 20,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextAlignment = TextAlignment.Right,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            LineNumbers.Children.Add(num);
        }

        // Render code with syntax highlighting
        ApplySyntaxHighlighting(text);
    }

    void ApplySyntaxHighlighting(string text)
    {
        CodeContent.Inlines.Clear();

        if (_language == "text" || _language == "plain")
        {
            CodeContent.Inlines.Add(new Run { Text = text });
            return;
        }

        var rules = GetSyntaxRules(_language);
        if (rules.Count == 0)
        {
            CodeContent.Inlines.Add(new Run { Text = text });
            return;
        }

        var defaultColor = ThemeColor("#D4D4D4", "#1E1E1E");

        // Build combined pattern
        var combined = string.Join("|", rules.Select(r => $"(?<{r.Name}>{r.Pattern})"));
        var regex = new Regex(combined, RegexOptions.Multiline);

        int lastIndex = 0;
        foreach (Match match in regex.Matches(text))
        {
            // Add unhighlighted text before this match
            if (match.Index > lastIndex)
            {
                CodeContent.Inlines.Add(new Run
                {
                    Text = text[lastIndex..match.Index],
                    Foreground = new SolidColorBrush(ColorHelper(defaultColor))
                });
            }

            // Determine which group matched
            foreach (var rule in rules)
            {
                if (match.Groups[rule.Name].Success)
                {
                    CodeContent.Inlines.Add(new Run
                    {
                        Text = match.Value,
                        Foreground = new SolidColorBrush(ColorHelper(ThemeColor(rule.DarkColor, rule.LightColor)))
                    });
                    break;
                }
            }

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            CodeContent.Inlines.Add(new Run
            {
                Text = text[lastIndex..],
                Foreground = new SolidColorBrush(ColorHelper(defaultColor))
            });
        }
    }

    // ── Language detection ──

    static string DetectLanguage(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "text";
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".js" or ".jsx" or ".mjs" => "javascript",
            ".ts" or ".tsx" => "typescript",
            ".py" or ".pyw" => "python",
            ".java" => "java",
            ".cpp" or ".cc" or ".cxx" or ".hpp" or ".h" or ".c" => "cpp",
            ".json" => "json",
            ".xml" or ".xaml" or ".csproj" or ".slnx" => "xml",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".sql" => "sql",
            ".sh" or ".bash" or ".zsh" => "bash",
            ".ps1" or ".psm1" => "powershell",
            ".rb" => "ruby",
            ".go" => "go",
            ".rs" => "rust",
            ".kt" or ".kts" => "kotlin",
            ".swift" => "swift",
            ".yaml" or ".yml" => "yaml",
            ".ini" or ".cfg" or ".conf" => "ini",
            ".md" or ".markdown" => "markdown",
            _ => "text"
        };
    }

    // ── Syntax rules ──

    record SyntaxRule(string Name, string Pattern, string DarkColor, string LightColor);

    static List<SyntaxRule> GetSyntaxRules(string language) => language switch
    {
        "csharp" => new()
        {
            new("cs_string", @"""(?:[^""\\]|\\.)*""|@""(?:[^""]|"""")*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("cs_comment", @"//.*$|/\*[\s\S]*?\*/", "#6A9955", "#008000"),
            new("cs_keyword", @"\b(?:abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|var|virtual|void|volatile|while|async|await|yield|record|init|required|file|scoped|when)\b", "#569CD6", "#0000FF"),
            new("cs_type", @"\b(?:string|int|long|bool|double|float|decimal|byte|char|object|void|dynamic)\b", "#4EC9B0", "#2B91AF"),
            new("cs_number", @"\b\d+\.?\d*(?:[eE][+-]?\d+)?[fFdDmMuUlL]*\b|0x[0-9a-fA-F]+", "#B5CEA8", "#098658"),
            new("cs_attribute", @"\[[\w.]+(?:\(.*?\))?\]", "#DCDCAA", "#795E26"),
        },
        "javascript" or "typescript" => new()
        {
            new("js_string", @"`(?:[^`\\]|\\.)*`|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("js_comment", @"//.*$|/\*[\s\S]*?\*/", "#6A9955", "#008000"),
            new("js_keyword", @"\b(?:abstract|arguments|async|await|boolean|break|byte|case|catch|char|class|const|continue|debugger|default|delete|do|double|else|enum|eval|export|extends|false|final|finally|float|for|from|function|goto|if|implements|import|in|instanceof|int|interface|let|long|native|new|null|of|package|private|protected|public|return|short|static|super|switch|synchronized|this|throw|throws|transient|true|try|typeof|undefined|var|void|volatile|while|with|yield|type|interface|namespace|module|declare|readonly|keyof|infer|extends)\b", "#C586C0", "#AF00DB"),
            new("js_builtin", @"\b(?:console|window|document|Math|JSON|Array|Object|String|Number|Date|Promise|Map|Set|Error|RegExp|Symbol|Proxy|Reflect|globalThis)\b", "#4EC9B0", "#2B91AF"),
            new("js_number", @"\b\d+\.?\d*(?:[eE][+-]?\d+)?\b|0x[0-9a-fA-F]+|0b[01]+|0o[0-7]+", "#B5CEA8", "#098658"),
            new("js_function", @"\b([a-zA-Z_$][\w$]*)\s*(?=\()", "#DCDCAA", "#795E26"),
        },
        "python" => new()
        {
            new("py_string", @"""""""[\s\S]*?""""""|'''[\s\S]*?'''|f""(?:[^""\\]|\\.)*""|f'(?:[^'\\]|\\.)*'|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("py_comment", @"#.*$", "#6A9955", "#008000"),
            new("py_keyword", @"\b(?:and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|try|while|with|yield|True|False|None)\b", "#C586C0", "#AF00DB"),
            new("py_builtin", @"\b(?:print|len|range|int|str|float|list|dict|tuple|set|bool|type|input|open|map|filter|zip|enumerate|super|staticmethod|classmethod|property|isinstance|issubclass|getattr|setattr|hasattr|abs|min|max|sum|sorted|reversed|any|all|iter|next|hash|id|repr|format|dir|vars|locals|globals|exec|eval|compile|breakpoint|__\w+__)\b", "#4EC9B0", "#2B91AF"),
            new("py_number", @"\b\d+\.?\d*(?:[eE][+-]?\d+)?[jJ]?\b|0x[0-9a-fA-F]+|0b[01]+|0o[0-7]+", "#B5CEA8", "#098658"),
            new("py_decorator", @"@\w+", "#DCDCAA", "#795E26"),
        },
        "java" => new()
        {
            new("java_string", @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("java_comment", @"//.*$|/\*[\s\S]*?\*/", "#6A9955", "#008000"),
            new("java_keyword", @"\b(?:abstract|assert|boolean|break|byte|case|catch|char|class|const|continue|default|do|double|else|enum|extends|final|finally|float|for|goto|if|implements|import|instanceof|int|interface|long|native|new|package|private|protected|public|return|short|static|strictfp|super|switch|synchronized|this|throw|throws|transient|try|void|volatile|while|var|record|sealed|permits|yield)\b", "#569CD6", "#0000FF"),
            new("java_number", @"\b\d+\.?\d*(?:[eE][+-]?\d+)?[fFdDlL]?\b|0x[0-9a-fA-F]+", "#B5CEA8", "#098658"),
        },
        "cpp" => new()
        {
            new("cpp_string", @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("cpp_comment", @"//.*$|/\*[\s\S]*?\*/", "#6A9955", "#008000"),
            new("cpp_preprocessor", @"#\s*\w+.*$", "#C586C0", "#AF00DB"),
            new("cpp_keyword", @"\b(?:alignas|alignof|and|and_eq|asm|auto|bitand|bitor|bool|break|case|catch|char|char8_t|char16_t|char32_t|class|compl|concept|const|consteval|constexpr|constinit|const_cast|continue|co_await|co_return|co_yield|decltype|default|delete|do|double|dynamic_cast|else|enum|explicit|export|extern|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|noexcept|not|not_eq|nullptr|operator|or|or_eq|private|protected|public|register|reinterpret_cast|requires|return|short|signed|sizeof|static|static_assert|static_cast|struct|switch|template|this|thread_local|throw|try|typedef|typeid|typename|union|unsigned|using|virtual|void|volatile|wchar_t|while|xor|xor_eq|override|final)\b", "#569CD6", "#0000FF"),
            new("cpp_number", @"\b\d+\.?\d*(?:[eE][+-]?\d+)?[fFlLuU]*\b|0x[0-9a-fA-F]+", "#B5CEA8", "#098658"),
        },
        "json" => new()
        {
            new("json_string", @"""(?:[^""\\]|\\.)*""(?=\s*:)", "#9CDCFE", "#0451A5"),
            new("json_value_string", @"""(?:[^""\\]|\\.)*""(?!\s*:)", "#CE9178", "#A31515"),
            new("json_number", @"-?\b\d+\.?\d*(?:[eE][+-]?\d+)?\b", "#B5CEA8", "#098658"),
            new("json_keyword", @"\b(?:true|false|null)\b", "#569CD6", "#0000FF"),
        },
        "xml" or "html" or "xaml" => new()
        {
            new("xml_comment", @"<!--[\s\S]*?-->", "#6A9955", "#008000"),
            new("xml_tag", @"</?\w[\w.-]*(?:\s+[\w:.-]+(?:=(?:""[^""]*""|'[^']*'|[^\s>]*))?)*\s*/?>?", "#569CD6", "#0000FF"),
            new("xml_attr_name", @"[\w:.-]+(?==)", "#9CDCFE", "#0451A5"),
            new("xml_attr_value", @"(?:=""[^""]*""|='[^']*')", "#CE9178", "#A31515"),
            new("xml_cdata", @"<!\[CDATA\[[\s\S]*?\]\]>", "#B5CEA8", "#098658"),
        },
        "sql" => new()
        {
            new("sql_string", @"'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("sql_comment", @"--.*$|/\*[\s\S]*?\*/", "#6A9955", "#008000"),
            new("sql_keyword", @"\b(?:SELECT|FROM|WHERE|AND|OR|NOT|IN|INSERT|UPDATE|DELETE|CREATE|DROP|ALTER|TABLE|INDEX|VIEW|JOIN|INNER|LEFT|RIGHT|OUTER|FULL|CROSS|ON|AS|SET|VALUES|INTO|ORDER|BY|GROUP|HAVING|LIMIT|OFFSET|UNION|ALL|DISTINCT|NULL|IS|LIKE|BETWEEN|EXISTS|CASE|WHEN|THEN|ELSE|END|BEGIN|COMMIT|ROLLBACK|GRANT|REVOKE|PRIMARY|KEY|FOREIGN|REFERENCES|CONSTRAINT|UNIQUE|CHECK|DEFAULT|AUTO_INCREMENT|SERIAL|TRUE|FALSE)\b", "#569CD6", "#0000FF"),
            new("sql_function", @"\b(?:COUNT|SUM|AVG|MIN|MAX|COALESCE|NULLIF|CAST|CONVERT|CONCAT|LENGTH|SUBSTRING|TRIM|UPPER|LOWER|ROUND|FLOOR|CEIL|NOW|CURRENT_TIMESTAMP|DATE|EXTRACT|IF|IFNULL|NVL)\b", "#DCDCAA", "#795E26"),
            new("sql_number", @"\b\d+\.?\d*\b", "#B5CEA8", "#098658"),
        },
        "css" => new()
        {
            new("css_string", @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("css_comment", @"/\*[\s\S]*?\*/", "#6A9955", "#008000"),
            new("css_keyword", @"@(?:media|keyframes|import|charset|font-face|supports|layer|property|scope)\b", "#C586C0", "#AF00DB"),
            new("css_selector", @"[.#]?[\w-]+(?:\s*[:,.[>+~]\s*[\w.#-]*)*", "#D7BA7D", "#800000"),
            new("css_property", @"[\w-]+(?=\s*:)", "#9CDCFE", "#0451A5"),
            new("css_number", @"\b\d+\.?\d*(?:px|em|rem|%|vh|vw|vmin|vmax|ch|ex|deg|rad|turn|s|ms|fr)?\b", "#B5CEA8", "#098658"),
        },
        "bash" or "powershell" => new()
        {
            new("sh_string", @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'|\$'[^']*'", "#CE9178", "#A31515"),
            new("sh_comment", @"#.*$", "#6A9955", "#008000"),
            new("sh_keyword", @"\b(?:if|then|else|elif|fi|for|while|until|do|done|case|esac|in|function|return|exit|local|export|source|alias|unalias|set|unset|shift|trap|eval|exec|readonly|declare|typeset|let|cd|pwd|echo|printf|read|test)\b", "#C586C0", "#AF00DB"),
            new("sh_variable", @"\$[\w{]+[\w}]*|@\w+", "#9CDCFE", "#0451A5"),
            new("sh_number", @"\b\d+\b", "#B5CEA8", "#098658"),
        },
        "yaml" => new()
        {
            new("yaml_string", @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'", "#CE9178", "#A31515"),
            new("yaml_comment", @"#.*$", "#6A9955", "#008000"),
            new("yaml_key", @"^[\w][\w.-]*(?=:)", "#9CDCFE", "#0451A5"),
            new("yaml_keyword", @"\b(?:true|false|yes|no|null|~)\b", "#569CD6", "#0000FF"),
            new("yaml_number", @"\b\d+\.?\d*\b", "#B5CEA8", "#098658"),
            new("yaml_anchor", @"[&*]\w+", "#DCDCAA", "#795E26"),
        },
        "ini" => new()
        {
            new("ini_comment", @"[#;].*$", "#6A9955", "#008000"),
            new("ini_section", @"^\[.+\]$", "#569CD6", "#0000FF"),
            new("ini_key", @"^[\w.-]+(?==)", "#9CDCFE", "#0451A5"),
            new("ini_value", @"(?<==).*$", "#CE9178", "#A31515"),
        },
        _ => new List<SyntaxRule>()
    };

    static Windows.UI.Color ColorHelper(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = Convert.ToByte(hex[..2], 16);
            var g = Convert.ToByte(hex[2..4], 16);
            var b = Convert.ToByte(hex[4..6], 16);
            return Windows.UI.Color.FromArgb(255, r, g, b);
        }
        return Colors.White;
    }

    static bool IsDarkTheme() => Application.Current.RequestedTheme == ApplicationTheme.Dark;

    static string ThemeColor(string darkHex, string lightHex) => IsDarkTheme() ? darkHex : lightHex;
}
