using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Gyroown.Views;
using Gyroown.Controls.Preview;
using Gyroown.Services;
using Gyroown.Models;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace Gyroown;

public sealed partial class MainWindow : Window
{
    private readonly PasswordService _pw;
    private readonly EncryptionService _enc;
    private readonly VaultService _vault;
    private readonly ThemeService _theme;
    private readonly DragDropService _drag;
    private readonly FavoritesService _favorites;
    private H.NotifyIcon.TaskbarIcon? _tray;
    private readonly CompositeTransform SuccessBannerTransform = new();
    private readonly CompositeTransform ErrorBannerTransform = new();
    private const double BannerHeight = 36;
    private DispatcherTimer? _autoLockTimer;
    private CancellationTokenSource? _batchCts;
    private Window? _previewWindow;
    private readonly List<IntegrityIssue> _integrityIssues = new();

    enum IntegrityIssueType { OrphanMeta, OrphanData, Undecryptable, Unbound }
    record IntegrityIssue(IntegrityIssueType Type, string Id, string Description);

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int n);
    private const int SW_HIDE = 0, SW_SHOW = 5, SW_RESTORE = 9;
    private IntPtr Hwnd => WindowNative.GetWindowHandle(this);

    public MainWindow(PasswordService pw, EncryptionService enc, VaultService vault)
    {
        _pw = pw; _enc = enc; _vault = vault;
        _theme = new ThemeService();
        _drag = new DragDropService(vault);
        _favorites = new FavoritesService();
        InitializeComponent();
        Title = AppInfo.Name;
        // Set window icon
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "favicon.ico");
            if (!File.Exists(iconPath)) iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "favicon.ico");
            if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);
        }
        catch { }
        Activated += (_, _) =>
        {
            SetTitleBar(TitleBar.GetDragElement());
            // Re-check lock state when window is restored from tray
            if (_vault.IsInitialized && _pw.IsLocked)
            {
                AuthOverlay.Visibility = Visibility.Visible;
                VaultContent.Visibility = Visibility.Collapsed;
                ShowUnlock();
            }
        };
        InitTray();

        // Intercept close �?minimize to tray
        AppWindow.Closing += (_, e) => { if (_busy) { e.Cancel = true; return; } e.Cancel = true; ShowWindow(Hwnd, SW_HIDE); };

        // Window size: default 1600×960, minimum 800×480
        var minW = 800; var minH = 480;
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1600, 960));
        SizeChanged += (_, _) =>
        {
            var sz = AppWindow.ClientSize;
            if (sz.Width < minW || sz.Height < minH)
                AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                    Math.Max(sz.Width, minW), Math.Max(sz.Height, minH)));
        };
        // Subscribe events once (not in ApplyToolbarLoc which re-runs on language change)
        FileList.ItemOpened += OnFileOpened;
        FileList.RenameRequested += OnRenameRequested;
        TitleBar.SearchChanged += q => FileList.Filter = q;
        TitleBar.FilterChanged += filter => FileList.SearchFilter = filter;
        TitleBar.RefreshRequested += (_, _) => RefreshList();
        TitleBar.IntegrityCheckRequested += (_, _) => _ = RunIntegrityCheck();
        TitleBar.SettingsRequested += (_, _) => OnSettingsCmd(null!, null!);
        Sidebar.FolderSelected += (_, path) =>
        {
            _vault.SetCurrentPath(path);
            RefreshList();
        };
        FileList.DecryptToFile = async (id, path) =>
        {
            try
            {
                await using var fs = File.Create(path);
                await _vault.ExportItemAsync(id, fs);
            }
            catch (Exception ex) { LogService.Error($"DecryptToFile failed: {ex}"); }
        };
        FileList.ExportRequested += async (_, item) =>
        {
            try
            {
                var p = new Windows.Storage.Pickers.FileSavePicker();
                WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
                p.SuggestedFileName = item.Name;
                var file = await p.PickSaveFileAsync();
                if (file != null) { await using var st = await file.OpenStreamForWriteAsync(); await _vault.ExportItemAsync(item.Id, st); }
            }
            catch (Exception ex) { LogService.Error($"Export failed: {ex}"); }
        };
        FileList.BatchDeleteRequested += async (_, items) =>
        {
            try { await ExecuteBatchDelete(items); }
            catch (Exception ex) { LogService.Error($"Batch delete failed: {ex}"); }
        };
        FileList.BatchExportRequested += async (_, items) =>
        {
            try { await ExecuteBatchExport(items); }
            catch (Exception ex) { LogService.Error($"Batch export failed: {ex}"); }
        };
        FileList.SelectionChanged += OnSelectionChanged;
        Sidebar.FavoritesPanel.Initialize(_favorites);
        Sidebar.FavoritesPanel.FavoriteNavigate += OnFavoriteNavigate;
        Sidebar.FavoritesPanel.FavoriteRemoved += (_, item) => { _favorites.Remove(item.ItemId); RefreshList(); };
        Sidebar.FavoritesPanel.FavoriteRenamed += async (_, item) =>
        {
            try { await OnFavoriteRename(item); }
            catch (Exception ex) { LogService.Error($"Favorite rename failed: {ex}"); }
        };
        Sidebar.FavoritesPanel.FavoriteMovedToGroup += (_, e) => { _favorites.MoveToGroup(e.Item.Id, e.NewGroup); };
        Sidebar.FavoritesPanel.GroupRenamed += (_, e) => { _favorites.RenameGroup(e.OldName, e.NewName); };
        Sidebar.FavoritesPanel.GroupDeleted += async (_, groupName) =>
        {
            try { await OnFavoriteGroupDelete(groupName); }
            catch (Exception ex) { LogService.Error($"Favorite group delete failed: {ex}"); }
        };
        FileList.FavoriteToggleRequested += OnFavoriteToggle;
        FileList.VersionHistoryRequested += OnVersionHistoryRequested;

        ApplyToolbarLoc(); ApplySettingsLoc();
        Loc.LanguageChanged += (_, _) => { ApplyToolbarLoc(); ApplySettingsLoc(); };
        _theme.ThemeChanged += (_, _) => ApplyTheme();

        foreach (var p in ThemeService.AccentPresets)
        {
            var btn = new Button { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Margin = new Thickness(2), Tag = p.Hex };
            ToolTipService.SetToolTip(btn, p.Name);
            var c = FromHex(p.Hex);
            btn.Background = new SolidColorBrush(c);
            btn.Click += OnAccentClick;
            if (p.Hex == _theme.AccentColor) { var checkFg = GetLuminance(c) > 0.5 ? Colors.Black : Colors.White; var check = new FontIcon { Glyph = "\uE73E", FontSize = 12, Foreground = new SolidColorBrush(checkFg) }; btn.Content = check; }
            AccentList.Children.Add(btn);
        }
        // Apply theme after content is loaded (RequestedTheme needs visual tree)
        if (Content is FrameworkElement fe) fe.Loaded += (_, _) => ApplyTheme();
        ApplyTheme();

        // Apply saved language
        Loc.Service.SetLanguage(_theme.Language);

        // Keyboard shortcuts
        SetupKeyboardShortcuts();
        InitAutoLock();

        // Defer auth flow until the visual tree is ready (XamlRoot available for dialogs)
        if (Content is FrameworkElement root) root.Loaded += (_, _) => _ = InitializeAuthFlow();
    }

    void SetupKeyboardShortcuts()
    {
        // Ctrl+F → Focus search
        var accelF = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.F, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        accelF.Invoked += (_, _) => TitleBar.FocusSearch();
        Content.KeyboardAccelerators.Add(accelF);

        // Ctrl+A → Select all
        var accelA = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.A, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        accelA.Invoked += (_, _) => FileList.SelectAll();
        Content.KeyboardAccelerators.Add(accelA);

        // Enter and Backspace via PreviewKeyDown on content
        Content.PreviewKeyDown += OnPreviewKeyDown;
    }

    void OnPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        ResetAutoLockTimer();
        // Skip if a text input is focused
        var focused = FocusManager.GetFocusedElement(Content.XamlRoot);
        if (focused is TextBox || focused is PasswordBox || focused is AutoSuggestBox) return;

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            // Open selected item
            var sel = FileList.SelectedItems;
            if (sel.Count == 1) OnFileOpened(this, sel[0]);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Back)
        {
            // Go to parent directory
            GoToParent();
            e.Handled = true;
        }
    }

    void GoToParent()
    {
        var current = _vault.CurrentPath;
        if (current == "/") return;
        var parent = System.IO.Path.GetDirectoryName(current.Replace('\\', '/'))?.Replace('\\', '/') ?? "/";
        if (string.IsNullOrEmpty(parent)) parent = "/";
        _vault.SetCurrentPath(parent);
        RefreshList();
    }

    // AutoLock
    void InitAutoLock()
    {
        _autoLockTimer = new DispatcherTimer();
        _autoLockTimer.Tick += OnAutoLockTick;

        Content.PointerMoved += (_, _) => ResetAutoLockTimer();
        Content.PointerPressed += (_, _) => ResetAutoLockTimer();

        _pw.Unlocked += (_, _) => ResetAutoLockTimer();

        ResetAutoLockTimer();
    }

    void LoadAutoLockFromConfig()
    {
        if (!_vault.IsInitialized) return;
        var cfg = _vault.GetConfig().Load();
        _pw.AutoLockTimeout = cfg.AutoLockTimeout;
        ResetAutoLockTimer();
    }

    void SaveAutoLockTimeout(int seconds)
    {
        _pw.AutoLockTimeout = seconds;
        if (!_vault.IsInitialized) return;
        var cfg = _vault.GetConfig().Load();
        cfg.AutoLockTimeout = seconds;
        _vault.GetConfig().Save(cfg);
        ResetAutoLockTimer();
    }

    void ResetAutoLockTimer()
    {
        _autoLockTimer?.Stop();
        var timeout = _pw.AutoLockTimeout;
        if (timeout > 0 && !_pw.IsLocked)
        {
            _autoLockTimer!.Interval = TimeSpan.FromSeconds(timeout);
            _autoLockTimer.Start();
        }
    }

    void OnAutoLockTick(object? sender, object e)
    {
        _autoLockTimer?.Stop();
        if (_pw.IsLocked || _busy) { ResetAutoLockTimer(); return; }
        _pw.Lock();
        AuthOverlay.Visibility = Visibility.Visible;
        VaultContent.Visibility = Visibility.Collapsed;
        ShowUnlock();
    }

    void InitTray()
    {
        try
        {
            System.Drawing.Icon? icon = null;

            // Try loading icon from file
            var icoPath = Path.Combine(AppContext.BaseDirectory, "favicon.ico");
            if (File.Exists(icoPath))
            {
                icon = new System.Drawing.Icon(icoPath);
                System.Diagnostics.Debug.WriteLine($"InitTray: icon loaded from {icoPath}");
            }

            // Fallback: extract icon from current executable
            if (icon == null)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    System.Diagnostics.Debug.WriteLine("InitTray: icon extracted from exe");
                }
            }

            if (icon == null)
            {
                System.Diagnostics.Debug.WriteLine("InitTray: no icon file found");
                return;
            }

            // Build right-click context menu
            var menu = new Microsoft.UI.Xaml.Controls.MenuFlyout();
            var openItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = Loc.Get("Tray", "Open") };
            openItem.Click += (_, _) => RestoreFromTray();
            var exitItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = Loc.Get("Tray", "Exit") };
            exitItem.Click += (_, _) => { _tray?.Dispose(); Application.Current.Exit(); };
            menu.Items.Add(openItem);
            menu.Items.Add(exitItem);

            _tray = new H.NotifyIcon.TaskbarIcon { ToolTipText = "Gyroown", Icon = icon };
            _tray.LeftClickCommand = new TrayCommand(() => RestoreFromTray());
            _tray.RightClickCommand = new TrayCommand(() =>
            {
                // Show context menu at cursor position
                GetCursorPos(out var pt);
                menu.XamlRoot = Content.XamlRoot;
                menu.ShowAt(Content, new Windows.Foundation.Point(pt.X, pt.Y));
            });

            // TaskbarIcon must be in the visual tree to initialize
            if (Content is Grid root)
                root.Children.Add(_tray);

            System.Diagnostics.Debug.WriteLine("InitTray: tray icon created successfully");
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"InitTray failed: {ex}"); }
    }

    void RestoreFromTray()
    {
        ShowWindow(Hwnd, SW_RESTORE);
        SetForegroundWindow(Hwnd);
        Activate();
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    private class TrayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        public TrayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    // Auth
    PasswordSetupControl? _setupControl;

    async Task InitializeAuthFlow()
    {
        try
        {
            if (!await CheckAuthIntegrity()) return;
            if (!_pw.IsPasswordSet) ShowSetup();
            else if (_pw.IsLocked) ShowUnlock();
            else ShowVault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeAuthFlow failed: {ex}");
            // Fallback: show setup if possible, otherwise the user sees the auth overlay
            try { if (!_pw.IsPasswordSet) ShowSetup(); } catch { }
        }
    }

    async Task<bool> CheckAuthIntegrity()
    {
        var authDir = VaultService.AuthDir;
        var gyrockPath = Path.Combine(authDir, ".gyrock");

        // First-time user: no auth dir and no password set — create dir and proceed to setup
        if (!Directory.Exists(authDir) && !_pw.IsPasswordSet)
        {
            Directory.CreateDirectory(authDir);
            VaultService.ProtectAuthDir();
            return true;
        }

        // Case 1: Auth directory missing (was previously set up)
        if (!Directory.Exists(authDir))
        {
            var dlg = new ContentDialog
            {
                Title = Loc.Get("MainWindow", "AuthDirMissing"),
                Content = new TextBlock { Text = Loc.Get("MainWindow", "AuthDirMissingMsg"), TextWrapping = TextWrapping.Wrap },
                CloseButtonText = Loc.Get("Common", "OK"),
                XamlRoot = Content.XamlRoot
            };
            await dlg.ShowAsync();
            Application.Current.Exit();
            return false;
        }

        // Case 2: Core key (.gyrock) missing
        if (!File.Exists(gyrockPath))
        {
            bool hasInsurance = InsuranceService.IsEnabled;
            var title = Loc.Get("MainWindow", "KeyLost");
            var msg = hasInsurance
                ? Loc.Get("MainWindow", "KeyLostInsuranceMsg")
                : Loc.Get("MainWindow", "KeyLostNoInsuranceMsg");

            var dlg = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = msg, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = Loc.Get("Common", "OK"),
                XamlRoot = Content.XamlRoot
            };
            await dlg.ShowAsync();
            if (!hasInsurance) Application.Current.Exit();
            return false;
        }

        return true;
    }

    void ShowSetup()
    {
        _setupControl = new PasswordSetupControl(_pw);
        _setupControl.SetupCompleted += async (_, cred) => await FinalizeSetup(cred);
        AuthHost.Content = _setupControl;
    }

    async Task FinalizeSetup(object cred)
    {
        var salt = _pw.GetStoredSalt()!;
        var uk = _enc.DeriveUserKey(CredStr(cred), salt);
        var kp = _enc.GenerateVaultKeyPair();
        var d = VaultService.AuthDir;
        Directory.CreateDirectory(d);
        File.WriteAllBytes(Path.Combine(d, ".gyrock"), _enc.EncryptVaultKeyPair(kp, uk));
        VaultService.ProtectAuthDir();

        if (InsuranceService.IsEnabled)
        {
            var insKp = _enc.GenerateInsuranceKeyPair();
            InsuranceService.SaveLocal(_enc.EncryptVaultKeyForInsurance(kp, insKp.PublicKey));
            var email = _setupControl?.CapturedInsuranceEmail;
            var token = _setupControl?.CapturedInsuranceToken;
            if (email != null && token != null)
                _ = UploadInsuranceAsync(email, token, insKp.PrivateKey);
        }

        _setupControl = null;
        _vault.Initialize(kp.PrivateKey, kp.PublicKey);
        _theme.Initialize(kp.PrivateKey);
        _favorites.Initialize(kp.PrivateKey);
        _favorites.Load();
        TitleBar.Initialize(kp.PrivateKey);
        LoadAutoLockFromConfig();
        AuthOverlay.Visibility = Visibility.Collapsed;
        VaultContent.Visibility = Visibility.Visible;
        RefreshList();
        _ = RunIntegrityCheck();
    }

    async Task UploadInsuranceAsync(string email, string token, byte[] insPriv)
    {
        try
        {
            await InsuranceService.UploadAsync(email, token, insPriv);
        }
        catch { /* upload is non-critical; insurance blob is already saved locally */ }
    }

    void ShowUnlock()
    {
        var c = new UnlockControl(_pw, _vault.IsInitialized ? _vault.GetConfig() : null);
        c.Unlocked += (_, r) =>
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "auth");
            var enc = File.ReadAllBytes(Path.Combine(d, ".gyrock"));
            var kp = _enc.DecryptVaultKeyPair(enc, r.UserKey!);
            _vault.Initialize(kp.PrivateKey, kp.PublicKey);
            _theme.Initialize(kp.PrivateKey);
            _favorites.Initialize(kp.PrivateKey);
            _favorites.Load();
            TitleBar.Initialize(kp.PrivateKey);
            LoadAutoLockFromConfig();
            AuthOverlay.Visibility = Visibility.Collapsed;
            VaultContent.Visibility = Visibility.Visible;
            RefreshList();
            _ = RunIntegrityCheck();
        };
        AuthHost.Content = c;
    }

    void ShowVault() { AuthOverlay.Visibility = Visibility.Collapsed; VaultContent.Visibility = Visibility.Visible; RefreshList(); InitChunkSlider(); _ = RunIntegrityCheck(); }

    async Task RunIntegrityCheck()
    {
        await Task.Delay(800);
        _integrityIssues.Clear();

        if (!VaultService.AreDataAndMetaBound())
        {
            _integrityIssues.Add(new IntegrityIssue(IntegrityIssueType.Unbound, "", Loc.Get("MainWindow", "IntegrityUnbound")));
        }
        else
        {
            var (orphanMeta, orphanData, paired, undecryptable) = _vault.CheckIntegrity();
            foreach (var id in orphanMeta)
                _integrityIssues.Add(new IntegrityIssue(IntegrityIssueType.OrphanMeta, id, Loc.Format("MainWindow", "IntegrityOrphanMeta", id)));
            foreach (var id in orphanData)
                _integrityIssues.Add(new IntegrityIssue(IntegrityIssueType.OrphanData, id, Loc.Format("MainWindow", "IntegrityOrphanData", id)));
            foreach (var id in undecryptable)
                _integrityIssues.Add(new IntegrityIssue(IntegrityIssueType.Undecryptable, id, Loc.Format("MainWindow", "IntegrityUndecryptableItem", id)));
            if (orphanMeta.Count + orphanData.Count + undecryptable.Count > 0)
                LogService.Warn($"Integrity: {orphanMeta.Count} orphan meta, {orphanData.Count} orphan data, {undecryptable.Count} undecryptable");
        }

        if (_integrityIssues.Count > 0)
        {
            ShowErrorBanner(Loc.Format("MainWindow", "IntegrityIssueSummary", _integrityIssues.Count));
        }
    }

    private ContentDialog? _activeDialog;

    async Task<ContentDialogResult> ShowDialog(string title, string msg, string? primary = null, string? secondary = null)
    {
        if (_activeDialog != null) return ContentDialogResult.None;
        try
        {
            var d = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = msg, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = primary == null ? Loc.Get("Common", "OK") : null,
                PrimaryButtonText = primary,
                SecondaryButtonText = secondary,
                XamlRoot = Content.XamlRoot
            };
            _activeDialog = d;
            var result = await d.ShowAsync();
            _activeDialog = null;
            return result;
        }
        catch { _activeDialog = null; return ContentDialogResult.None; }
    }
    // Must match PasswordService.SerializeCredential output exactly — different format
    // produces a different PBKDF2-derived user key, permanently locking out the vault.
    static string CredStr(object c) => c switch
    {
        string s => s,
        int[] a => string.Join(",", a),
        Array arr when arr.Length > 0 && arr.GetValue(0) is ValueTuple<double, double>
            => string.Join(";", arr.Cast<(double X, double Y)>().Select(t => $"{t.X},{t.Y}")),
        Array arr => string.Join(";", arr.Cast<object>().Select(o => o?.ToString() ?? "")),
        _ => c.ToString() ?? ""
    };

    // ┢�┢� Localization ┢�┢�
    void ApplyToolbarLoc()
    {
        BtnNewFolder.Label = Loc.Get("MainWindow", "NewFolder"); BtnImport.Label = Loc.Get("MainWindow", "Import");
        BtnExport.Label = Loc.Get("MainWindow", "Export"); BtnDelete.Label = Loc.Get("MainWindow", "Delete");
        BtnLock.Label = Loc.Get("MainWindow", "Lock");
        BtnMoveIn.Label = Loc.Get("MainWindow", "MoveIn");
        BtnMoveOut.Label = Loc.Get("MainWindow", "MoveOut");
    }

    private static readonly int[] AutoLockValues = { 0, 60, 300, 600, 900, 1800 };

    void ApplySettingsLoc()
    {
        SettingsTitle.Text = Loc.Get("SettingsWindow", "Title");
        ThemeLabel.Text = Loc.Get("SettingsWindow", "Theme");
        AccentLabel.Text = Loc.Get("SettingsWindow", "Accent");
        LangLabel.Text = Loc.Get("SettingsWindow", "Language");
        PwLabel.Text = Loc.Get("SettingsWindow", "Password");
        ChangePwBtn.Content = Loc.Get("SettingsWindow", "ChangePassword");
        ChunkLabel.Text = Loc.Get("SettingsWindow", "ChunkSize");
        VaultLabel.Text = Loc.Get("SettingsWindow", "Vault");
        AboutLabel.Text = Loc.Get("SettingsWindow", "About");
        AboutText.Text = Loc.Get("SettingsWindow", "AboutText");
        VersionText.Text = AppInfo.FullVersion;
        GitHubLink.Content = Loc.Get("SettingsWindow", "GitHub");
        VaultPathText.Text = _vault.VaultPath;
        RefreshErrorLogSection();
        ThemeCombo.Items.Clear();
        ThemeCombo.Items.Add(new ComboBoxItem { Tag = AppTheme.Default, Content = Loc.Get("SettingsWindow", "ThemeDefault") });
        ThemeCombo.Items.Add(new ComboBoxItem { Tag = AppTheme.Light, Content = Loc.Get("SettingsWindow", "ThemeLight") });
        ThemeCombo.Items.Add(new ComboBoxItem { Tag = AppTheme.Dark, Content = Loc.Get("SettingsWindow", "ThemeDark") });
        var themeIdx = _theme.GetAvailableThemes().ToList().IndexOf(_theme.CurrentTheme);
        if (themeIdx >= 0) ThemeCombo.SelectedIndex = themeIdx;
        if (LangCombo.Items.Count == 0)
        {
            foreach (var (code, name) in AppInfo.SupportedLanguages)
                LangCombo.Items.Add(new ComboBoxItem { Tag = code, Content = name });
        }
        var selIdx = AppInfo.SupportedLanguages.ToList().FindIndex(l => l.Code == _theme.Language);
        if (selIdx >= 0) LangCombo.SelectedIndex = selIdx;

        // Auto-lock combo
        AutoLockLabel.Text = Loc.Get("SettingsWindow", "AutoLock");
        if (AutoLockCombo.Items.Count == 0)
        {
            AutoLockCombo.Items.Add(new ComboBoxItem { Tag = 0, Content = Loc.Get("SettingsWindow", "AutoLockDisabled") });
            for (int i = 1; i < AutoLockValues.Length; i++)
                AutoLockCombo.Items.Add(new ComboBoxItem { Tag = AutoLockValues[i], Content = Loc.Format("SettingsWindow", "AutoLockValue", AutoLockValues[i] / 60) });
        }
        var autoLockIdx = Array.IndexOf(AutoLockValues, _pw.AutoLockTimeout);
        if (autoLockIdx >= 0) AutoLockCombo.SelectedIndex = autoLockIdx;

        // Preview toggle
        PreviewLabel.Text = Loc.Get("SettingsWindow", "GeneratePreview");
        if (_vault.IsInitialized)
        {
            var cfg = _vault.GetConfig().Load();
            PreviewToggle.IsOn = cfg.GeneratePreviews;
        }
    }

    // ┢�┢� Settings panel ┢�┢�
    void OnSettingsCmd(object s, RoutedEventArgs e)
    {
        InitChunkSlider();
        RefreshErrorLogSection();
        SettingsPanel.Visibility = Visibility.Visible;
    }

    void OnSettingsClose(object s, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Collapsed;
    void OnThemeSel(object s, SelectionChangedEventArgs e) { if (ThemeCombo.SelectedItem is ComboBoxItem ci && ci.Tag is AppTheme t) _theme.SetTheme(t); }
    void OnLangSel(object s, SelectionChangedEventArgs e) { if (LangCombo.SelectedItem is ComboBoxItem ci && ci.Tag is string t) { Loc.Service.SetLanguage(t); _theme.SetLanguage(t); } }
    void OnAutoLockSel(object s, SelectionChangedEventArgs e) { if (AutoLockCombo.SelectedItem is ComboBoxItem ci && ci.Tag is int sec) SaveAutoLockTimeout(sec); }
    void OnPreviewToggled(object s, RoutedEventArgs e)
    {
        if (!_vault.IsInitialized || PreviewToggle == null) return;
        var cfg = _vault.GetConfig().Load();
        cfg.GeneratePreviews = PreviewToggle.IsOn;
        _vault.GetConfig().Save(cfg);
    }

    void InitChunkSlider()
    {
        if (!_vault.IsInitialized) return;
        var cfg = _vault.GetConfig().Load();
        ChunkSlider.Value = cfg.ChunkTier;
        ChunkValue.Text = Loc.Format("SettingsWindow", "ChunkValue", ConfigService.ChunkTiers[cfg.ChunkTier]);
    }

    async void OnChunkChanged(object s, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ChunkValue == null || !_vault.IsInitialized) return;
        try
        {
            var tier = (int)Math.Round(e.NewValue);

            if (tier == 6)
            {
                var d = new ContentDialog { Title = Loc.Get("SettingsWindow", "ChunkWarnTitle"), Content = new TextBlock { Text = Loc.Get("SettingsWindow", "ChunkWarnMsg"), TextWrapping = TextWrapping.Wrap }, PrimaryButtonText = Loc.Get("Common", "OK"), CloseButtonText = Loc.Get("Common", "Cancel"), XamlRoot = Content.XamlRoot };
                if (await d.ShowAsync() != ContentDialogResult.Primary)
                { ChunkSlider.Value = 5; return; }
            }

            var mb = ConfigService.ChunkTiers[tier];
            ChunkValue.Text = Loc.Format("SettingsWindow", "ChunkValue", mb);
            var cfg = _vault.GetConfig().Load();
            cfg.ChunkTier = tier;
            _vault.GetConfig().Save(cfg);
        }
        catch (Exception ex) { LogService.Error($"Chunk size change failed: {ex}"); }
    }

    void OnAccentClick(object s, RoutedEventArgs e)
    {
        if (s is Button b && b.Tag is string hex)
        {
            _theme.SetAccentColor(hex);
            ApplyAccent(hex);
        }
    }

    // ┢�┢� Theme ┢�┢�
    void ApplyTheme()
    {
        var theme = _theme.CurrentTheme switch { AppTheme.Light => ElementTheme.Light, AppTheme.Dark => ElementTheme.Dark, _ => ElementTheme.Default };
        if (Content is FrameworkElement root) root.RequestedTheme = theme;

        // Override title bar colors so dark mode works even when OS is light
        var isDark = theme == ElementTheme.Dark ||
            (theme == ElementTheme.Default && Application.Current.RequestedTheme == Microsoft.UI.Xaml.ApplicationTheme.Dark);

        var bgColor = isDark ? Windows.UI.Color.FromArgb(255, 32, 32, 32) : Windows.UI.Color.FromArgb(255, 243, 243, 243);
        var fgColor = isDark ? Windows.UI.Color.FromArgb(255, 255, 255, 255) : Windows.UI.Color.FromArgb(255, 0, 0, 0);
        var btnHover = isDark ? Windows.UI.Color.FromArgb(255, 50, 50, 50) : Windows.UI.Color.FromArgb(255, 230, 230, 230);

        try
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.BackgroundColor = bgColor;
            titleBar.ForegroundColor = fgColor;
            titleBar.InactiveBackgroundColor = bgColor;
            titleBar.InactiveForegroundColor = fgColor;
            titleBar.ButtonBackgroundColor = bgColor;
            titleBar.ButtonForegroundColor = fgColor;
            titleBar.ButtonHoverBackgroundColor = btnHover;
            titleBar.ButtonHoverForegroundColor = fgColor;
            titleBar.ButtonPressedBackgroundColor = bgColor;
            titleBar.ButtonPressedForegroundColor = fgColor;
        }
        catch { }

        ApplyAccent(_theme.AccentColor);
    }

    void ApplyAccent(string hex)
    {
        var color = FromHex(hex);
        var brush = new SolidColorBrush(color);
        StatusBar.SetAccentBrush(brush);
    }

    static Windows.UI.Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return hex.Length == 6
            ? Windows.UI.Color.FromArgb(255, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex[2..4], 16), Convert.ToByte(hex[4..6], 16))
            : Microsoft.UI.Colors.DodgerBlue;
    }

    // ── Sidebar splitter ──
    private double _splitterStartX;
    private bool _splitterActive;

    void OnSplitterPressed(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (s is not UIElement el) return;
        _splitterStartX = e.GetCurrentPoint(el).Position.X;
        el.CapturePointer(e.Pointer);
        _splitterActive = true;
    }

    void OnSplitterMoved(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_splitterActive || s is not UIElement el) return;
        var dx = e.GetCurrentPoint(el).Position.X - _splitterStartX;
        var newWidth = Math.Max(56, Math.Min(400, SidebarBorder.Width + dx));
        SidebarBorder.Width = newWidth;
        _splitterStartX = e.GetCurrentPoint(el).Position.X;
    }

    void OnSplitterReleased(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (s is UIElement el) el.ReleasePointerCapture(e.Pointer);
        _splitterActive = false;
    }

    void OnSplitterEntered(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Controls.CursorHelper.ShowResize();
    }

    void OnSplitterExited(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Controls.CursorHelper.ShowArrow();
    }

    // ── Window ──
    private bool _busy;

    // ┢�┢� Commands ┢�┢�
    void RefreshList()
    {
        var items = _vault.ListItems(_vault.CurrentPath);
        foreach (var item in items)
            item.IsFavorited = _favorites.IsFavorited(item.Id);
        FileList.FilterPath = _vault.CurrentPath;
        FileList.SetItems(items);
        StatusBar.SetItemCount(items.Count);
        StatusBar.SetVaultPath(_vault.CurrentPath);
        _ = FileList.LoadPreviewsAsync(_vault);
        Sidebar.BuildTree(_vault.GetFolderTree());
        Sidebar.FavoritesPanel.Refresh();
    }

    void OnFavoriteToggle(object? sender, VaultFileItem item)
    {
        _favorites.Toggle(item.Id, item.Name, item.VirtualPath, item.IsFolder, item.ContentType);
        item.IsFavorited = _favorites.IsFavorited(item.Id);
        Sidebar.FavoritesPanel.Refresh();
    }

    async void OnVersionHistoryRequested(object? sender, VaultFileItem item)
    {
        if (_activeDialog != null) return;
        try
        {
            var dialog = new Views.VersionHistoryDialog(_vault, item.Id, item.Name);
            _activeDialog = dialog;

            dialog.VersionRestored += (_, _) =>
            {
                ShowSuccessBanner(Loc.Get("VersionHistory", "Restored"));
                RefreshList();
            };

            dialog.VersionsCleaned += (_, count) =>
            {
                ShowSuccessBanner(string.Format(Loc.Get("VersionHistory", "Cleaned"), count));
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex) { LogService.Error($"Version history failed: {ex}"); }
        finally { _activeDialog = null; }
    }

    void OnFavoriteNavigate(object? sender, FavoriteItem fav)
    {
        var targetPath = fav.IsFolder ? fav.ItemPath : (Path.GetDirectoryName(fav.ItemPath.Replace('\\', '/'))?.Replace('\\', '/') ?? "/");
        if (string.IsNullOrEmpty(targetPath)) targetPath = "/";
        _vault.SetCurrentPath(targetPath);
        RefreshList();
    }

    async Task OnFavoriteRename(FavoriteItem fav)
    {
        if (_activeDialog != null) return;
        var input = new TextBox { Text = fav.Name };
        var d = new ContentDialog
        {
            Title = Loc.Get("Favorites", "Rename"),
            Content = input,
            PrimaryButtonText = Loc.Get("Common", "OK"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = Content.XamlRoot
        };
        _activeDialog = d;
        var r = await d.ShowAsync();
        _activeDialog = null;
        if (r == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            _favorites.Rename(fav.Id, input.Text);
    }

    async Task OnFavoriteGroupDelete(string groupName)
    {
        var d = new ContentDialog
        {
            Title = Loc.Get("Favorites", "DeleteGroup"),
            Content = new TextBlock { Text = Loc.Format("Favorites", "DeleteGroupConfirm", groupName), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = Loc.Get("Common", "Delete"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = Content.XamlRoot
        };
        if (await d.ShowAsync() == ContentDialogResult.Primary)
            _favorites.DeleteGroup(groupName);
    }

    async void OnImportCmd(object s, RoutedEventArgs e)
    {
        if (_busy) return;
        var p = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        p.FileTypeFilter.Add("*");
        var files = await p.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        // Get file sizes for progress tracking
        var fileSizes = new long[files.Count];
        long totalSize = 0;
        for (int i = 0; i < files.Count; i++)
        {
            var prop = await files[i].GetBasicPropertiesAsync();
            fileSizes[i] = (long)prop.Size;
            totalSize += fileSizes[i];
        }
        var drive = new DriveInfo(Path.GetPathRoot(_vault.VaultPath) ?? "C:\\");
        if (drive.AvailableFreeSpace < totalSize)
        {
            await Info(Loc.Get("MainWindow", "DiskSpaceInsufficient"));
            return;
        }

        _busy = true;
        ShowProgress(Loc.Get("MainWindow", "Import"), Loc.Format("MainWindow", "BatchProgress", 0, files.Count));
        var errors = new List<string>();
        var completedSize = 0L;
        var lastUpdate = DateTime.MinValue;
        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                try
                {
                    await using var st = await f.OpenStreamForReadAsync();
                    var fileIdx = i;
                    var progress = new Progress<double>(pct =>
                    {
                        var now = DateTime.Now;
                        if ((now - lastUpdate).TotalMilliseconds < 500 && pct < 1.0) return;
                        lastUpdate = now;
                        var current = completedSize + (long)(pct * fileSizes[fileIdx]);
                        var overall = totalSize > 0 ? (double)current / totalSize : 0;
                        UpdateProgress(overall, $"{f.Name} — {FormatBytes(current)} / {FormatBytes(totalSize)}");
                    });
                    await _vault.ImportItemAsync(st, f.Name, progress: progress);
                }
                catch (Exception ex) { errors.Add($"{f.Name}: {ex.Message}"); LogService.Error($"Import failed for {f.Name}: {ex}"); }
                completedSize += fileSizes[i];
                UpdateProgress((double)completedSize / totalSize, Loc.Format("MainWindow", "BatchProgressFile", i + 1, files.Count, f.Name));
            }
        }
        finally
        {
            _busy = false;
            HideProgress();
            RefreshList();
            if (errors.Count > 0)
                ShowErrorBanner(string.Join("\n", errors));
        }
    }

    async void OnExportCmd(object s, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            var sel = FileList.SelectedItems;
            if (sel.Count == 0) { await Info(Loc.Get("MainWindow", "ExportSelectHint")); return; }
            await ExecuteBatchExport(sel);
        }
        catch (Exception ex) { LogService.Error($"Export failed: {ex}"); }
    }

    async Task ExecuteBatchExport(IReadOnlyList<Models.VaultFileItem> items)
    {
        if (items.Count == 0) return;
        var p = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        var folder = await p.PickSingleFolderAsync();
        if (folder == null) return;

        var totalSize = items.Sum(i => i.OriginalSize);
        _busy = true;
        _batchCts = new CancellationTokenSource();
        var errors = new List<(string Name, string Error)>();
        var completedSize = 0L;
        var lastUpdate = DateTime.MinValue;
        try
        {
            ShowProgress(Loc.Get("MainWindow", "Export"), Loc.Format("MainWindow", "BatchProgress", 0, items.Count), true);
            for (int i = 0; i < items.Count; i++)
            {
                if (_batchCts.Token.IsCancellationRequested) break;
                try
                {
                    var itemSize = items[i].OriginalSize;
                    var fileIdx = i;
                    var progress = new Progress<double>(pct =>
                    {
                        var now = DateTime.Now;
                        if ((now - lastUpdate).TotalMilliseconds < 500 && pct < 1.0) return;
                        lastUpdate = now;
                        var current = completedSize + (long)(pct * itemSize);
                        var overall = totalSize > 0 ? (double)current / totalSize : 0;
                        UpdateProgress(overall, $"{items[fileIdx].Name} — {FormatBytes(current)} / {FormatBytes(totalSize)}");
                    });
                    var f = await folder.CreateFileAsync(items[i].Name, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                    await using var st = await f.OpenStreamForWriteAsync();
                    await _vault.ExportItemAsync(items[i].Id, st, progress, _batchCts.Token);
                }
                catch (Exception ex) { errors.Add((items[i].Name, ex.Message)); }
                completedSize += items[i].OriginalSize;
                UpdateProgress((double)completedSize / totalSize, Loc.Format("MainWindow", "BatchProgressFile", i + 1, items.Count, items[i].Name));
            }
        }
        finally { _busy = false; _batchCts?.Dispose(); _batchCts = null; HideProgress(); }

        await ReportBatchResult(Loc.Get("MainWindow", "Export"), items.Count, errors);
    }

    // ┢�┢� Move (cut) ┢�┢�
    async void OnMoveInCmd(object s, RoutedEventArgs e)
    {
        if (_busy) return;
        var p = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        p.FileTypeFilter.Add("*");
        var files = await p.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        _busy = true;
        ShowProgress(Loc.Get("MainWindow", "MoveIn"), Loc.Format("MainWindow", "BatchProgress", 0, files.Count));
        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                await using var st = await f.OpenStreamForReadAsync();
                await _vault.ImportItemAsync(st, f.Name);
                UpdateProgress((double)(i + 1) / files.Count, Loc.Format("MainWindow", "BatchProgressFile", i + 1, files.Count, f.Name));
                // Delete original after successful import
                try { await f.DeleteAsync(); } catch { /* original might be locked */ }
            }
        }
        finally { _busy = false; HideProgress(); RefreshList(); }
    }

    async void OnMoveOutCmd(object s, RoutedEventArgs e)
    {
        if (_busy) return;
        var sel = FileList.SelectedItems;
        if (sel.Count == 0) { await Info(Loc.Get("MainWindow", "ExportSelectHint")); return; }
        var p = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        var folder = await p.PickSingleFolderAsync();
        if (folder == null) return;

        _busy = true;
        _batchCts = new CancellationTokenSource();
        var errors = new List<(string Name, string Error)>();
        var items = sel.ToList();
        try
        {
            ShowProgress(Loc.Get("MainWindow", "MoveOut"), Loc.Format("MainWindow", "BatchProgress", 0, items.Count), true);
            for (int i = 0; i < items.Count; i++)
            {
                if (_batchCts.Token.IsCancellationRequested) break;
                try
                {
                    UpdateProgress((double)i / items.Count, Loc.Format("MainWindow", "BatchProgressFile", i + 1, items.Count, items[i].Name));
                    var f = await folder.CreateFileAsync(items[i].Name, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                    await using var st = await f.OpenStreamForWriteAsync();
                    await _vault.ExportItemAsync(items[i].Id, st, progress: null, _batchCts.Token);
                    _vault.DeleteItem(items[i].Id);
                }
                catch (Exception ex) { errors.Add((items[i].Name, ex.Message)); }
            }
        }
        finally { _busy = false; _batchCts?.Dispose(); _batchCts = null; HideProgress(); RefreshList(); }

        await ReportBatchResult(Loc.Get("MainWindow", "MoveOut"), items.Count, errors);
    }

    async void OnDropIn(object? s, IReadOnlyList<string> paths)
    {
        _busy = true;
        ShowProgress(Loc.Get("MainWindow", "Import"), Loc.Format("MainWindow", "BatchProgress", 0, paths.Count));
        var errors = new List<string>();
        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                var p = paths[i];
                try
                {
                    await using var fs = File.OpenRead(p);
                    await _vault.ImportItemAsync(fs, Path.GetFileName(p));
                }
                catch (Exception ex) { errors.Add($"{Path.GetFileName(p)}: {ex.Message}"); LogService.Error($"Drop import failed for {p}: {ex}"); }
                UpdateProgress((double)(i + 1) / paths.Count, Loc.Format("MainWindow", "BatchProgressFile", i + 1, paths.Count, Path.GetFileName(p)));
            }
        }
        finally
        {
            _busy = false;
            HideProgress();
            RefreshList();
            if (errors.Count > 0)
                ShowErrorBanner(string.Join("\n", errors));
        }
    }

    async void OnDeleteCmd(object s, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            var sel = FileList.SelectedItems;
            if (sel.Count == 0) return;
            await ExecuteBatchDelete(sel);
        }
        catch (Exception ex) { LogService.Error($"Delete failed: {ex}"); }
    }

    async Task ExecuteBatchDelete(IReadOnlyList<Models.VaultFileItem> items)
    {
        if (items.Count == 0) return;
        var d = new ContentDialog
        {
            Title = Loc.Get("MainWindow", "Delete"),
            Content = new TextBlock { Text = Loc.Format("MainWindow", "DeleteConfirm", items.Count) },
            PrimaryButtonText = Loc.Get("Common", "Delete"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = Content.XamlRoot
        };
        if (await d.ShowAsync() != ContentDialogResult.Primary) return;

        _busy = true;
        _batchCts = new CancellationTokenSource();
        var errors = new List<(string Name, string Error)>();
        try
        {
            ShowProgress(Loc.Get("MainWindow", "Delete"), Loc.Format("MainWindow", "BatchProgress", 0, items.Count), true);
            for (int i = 0; i < items.Count; i++)
            {
                if (_batchCts.Token.IsCancellationRequested) break;
                try
                {
                    UpdateProgress((double)i / items.Count, Loc.Format("MainWindow", "BatchProgressFile", i + 1, items.Count, items[i].Name));
                    _vault.DeleteItem(items[i].Id);
                }
                catch (Exception ex) { errors.Add((items[i].Name, ex.Message)); }
            }
            FileList.RemoveItems(items);
            RefreshList();
        }
        finally { _busy = false; _batchCts?.Dispose(); _batchCts = null; HideProgress(); }

        await ReportBatchResult(Loc.Get("MainWindow", "Delete"), items.Count, errors);
    }

    async void OnLockCmd(object s, RoutedEventArgs e) { if (_busy) return; _busy = true; try { _pw.Lock(); AuthOverlay.Visibility = Visibility.Visible; VaultContent.Visibility = Visibility.Collapsed; ShowUnlock(); } finally { _busy = false; } }

    async void OnNewFolderCmd(object s, RoutedEventArgs e)
    {
        try { _vault.CreateFolder(Loc.Get("MainWindow", "NewFolderDefault")); RefreshList(); }
        catch (Exception ex) { LogService.Error($"New folder failed: {ex}"); }
    }

    void OnFileOpened(object? s, Models.VaultFileItem item) => _ = OpenViewer(item);

    async Task OpenViewer(Models.VaultFileItem item)
    {
        if (item.IsFolder) return;

        // Close existing preview window
        if (_previewWindow != null)
        {
            try { _previewWindow.Close(); } catch { }
            _previewWindow = null;
        }

        // Get all non-folder items in current directory for file switching
        var files = FileList.GetVisibleFileItems();
        var startIndex = files.ToList().FindIndex(f => f.Id == item.Id);
        if (startIndex < 0) startIndex = 0;

        // Create preview window with content loader
        var preview = new FilePreviewWindow(
            _vault,
            files,
            startIndex,
            async (itemId) =>
            {
                using var ms = new MemoryStream();
                await _vault.ExportItemAsync(itemId, ms);
                var fileItem = files.FirstOrDefault(f => f.Id == itemId);
                return (fileItem?.Name ?? "unknown", ms.ToArray());
            });
        _previewWindow = preview;
        preview.Closed += (_, _) => _previewWindow = null;
        preview.Activate();
    }

    async void OnRenameRequested(object? s, Models.VaultFileItem item)
    {
        if (_activeDialog != null) return;
        try
        {
            var input = new TextBox { Text = item.Name };
            var d = new ContentDialog { Title = Loc.Get("MainWindow", "Rename"), Content = input, PrimaryButtonText = Loc.Get("Common", "OK"), CloseButtonText = Loc.Get("Common", "Cancel"), XamlRoot = Content.XamlRoot };
            _activeDialog = d;
            var r = await d.ShowAsync();
            _activeDialog = null;
            if (r == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
            { _vault.RenameItem(item.Id, input.Text); RefreshList(); }
        }
        catch (Exception ex) { LogService.Error($"Rename failed: {ex}"); _activeDialog = null; }
    }



    // Progress
    void ShowProgress(string title, string detail, bool cancellable = false)
    {
        ProgressTitle.Text = title;
        ProgressDetail.Text = detail;
        ProgressBar.Value = 0;
        ProgressCancelBtn.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
        ProgressCancelBtn.IsEnabled = true;
        ProgressOverlay.Visibility = Visibility.Visible;
    }
    void UpdateProgress(double v, string detail)
    {
        ProgressDetail.Text = detail;
        var sb = new Storyboard();
        var da = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            From = ProgressBar.Value,
            To = v * 100
        };
        Storyboard.SetTarget(da, ProgressBar);
        Storyboard.SetTargetProperty(da, "Value");
        sb.Children.Add(da);
        sb.Begin();
    }
    void HideProgress() => ProgressOverlay.Visibility = Visibility.Collapsed;

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }

    void OnProgressCancel(object s, RoutedEventArgs e)
    {
        _batchCts?.Cancel();
        ProgressCancelBtn.IsEnabled = false;
    }

    // Selection
    void OnSelectionChanged(object? sender, EventArgs e)
    {
        var sel = FileList.SelectedItems;
        if (sel.Count > 0)
            StatusBar.SetSelectionInfo(sel.Count, FileList.GetSelectedTotalSize());
        else
            StatusBar.SetSelectionInfo(0, 0);
    }

    // Batch result reporting
    async Task ReportBatchResult(string operation, int total, List<(string Name, string Error)> errors)
    {
        if (errors.Count == 0)
        {
            ShowSuccessBanner(Loc.Format("MainWindow", "BatchDone", operation, total));
        }
        else
        {
            var successCount = total - errors.Count;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Loc.Format("MainWindow", "BatchPartialFail", operation, successCount, total));
            sb.AppendLine();
            sb.AppendLine(Loc.Get("MainWindow", "BatchErrorDetails"));
            foreach (var (name, error) in errors.Take(20))
                sb.AppendLine($"  {name}: {error}");
            if (errors.Count > 20)
                sb.AppendLine(Loc.Format("MainWindow", "BatchErrorMore", errors.Count - 20));

            var d = new ContentDialog
            {
                Title = Loc.Get("MainWindow", "BatchResult"),
                Content = new ScrollViewer
                {
                    Content = new TextBlock { Text = sb.ToString(), TextWrapping = TextWrapping.Wrap, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") },
                    MaxHeight = 400
                },
                CloseButtonText = Loc.Get("Common", "OK"),
                XamlRoot = Content.XamlRoot
            };
            await d.ShowAsync();
        }
    }

    // ┢�┢� Error banner ┢�┢�
    void ShowErrorBanner(string msg)
    {
        LogService.Error(msg);
        ErrorBannerText.Text = msg;
        ErrorBannerViewBtn.Content = Loc.Get("SettingsWindow", "ViewLog");
        ErrorBanner.Visibility = Visibility.Visible;
        AnimateBannerIn(ErrorBanner, ErrorBannerTransform);
    }

    void OnErrorBannerClick(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ShowErrorLog();
    void OnErrorBannerClickBtn(object s, RoutedEventArgs e) => ShowErrorLog();

    void ShowErrorLog()
    {
        OnSettingsCmd(null!, null!);
        DispatcherQueue.TryEnqueue(() => ErrorLogSection?.StartBringIntoView());
    }

    void OnErrorBannerClose(object s, RoutedEventArgs e) => AnimateBannerOut(ErrorBanner, ErrorBannerTransform);

    // Error log section in settings
    void RefreshErrorLogSection()
    {
        ErrorLogSection.Visibility = _integrityIssues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ErrorLogLabel.Text = Loc.Get("SettingsWindow", "ErrorLogLabel");
        ErrorLogClearAllBtn.Content = Loc.Get("MainWindow", "IntegrityClearAll");
        ErrorLogList.Items.Clear();
        foreach (var issue in _integrityIssues)
        {
            var (glyph, color) = issue.Type switch
            {
                IntegrityIssueType.OrphanMeta => ("", "#CA5010"),
                IntegrityIssueType.OrphanData => ("", "#CA5010"),
                IntegrityIssueType.Undecryptable => ("", "#D13438"),
                IntegrityIssueType.Unbound => ("", "#D13438"),
                _ => ("", "#666666")
            };
            ErrorLogList.Items.Add(new IntegrityIssueVM(issue, glyph, color));
        }
    }

    void OnIssueClick(object s, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not IntegrityIssueVM vm) return;
        var flyout = new Flyout();
        var panel = new StackPanel { Spacing = 8, Width = 280 };
        panel.Children.Add(new TextBlock { Text = vm.Issue.Description, TextWrapping = TextWrapping.Wrap });

        if (vm.Issue.Type is IntegrityIssueType.OrphanMeta or IntegrityIssueType.OrphanData)
        {
            var cleanBtn = new Button { Content = Loc.Get("MainWindow", "IntegrityClean"), HorizontalAlignment = HorizontalAlignment.Stretch };
            cleanBtn.Click += (_, _) => { CleanSingleIssue(vm.Issue); flyout.Hide(); };
            var ignoreBtn = new Button { Content = Loc.Get("MainWindow", "IntegrityIgnore"), HorizontalAlignment = HorizontalAlignment.Stretch };
            ignoreBtn.Click += (_, _) => { _integrityIssues.Remove(vm.Issue); RefreshErrorLogSection(); flyout.Hide(); };
            panel.Children.Add(cleanBtn);
            panel.Children.Add(ignoreBtn);
        }
        else if (vm.Issue.Type == IntegrityIssueType.Undecryptable)
        {
            var cleanBtn = new Button { Content = Loc.Get("MainWindow", "IntegrityClean"), HorizontalAlignment = HorizontalAlignment.Stretch };
            cleanBtn.Click += (_, _) => { CleanSingleIssue(vm.Issue); flyout.Hide(); };
            var keepBtn = new Button { Content = Loc.Get("MainWindow", "IntegrityKeep"), HorizontalAlignment = HorizontalAlignment.Stretch };
            keepBtn.Click += (_, _) => { _integrityIssues.Remove(vm.Issue); RefreshErrorLogSection(); flyout.Hide(); };
            panel.Children.Add(cleanBtn);
            panel.Children.Add(keepBtn);
        }
        // Unbound: info only, no action

        flyout.Content = panel;
        if (ErrorLogList.ContainerFromItem(vm) is ListViewItem container)
            flyout.ShowAt(container);
    }

    void CleanSingleIssue(IntegrityIssue issue)
    {
        if (issue.Type is IntegrityIssueType.OrphanMeta or IntegrityIssueType.OrphanData)
            _vault.CleanOrphans(
                issue.Type == IntegrityIssueType.OrphanMeta ? new List<string> { issue.Id } : new(),
                issue.Type == IntegrityIssueType.OrphanData ? new List<string> { issue.Id } : new());
        else if (issue.Type == IntegrityIssueType.Undecryptable)
            _vault.CleanUndecryptable(new List<string> { issue.Id });
        _integrityIssues.Remove(issue);
        RefreshErrorLogSection();
        RefreshList();
        if (_integrityIssues.Count == 0) AnimateBannerOut(ErrorBanner, ErrorBannerTransform);
    }

    void OnClearAllIssues(object s, RoutedEventArgs e)
    {
        var orphans = _integrityIssues.Where(i => i.Type is IntegrityIssueType.OrphanMeta or IntegrityIssueType.OrphanData).ToList();
        var undecryptable = _integrityIssues.Where(i => i.Type == IntegrityIssueType.Undecryptable).ToList();
        if (orphans.Count > 0)
            _vault.CleanOrphans(
                orphans.Where(i => i.Type == IntegrityIssueType.OrphanMeta).Select(i => i.Id).ToList(),
                orphans.Where(i => i.Type == IntegrityIssueType.OrphanData).Select(i => i.Id).ToList());
        if (undecryptable.Count > 0)
            _vault.CleanUndecryptable(undecryptable.Select(i => i.Id).ToList());
        _integrityIssues.Clear();
        RefreshErrorLogSection();
        RefreshList();
        AnimateBannerOut(ErrorBanner, ErrorBannerTransform);
    }

    class IntegrityIssueVM
    {
        public IntegrityIssue Issue { get; }
        public string IconGlyph { get; }
        public string Title => Issue.Type switch
        {
            IntegrityIssueType.OrphanMeta => Loc.Get("MainWindow", "IntegrityOrphanMetaTitle"),
            IntegrityIssueType.OrphanData => Loc.Get("MainWindow", "IntegrityOrphanDataTitle"),
            IntegrityIssueType.Undecryptable => Loc.Get("MainWindow", "IntegrityUndecryptableTitle"),
            IntegrityIssueType.Unbound => Loc.Get("MainWindow", "IntegrityUnboundTitle"),
            _ => ""
        };
        public string Detail => Issue.Id.Length > 12 ? Issue.Id[..12] + "..." : Issue.Id;
        public Microsoft.UI.Xaml.Media.SolidColorBrush IconBrush { get; }
        public IntegrityIssueVM(IntegrityIssue issue, string iconGlyph, string color)
        {
            Issue = issue;
            IconGlyph = iconGlyph;
            IconBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(FromHex(color));
        }
    }

    // ���� Success banner ����
    void ShowSuccessBanner(string msg)
    {
        SuccessBannerText.Text = msg;
        SuccessBanner.Visibility = Visibility.Visible;
        AnimateBannerIn(SuccessBanner, SuccessBannerTransform);
        _ = AutoHideSuccessBanner();
    }

    async Task AutoHideSuccessBanner()
    {
        await Task.Delay(3000);
        AnimateBannerOut(SuccessBanner, SuccessBannerTransform);
    }

    void OnSuccessBannerClose(object s, RoutedEventArgs e) => AnimateBannerOut(SuccessBanner, SuccessBannerTransform);

    void AnimateBannerIn(UIElement banner, CompositeTransform transform)
    {
        transform.TranslateY = BannerHeight;
        banner.Opacity = 1;
        var sb = new Storyboard();
        var da = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            From = BannerHeight,
            To = 0
        };
        Storyboard.SetTarget(da, transform);
        Storyboard.SetTargetProperty(da, nameof(CompositeTransform.TranslateY));
        sb.Children.Add(da);
        sb.Begin();
    }

    void AnimateBannerOut(UIElement banner, CompositeTransform transform)
    {
        var sb = new Storyboard();
        var da = new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            From = 1,
            To = 0
        };
        Storyboard.SetTarget(da, banner);
        Storyboard.SetTargetProperty(da, nameof(UIElement.Opacity));
        da.Completed += (_, _) => { banner.Visibility = Visibility.Collapsed; transform.TranslateY = BannerHeight; };
        sb.Children.Add(da);
        sb.Begin();
    }

    async void OnChangePw(object s, RoutedEventArgs e)
    {
        // Multi-type change password: verify old password → select new type → enter → confirm
        var changeCtrl = new Views.PasswordChangeControl(_pw);

        var tcs = new TaskCompletionSource<(object Old, object New)?>();
        changeCtrl.ChangeCompleted += (_, creds) => { tcs.TrySetResult(creds); };

        var d = new ContentDialog
        {
            Title = Loc.Get("SettingsWindow", "ChangePassword"),
            Content = changeCtrl,
            CloseButtonText = Loc.Get("Common", "Cancel"),
            // Hide primary/secondary buttons; all interaction is inside the control
            PrimaryButtonText = null,
            SecondaryButtonText = null,
            XamlRoot = Content.XamlRoot
        };

        // Close the dialog when the change flow completes
        changeCtrl.ChangeCompleted += (_, _) => { _ = DispatcherQueue.TryEnqueue(() => d.Hide()); };

        var dialogResult = await d.ShowAsync();

        // If user cancelled the dialog without completing the flow, bail out
        if (dialogResult != ContentDialogResult.None || !tcs.Task.IsCompleted)
        {
            // Dialog was dismissed by Cancel button or other means without completing
            if (!tcs.Task.IsCompleted) return;
        }

        var result = await tcs.Task;
        if (result == null) return;

        _busy = true;
        try
        {
            var (oldUk, newUk) = await _pw.ChangePasswordAsync(result.Value.Old, result.Value.New);
            var authDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "auth");
            var vkPath = Path.Combine(authDir, ".gyrock");
            var enc = File.ReadAllBytes(vkPath);
            var kp = _enc.DecryptVaultKeyPair(enc, oldUk);
            File.WriteAllBytes(vkPath, _enc.EncryptVaultKeyPair(kp, newUk));
            await Info(Loc.Get("SettingsWindow", "PwChanged"));
        }
        catch (Exception ex) { await Info(ex.Message); }
        finally { _busy = false; }
    }
    // Drag-out: decrypt to temp folder, provide as StorageItems, auto-cleanup
    async void OnDragOut(object? s, IReadOnlyList<Models.VaultFileItem> items)
    {
        // Deferred handling is done in VaultFileListView.OnDragStart via DataPackage
    }

    static double GetLuminance(Windows.UI.Color c) => 0.299 * c.R / 255.0 + 0.587 * c.G / 255.0 + 0.114 * c.B / 255.0;

    async Task Info(string msg) { var d = new ContentDialog { Title = AppInfo.Name, Content = new TextBlock { Text = msg }, CloseButtonText = Loc.Get("Common", "OK"), XamlRoot = Content.XamlRoot }; await d.ShowAsync(); }
}
