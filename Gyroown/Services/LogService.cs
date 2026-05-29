using System.Text;

namespace Gyroown.Services;

/// <summary>
/// Log service with subdirectory-based categorization and 200KB auto-slicing.
/// Output: %USERPROFILE%\.Gyroown\log\
///   log/error/   — error logs
///   log/crash/   — crash/exception logs
///   log/run/     — runtime/work logs
/// File naming: {prefix}-{start:yyyy-MM-dd}-{end:yyyy-MM-dd}.txt
/// Slicing: 200KB threshold, message-boundary aware (no mid-message splits).
/// </summary>
public enum LogLevel { Debug, Info, Warn, Error }

public static class LogService
{
    private static readonly string LogRoot;
    private static readonly string ErrorDir;
    private static readonly string CrashDir;
    private static readonly string RunDir;
    private const int MaxFileSize = 200 * 1024; // 200KB
    private static readonly object _lock = new();
    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    static LogService()
    {
        LogRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "log");
        ErrorDir = Path.Combine(LogRoot, "error");
        CrashDir = Path.Combine(LogRoot, "crash");
        RunDir = Path.Combine(LogRoot, "run");
        Directory.CreateDirectory(ErrorDir);
        Directory.CreateDirectory(CrashDir);
        Directory.CreateDirectory(RunDir);
    }

    public static void Error(string message) { if (MinLevel <= LogLevel.Error) Write(ErrorDir, "error", $"[ERROR] {message}"); }
    public static void Warn(string message) { if (MinLevel <= LogLevel.Warn) Write(RunDir, "run", $"[WARN] {message}"); }
    public static void Info(string message) { if (MinLevel <= LogLevel.Info) Write(RunDir, "run", $"[INFO] {message}"); }
    public static void Debug(string message) { if (MinLevel <= LogLevel.Debug) Write(RunDir, "run", $"[DEBUG] {message}"); }

    /// <summary>Log an unhandled exception to the crash directory.</summary>
    public static void Crash(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[CRASH] {context}");
        sb.AppendLine($"Type: {ex.GetType().FullName}");
        sb.AppendLine($"Message: {ex.Message}");
        sb.AppendLine($"Stack: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            sb.AppendLine($"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            sb.AppendLine($"Inner Stack: {ex.InnerException.StackTrace}");
        }
        Write(CrashDir, "crash", sb.ToString().TrimEnd());
    }

    public static IReadOnlyList<LogFileInfo> GetErrorLogFiles() => GetLogFiles(ErrorDir, "error");
    public static IReadOnlyList<LogFileInfo> GetCrashLogFiles() => GetLogFiles(CrashDir, "crash");
    public static IReadOnlyList<LogFileInfo> GetRunLogFiles() => GetLogFiles(RunDir, "run");

    public static string ReadErrorLogContent()
    {
        var sb = new StringBuilder();
        foreach (var f in GetErrorLogFiles().OrderBy(f => f.StartDate))
        {
            sb.AppendLine($"=== {f.FileName} ===");
            sb.AppendLine(File.ReadAllText(Path.Combine(ErrorDir, f.FileName)));
        }
        return sb.ToString();
    }

    public static IReadOnlyList<LogFileInfo> GetLogFiles(string dir, string prefix)
    {
        var list = new List<LogFileInfo>();
        if (!Directory.Exists(dir)) return list;

        foreach (var f in Directory.GetFiles(dir, $"{prefix}-*.txt"))
        {
            var name = Path.GetFileNameWithoutExtension(f);
            var parts = name.Split('-');
            if (parts.Length >= 7 &&
                DateOnly.TryParse($"{parts[1]}-{parts[2]}-{parts[3]}", out var start) &&
                DateOnly.TryParse($"{parts[4]}-{parts[5]}-{parts[6]}", out var end))
            {
                list.Add(new LogFileInfo { FileName = Path.GetFileName(f), StartDate = start, EndDate = end });
            }
        }
        return list;
    }

    private static void Write(string dir, string prefix, string message)
    {
        lock (_lock)
        {
            try
            {
                var now = DateTime.Now;
                var today = DateOnly.FromDateTime(now);
                var line = $"[{now:yyyy-MM-dd HH:mm:ss}]{(message.StartsWith("[CRASH]") ? "" : " ")}{message}{Environment.NewLine}";

                // Find current non-full file
                var files = Directory.GetFiles(dir, $"{prefix}-*.txt")
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
                    int seq = 1;
                    do
                    {
                        targetFile = Path.Combine(dir, $"{prefix}-{today:yyyy-MM-dd}-{today:yyyy-MM-dd}_{seq}.txt");
                        seq++;
                    } while (File.Exists(targetFile) && new FileInfo(targetFile).Length >= MaxFileSize);
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
