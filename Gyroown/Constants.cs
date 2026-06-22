namespace Gyroown;

/// <summary>
/// Project-wide constants. Change values here only — all references derive from this file.
/// </summary>
public static class Constants
{
    // ── Vault root ──
    public static readonly string VaultRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown");
    public static readonly string AuthDir = Path.Combine(VaultRoot, "auth");
    public static readonly string DataDir = Path.Combine(VaultRoot, "data");
    public static readonly string MetaDir = Path.Combine(VaultRoot, "meta");
    public static readonly string PreviewDir = Path.Combine(VaultRoot, "preview");
    public static readonly string VersionsDir = Path.Combine(VaultRoot, "versions");
    public static readonly string LogDir = Path.Combine(VaultRoot, "log");

    // ── Auth files ──
    public static readonly string PasswordFile = Path.Combine(AuthDir, ".gyropw");
    public static readonly string VaultKeyFile = Path.Combine(AuthDir, ".gyrock");
    public static readonly string InsuranceFile = Path.Combine(AuthDir, "insurance.gyrock");
    public static readonly string ImageKeyFile = Path.Combine(AuthDir, ".imgkey");
    public static readonly string ImageFile = Path.Combine(AuthDir, "image.pwimg");

    // ── Config files ──
    public static readonly string ConfigFile = Path.Combine(VaultRoot, "config.gyrojson");
    public static readonly string SettingsFile = Path.Combine(VaultRoot, "settings.gyrojson");
    public static readonly string FavoritesFile = Path.Combine(VaultRoot, "favorites.gyrojson");
    public static readonly string TreeFile = Path.Combine(MetaDir, ".tree.gyrojson");
    public static readonly string SearchHistoryFile = Path.Combine(VaultRoot, "search-history.gyrojson");

    // ── File extensions ──
    public const string ExtData = ".gyrodt";
    public const string ExtMeta = ".gyromt";
    public const string ExtPreview = ".gyropv";
    public const string ExtVersionData = ".gyroverdata";
    public const string ExtVersionMeta = ".gyrovermeta";
    public const string ExtJson = ".gyrojson";

    // ── Chunk naming ──
    public const string ChunkPrefix = "c";
    public const string ChunkFormat = "x4"; // hex, 4 digits

    // ── Crypto parameters ──
    public const int RsaKeySize = 2048;
    public const int AesKeySize = 32;
    public const int AesNonceSize = 12;
    public const int AesTagSize = 16;
    public const int Pbkdf2Iterations = 100_000;
    public const int UserKeySize = 32;
    public const int SaltSize = 32;
    public const int HashSize = 32;

    // ── Password policy ──
    public const int PinLength = 6;
    public const int GestureMinPoints = 4;
    public const int CustomMinLength = 6;
    public const int CustomMaxLength = 32;
    public const int PictureMinPoints = 3;
    public const int LockoutThreshold = 5;
    public const int LockoutDurationSec = 30;
    public const double PictureToleranceRatio = 0.05;

    // ── Storage ──
    public const long LargeFileWarningThreshold = 100L * 1024 * 1024; // 100 MB
    public const long PreviewMaxSize = 50L * 1024 * 1024; // 50 MB
    public const long PreviewMaxBytes = 1024 * 1024; // 1 MB JPEG
    public const int MaxVersions = 10;
    public const int MaxSearchHistory = 10;

    // ── UI ──
    public const int WindowDefaultWidth = 1600;
    public const int WindowDefaultHeight = 960;
    public const int WindowMinWidth = 800;
    public const int WindowMinHeight = 480;
    public const int SidebarDefaultWidth = 220;
    public const int SidebarMinWidth = 180;
    public const int SidebarMaxWidth = 400;
    public const double BannerHeight = 36;
    public const int BannerAutoHideMs = 3000;
    public const int AnimationDurationMs = 250;
    public const int BannerAnimationMs = 200;
    public const int ProgressAnimationMs = 300;
    public const int DragOutCleanupMs = 30_000;

    // ── Tray ──
    public const uint TrayCmdOpen = 1001;
    public const uint TrayCmdLock = 1002;
    public const uint TrayCmdExit = 1003;

    // ── Icon font ──
    public const string IconFontFamily = "Segoe MDL2 Assets";

    // ── Log ──
    public const long LogMaxSize = 200 * 1024; // 200 KB
}
