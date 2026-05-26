using Microsoft.UI;
using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Gyroown.Views;
using Gyroown.Services;
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
    private H.NotifyIcon.TaskbarIcon? _tray;

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int n);
    private const int SW_HIDE = 0, SW_SHOW = 5, SW_RESTORE = 9;
    private IntPtr Hwnd => WindowNative.GetWindowHandle(this);

    public MainWindow(PasswordService pw, EncryptionService enc, VaultService vault)
    {
        _pw = pw; _enc = enc; _vault = vault;
        _theme = new ThemeService();
        _drag = new DragDropService(vault);
        InitializeComponent();
        Activated += (_, _) => SetTitleBar(TitleBar.GetDragElement());
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
            if (p.Hex == _theme.AccentColor) { var check = new FontIcon { Glyph = "\uE73E", FontSize = 12, Foreground = new SolidColorBrush(Colors.White) }; btn.Content = check; }
            AccentList.Children.Add(btn);
        }
        ApplyTheme();

        // Apply saved language
        Loc.Service.SetLanguage(_theme.Language);

        if (!_pw.IsPasswordSet) ShowSetup();
        else if (_pw.IsLocked) ShowUnlock();
        else ShowVault();
    }

    void InitTray()
    {
        try
        {
            // Prefer favicon.png, fallback to .ico
            string[] candidates = { "favicon.png", "favicon.ico" };
            foreach (var name in candidates)
            {
                var p = Path.Combine(AppContext.BaseDirectory, name);
                if (!File.Exists(p)) p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", name);
                if (File.Exists(p))
                {
                    var ico = name.EndsWith(".ico")
                        ? new System.Drawing.Icon(p)
                        : System.Drawing.Icon.FromHandle(new System.Drawing.Bitmap(p).GetHicon());
                    _tray = new H.NotifyIcon.TaskbarIcon { ToolTipText = "Gyroown", Icon = ico };
                    break;
                }
            }
        }
        catch { /* degrade gracefully */ }
    }

    // ┢�┢� Auth ┢�┢�
    void ShowSetup()
    {
        var c = new PasswordSetupControl(_pw);
        c.SetupCompleted += async (_, cred) => await FinalizeSetup(cred);
        AuthHost.Content = c;
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

        // Insurance handled inline in PasswordSetupControl
        if (InsuranceService.IsEnabled)
        {
            var insKp = _enc.GenerateInsuranceKeyPair();
            InsuranceService.SaveLocal(_enc.EncryptVaultKeyForInsurance(kp, insKp.PublicKey));
            _ = UploadInsuranceAsync(insKp.PrivateKey);
        }

        _vault.Initialize(kp.PrivateKey, kp.PublicKey);
        AuthOverlay.Visibility = Visibility.Collapsed;
        VaultContent.Visibility = Visibility.Visible;
        RefreshList();
        _ = RunIntegrityCheck();
    }

    async Task UploadInsuranceAsync(byte[] insPriv)
    {
        try { await InsuranceService.UploadAsync("user@example.com", "token-stub", insPriv); }
        catch { }
    }

    async void ShowUnlock()
    {
        // Verify auth integrity before showing unlock
        if (!VaultService.IsVaultIntact())
        {
            var dlg = new ContentDialog
            {
                Title = Loc.Get("MainWindow", "AuthMissing"),
                Content = new TextBlock { Text = Loc.Get("MainWindow", "AuthMissingMsg"), TextWrapping = TextWrapping.Wrap },
                CloseButtonText = Loc.Get("Common", "OK"),
                XamlRoot = Content.XamlRoot
            };
            await dlg.ShowAsync();
            Application.Current.Exit();
            return;
        }

        var c = new UnlockControl(_pw);
        c.Unlocked += (_, r) =>
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "auth");
            var enc = File.ReadAllBytes(Path.Combine(d, ".gyrock"));
            var kp = _enc.DecryptVaultKeyPair(enc, r.UserKey!);
            _vault.Initialize(kp.PrivateKey, kp.PublicKey);
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
        await Task.Delay(800); // let UI settle

        if (!VaultService.AreDataAndMetaBound())
        {
            var msg = Loc.Get("MainWindow", "IntegrityUnbound");
            LogService.Error(msg);
            await ShowDialog(Loc.Get("MainWindow", "Integrity"), msg);
            return;
        }

        var (orphanMeta, orphanData, paired) = _vault.CheckIntegrity();
        if (orphanMeta.Count == 0 && orphanData.Count == 0) return;

        var imsg = Loc.Format("MainWindow", "IntegrityMsg", paired, orphanMeta.Count, orphanData.Count);
        ShowErrorBanner($"{Loc.Get("MainWindow", "Integrity")}: {imsg}");
        var r = await ShowDialog(Loc.Get("MainWindow", "Integrity"), imsg,
            Loc.Get("MainWindow", "IntegrityClean"), Loc.Get("MainWindow", "IntegrityIgnore"));
        if (r == ContentDialogResult.Primary) { _vault.CleanOrphans(orphanMeta, orphanData); RefreshList(); }
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
    static string CredStr(object c) => c switch { string s => s, int[] a => string.Join(",", a), Array arr => string.Join(";", arr.Cast<object>().Select(o => o?.ToString() ?? "")), _ => c.ToString() ?? "" };

    // ┢�┢� Localization ┢�┢�
    void ApplyToolbarLoc()
    {
        BtnNewFolder.Label = Loc.Get("MainWindow", "NewFolder"); BtnImport.Label = Loc.Get("MainWindow", "Import");
        BtnExport.Label = Loc.Get("MainWindow", "Export"); BtnDelete.Label = Loc.Get("MainWindow", "Delete");
        BtnSettings.Label = Loc.Get("MainWindow", "Settings"); BtnLock.Label = Loc.Get("MainWindow", "Lock");
        BtnMoveIn.Label = Loc.Get("MainWindow", "MoveIn");
        BtnMoveOut.Label = Loc.Get("MainWindow", "MoveOut");

        FileList.ItemOpened += OnFileOpened;
        FileList.RenameRequested += OnRenameRequested;
        TitleBar.SearchChanged += q => FileList.Filter = q;
        Sidebar.FolderSelected += (_, path) =>
        {
            FileList.FilterPath = path;
            _vault.SetCurrentPath(path);
        };
        FileList.DecryptToFile = async (id, path) =>
        {
            await using var fs = File.Create(path);
            await _vault.ExportItemAsync(id, fs);
        };
        FileList.ExportRequested += async (_, item) =>
        {
            var p = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
            p.SuggestedFileName = item.Name;
            var file = await p.PickSaveFileAsync();
            if (file != null) { await using var st = await file.OpenStreamForWriteAsync(); await _vault.ExportItemAsync(item.Id, st); }
        };
    }

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
        VersionText.Text = Loc.Get("SettingsWindow", "Version");
        GitHubLink.Content = Loc.Get("SettingsWindow", "GitHub");
        VaultPathText.Text = _vault.VaultPath;
        ThemeCombo.ItemsSource = _theme.GetAvailableThemes();
        ThemeCombo.SelectedItem = _theme.CurrentTheme;
    }

    // ┢�┢� Settings panel ┢�┢�
    void OnSettingsCmd(object s, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Visible;
    void OnSettingsClose(object s, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Collapsed;
    void OnThemeSel(object s, SelectionChangedEventArgs e) { if (ThemeCombo.SelectedItem is AppTheme t) _theme.SetTheme(t); }
    void OnLangSel(object s, SelectionChangedEventArgs e) { if (LangCombo.SelectedItem is ComboBoxItem ci && ci.Tag is string t) { Loc.Service.SetLanguage(t); _theme.SetLanguage(t); } }

    void InitChunkSlider()
    {
        var cfg = _vault.GetConfig().Load();
        ChunkSlider.Value = cfg.ChunkTier;
        ChunkValue.Text = Loc.Format("SettingsWindow", "ChunkValue", ConfigService.ChunkTiers[cfg.ChunkTier]);
    }

    async void OnChunkChanged(object s, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ChunkValue == null || !_vault.IsInitialized) return;
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
        // Set on root only �?cascades to all children
        if (Content is FrameworkElement root) root.RequestedTheme = theme;
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

    // ┢�┢� Window ┢�┢�
    private bool _busy;

    // ┢�┢� Commands ┢�┢�
    void RefreshList() { var items = _vault.ListItems(); FileList.SetItems(items); StatusBar.SetItemCount(items.Count); StatusBar.SetVaultPath(_vault.CurrentPath); _ = FileList.LoadPreviewsAsync(_vault); Sidebar.BuildTree(_vault.GetFolderTree()); }

    async void OnImportCmd(object s, RoutedEventArgs e)
    {
        var p = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        p.FileTypeFilter.Add("*");
        var files = await p.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        ShowProgress(Loc.Get("MainWindow", "Import"), $"0 / {files.Count}");
        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                await using var st = await f.OpenStreamForReadAsync();
                await _vault.ImportItemAsync(st, f.Name);
                UpdateProgress((double)(i + 1) / files.Count, $"{i + 1} / {files.Count}: {f.Name}");
            }
        }
        finally { HideProgress(); RefreshList(); }
    }

    async void OnExportCmd(object s, RoutedEventArgs e)
    {
        var sel = FileList.SelectedItems;
        if (sel.Count == 0) { await Info(Loc.Get("MainWindow", "ExportSelectHint")); return; }
        var p = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        var folder = await p.PickSingleFolderAsync();
        if (folder == null) return;

        ShowProgress(Loc.Get("MainWindow", "Export"), $"0 / {sel.Count}");
        var items = sel.ToList();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var f = await folder.CreateFileAsync(item.Name, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                await using var st = await f.OpenStreamForWriteAsync();
                await _vault.ExportItemAsync(item.Id, st);
                UpdateProgress((double)(i + 1) / items.Count, $"{i + 1} / {items.Count}: {item.Name}");
            }
        }
        finally { HideProgress(); }
    }

    // ┢�┢� Move (cut) ┢�┢�
    async void OnMoveInCmd(object s, RoutedEventArgs e)
    {
        var p = new Windows.Storage.Pickers.FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        p.FileTypeFilter.Add("*");
        var files = await p.PickMultipleFilesAsync();
        if (files == null || files.Count == 0) return;

        _busy = true;
        ShowProgress(Loc.Get("MainWindow", "MoveIn"), $"0 / {files.Count}");
        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                await using var st = await f.OpenStreamForReadAsync();
                await _vault.ImportItemAsync(st, f.Name);
                UpdateProgress((double)(i + 1) / files.Count, $"{i + 1} / {files.Count}: {f.Name}");
                // Delete original after successful import
                try { await f.DeleteAsync(); } catch { /* original might be locked */ }
            }
        }
        finally { _busy = false; HideProgress(); RefreshList(); }
    }

    async void OnMoveOutCmd(object s, RoutedEventArgs e)
    {
        var sel = FileList.SelectedItems;
        if (sel.Count == 0) { await Info(Loc.Get("MainWindow", "ExportSelectHint")); return; }
        var p = new Windows.Storage.Pickers.FolderPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(p, Hwnd);
        var folder = await p.PickSingleFolderAsync();
        if (folder == null) return;

        _busy = true;
        ShowProgress(Loc.Get("MainWindow", "MoveOut"), $"0 / {sel.Count}");
        var items = sel.ToList();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var f = await folder.CreateFileAsync(item.Name, Windows.Storage.CreationCollisionOption.GenerateUniqueName);
                await using var st = await f.OpenStreamForWriteAsync();
                await _vault.ExportItemAsync(item.Id, st);
                _vault.DeleteItem(item.Id);
                UpdateProgress((double)(i + 1) / items.Count, $"{i + 1} / {items.Count}: {item.Name}");
            }
        }
        finally { _busy = false; HideProgress(); RefreshList(); }
    }

    async void OnDropIn(object? s, IReadOnlyList<string> paths)
    {
        ShowProgress(Loc.Get("MainWindow", "Import"), $"0 / {paths.Count}");
        try
        {
            for (int i = 0; i < paths.Count; i++)
            {
                var p = paths[i];
                await using var fs = File.OpenRead(p);
                await _vault.ImportItemAsync(fs, Path.GetFileName(p));
                UpdateProgress((double)(i + 1) / paths.Count, $"{i + 1} / {paths.Count}: {Path.GetFileName(p)}");
            }
        }
        finally { HideProgress(); RefreshList(); }
    }

    async void OnDeleteCmd(object s, RoutedEventArgs e) { var sel = FileList.SelectedItems; if (sel.Count == 0) return; var d = new ContentDialog { Title = Loc.Get("MainWindow", "Delete"), Content = new TextBlock { Text = Loc.Format("MainWindow", "DeleteConfirm", sel.Count) }, PrimaryButtonText = Loc.Get("Common", "Delete"), CloseButtonText = Loc.Get("Common", "Cancel"), XamlRoot = Content.XamlRoot }; if (await d.ShowAsync() == ContentDialogResult.Primary) { _busy = true; try { foreach (var i in sel) _vault.DeleteItem(i.Id); FileList.RemoveItems(sel); RefreshList(); } finally { _busy = false; } } }

    async void OnLockCmd(object s, RoutedEventArgs e) { _busy = true; try { _pw.Lock(); AuthOverlay.Visibility = Visibility.Visible; VaultContent.Visibility = Visibility.Collapsed; ShowUnlock(); } finally { _busy = false; } }

    async void OnNewFolderCmd(object s, RoutedEventArgs e) { _vault.CreateFolder("/" + Loc.Get("MainWindow", "NewFolderDefault")); RefreshList(); }

    void OnFileOpened(object? s, Models.VaultFileItem item) => _ = OpenViewer(item);
    async void OnRenameRequested(object? s, Models.VaultFileItem item)
    {
        if (_activeDialog != null) return;
        var input = new TextBox { Text = item.Name };
        var d = new ContentDialog { Title = Loc.Get("MainWindow", "Rename"), Content = input, PrimaryButtonText = Loc.Get("Common", "OK"), CloseButtonText = Loc.Get("Common", "Cancel"), XamlRoot = Content.XamlRoot };
        _activeDialog = d;
        var r = await d.ShowAsync();
        _activeDialog = null;
        if (r == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        { _vault.RenameItem(item.Id, input.Text); RefreshList(); }
    }

    async Task OpenViewer(Models.VaultFileItem item)
    {
        if (item.IsFolder) return;
        var ct = item.ContentType;
        try
        {
            if (ct.StartsWith("image/"))
            {
                using var ms = new MemoryStream();
                await _vault.ExportItemAsync(item.Id, ms);
                ms.Position = 0;
                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                var d = new ContentDialog { Title = item.Name, CloseButtonText = Loc.Get("Common", "Close"), XamlRoot = Content.XamlRoot };
                d.Content = new ScrollViewer { Content = new Microsoft.UI.Xaml.Controls.Image { Source = bmp, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform }, MaxHeight = 550, MaxWidth = 800 };
                await d.ShowAsync();
            }
            else if (ct.StartsWith("video/") || ct.StartsWith("audio/"))
            {
                var ms = new MemoryStream();
                await _vault.ExportItemAsync(item.Id, ms);
                ms.Position = 0;
                var d = new ContentDialog { Title = item.Name, CloseButtonText = Loc.Get("Common", "Close"), XamlRoot = Content.XamlRoot };
                var player = new Microsoft.UI.Xaml.Controls.MediaPlayerElement { AutoPlay = true, AreTransportControlsEnabled = true, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform, MaxHeight = 500, MaxWidth = 800 };
                player.Source = Windows.Media.Core.MediaSource.CreateFromStream(ms.AsRandomAccessStream(), ct);
                d.Content = player; await d.ShowAsync();
            }
            else if (ct.StartsWith("text/"))
            {
                using var ms = new MemoryStream();
                await _vault.ExportItemAsync(item.Id, ms);
                var text = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                var d = new ContentDialog { Title = item.Name, CloseButtonText = Loc.Get("Common", "Close"), XamlRoot = Content.XamlRoot };
                d.Content = new ScrollViewer
                {
                    Content = new TextBox { Text = text, IsReadOnly = true, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap },
                    MaxHeight = 550, MinWidth = 500
                };
                await d.ShowAsync();
            }
            else { await Info($"{item.Name} ({ct})"); }
        }
        catch (Exception ex) { await Info(ex.Message); }
    }

    string? GetPreviewId(string itemId) => _vault.GetPreviewId(itemId);

    // ┢�┢� Progress ┢�┢�
    void ShowProgress(string title, string detail) { ProgressTitle.Text = title; ProgressDetail.Text = detail; ProgressBar.Value = 0; ProgressOverlay.Visibility = Visibility.Visible; }
    void UpdateProgress(double v, string detail) { ProgressBar.Value = v * 100; ProgressDetail.Text = detail; }
    void HideProgress() => ProgressOverlay.Visibility = Visibility.Collapsed;

    // ┢�┢� Error banner ┢�┢�
    void ShowErrorBanner(string msg)
    {
        LogService.Error(msg);
        ErrorBannerText.Text = msg;
        ErrorBannerViewBtn.Content = Loc.Get("SettingsWindow", "ViewLog");
        ErrorBanner.Visibility = Visibility.Visible;
    }

    void OnErrorBannerClick(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e) => ShowErrorLog();
    void OnErrorBannerClickBtn(object s, RoutedEventArgs e) => ShowErrorLog();

    async void ShowErrorLog()
    {
        var content = LogService.ReadErrorLogContent();
        var d = new ContentDialog
        {
            Title = Loc.Get("SettingsWindow", "ErrorLog"),
            Content = new ScrollViewer { Content = new TextBlock { Text = string.IsNullOrEmpty(content) ? Loc.Get("Common", "NotImplementDetail") : content, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap }, MaxHeight = 400 },
            CloseButtonText = Loc.Get("Common", "OK"),
            XamlRoot = Content.XamlRoot
        };
        await d.ShowAsync();
    }

    void OnErrorBannerClose(object s, RoutedEventArgs e) => ErrorBanner.Visibility = Visibility.Collapsed;

    // ���� Success banner ����
    void ShowSuccessBanner(string msg)
    {
        SuccessBannerText.Text = msg;
        SuccessBanner.Visibility = Visibility.Visible;
        _ = AutoHideSuccessBanner();
    }

    async Task AutoHideSuccessBanner()
    {
        await Task.Delay(3000);
        SuccessBanner.Visibility = Visibility.Collapsed;
    }

    void OnSuccessBannerClose(object s, RoutedEventArgs e) => SuccessBanner.Visibility = Visibility.Collapsed;

    async void OnChangePw(object s, RoutedEventArgs e)
    {
        // Simplified: prompt for old + new password (string type only for now)
        var oldBox = new PasswordBox { PlaceholderText = Loc.Get("SettingsWindow", "OldPassword"), Header = Loc.Get("SettingsWindow", "OldPassword") };
        var newBox = new PasswordBox { PlaceholderText = Loc.Get("SettingsWindow", "NewPassword"), Header = Loc.Get("SettingsWindow", "NewPassword") };
        var sp = new StackPanel { Spacing = 12 };
        sp.Children.Add(oldBox); sp.Children.Add(newBox);

        var d = new ContentDialog
        {
            Title = Loc.Get("SettingsWindow", "ChangePassword"),
            Content = sp,
            PrimaryButtonText = Loc.Get("Common", "OK"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = Content.XamlRoot
        };

        if (await d.ShowAsync() != ContentDialogResult.Primary) return;

        _busy = true;
        try
        {
            var (oldUk, newUk) = await _pw.ChangePasswordAsync(oldBox.Password, newBox.Password);
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

    async Task Info(string msg) { var d = new ContentDialog { Title = "Gyroown", Content = new TextBlock { Text = msg }, CloseButtonText = Loc.Get("Common", "OK"), XamlRoot = Content.XamlRoot }; await d.ShowAsync(); }
}
