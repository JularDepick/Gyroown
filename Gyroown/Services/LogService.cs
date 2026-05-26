using System.Text;

namespace Gyroown.Services;

/// <summary>
/// Log service: error log + runtime log, 100KB file slicing, date-range naming.
/// Output: %USERPROFILE%\.Gyroown\log\
/// </summary>
public static class LogService
{
    private static readonly string LogDir;
    private const int MaxFileSize = 100 * 1024; // 100KB
    private static readonly object _lock = new();

    static LogService()
    {
        LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "log");
        Directory.CreateDirectory(LogDir);
    }

    public static void Error(string message)
    {
        Write("error", message);
    }

    public static void Info(string message)
    {
        Write("run", message);
    }

    public static IReadOnlyList<LogFileInfo> GetErrorLogFiles()
    {
        return GetLogFiles("error");
    }

    public static string ReadErrorLogContent()
    {
        var sb = new StringBuilder();
        foreach (var f in GetErrorLogFiles().OrderBy(f => f.StartDate))
        {
            sb.AppendLine($"=== {f.FileName} ===");
            sb.AppendLine(File.ReadAllText(Path.Combine(LogDir, f.FileName)));
        }
        return sb.ToString();
    }

    public static IReadOnlyList<LogFileInfo> GetLogFiles(string prefix)
    {
        var list = new List<LogFileInfo>();
        if (!Directory.Exists(LogDir)) return list;

        foreach (var f in Directory.GetFiles(LogDir, $"{prefix}-*.txt"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var parts = name.Split('-');
            if (parts.Length >= 4 &&
                DateOnly.TryParse($"{parts[1]}-{parts[2]}-{parts[3]}", out var start) &&
                DateOnly.TryParse($"{parts[4]}-{parts[5]}-{parts[6]}", out var end))
            {
                list.Add(new LogFileInfo { FileName = Path.GetFileName(f), StartDate = start, EndDate = end });
            }
        }
        return list;
    }

    private static void Write(string prefix, string message)
    {
        lock (_lock)
        {
            try
            {
                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var line = $"[{now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";

                // Find current file
                var files = Directory.GetFiles(LogDir, $"{prefix}-*.txt")
                    .OrderBy(f => f)
                    .ToList();

                string? targetFile = null;
                foreach (var f in files)
                {
                    var fi = new FileInfo(f);
                    if (fi.Length < MaxFileSize)
                    {
                        targetFile = f;
                        break;
                    }
                }

                if (targetFile == null)
                {
                    targetFile = Path.Combine(LogDir, $"{prefix}-{today:yyyy-MM-dd}-{today:yyyy-MM-dd}.txt");
                }

                File.AppendAllText(targetFile, line, Encoding.UTF8);
            }
            catch { /* best-effort logging */ }
        }
    }

    public class LogFileInfo
    {
        public string FileName { get; init; } = "";
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
    }
}
