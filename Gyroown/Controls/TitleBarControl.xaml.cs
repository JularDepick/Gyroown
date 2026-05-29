using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System.Text;
using System.Text.Json;
using Gyroown.Services;
using Gyroown.Models;

namespace Gyroown.Controls;

public sealed partial class TitleBarControl : UserControl
{
    public event Action<string>? SearchChanged;
    public event Action<SearchFilter>? FilterChanged;
    public event EventHandler? RefreshRequested;
    public event EventHandler? IntegrityCheckRequested;
    public event EventHandler? SettingsRequested;

    private static string HistoryFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".Gyroown", "search-history.gyrojson");
    private const int MaxHistory = 10;
    private readonly EncryptionService _enc = new();
    private byte[]? _vaultKey;
    private List<string> _history = new();
    private SearchFilter _currentFilter = new();
    private bool _suppressFilterEvent;

    public TitleBarControl()
    {
        InitializeComponent();
        var langHandler = (EventHandler)((_, _) => ApplyLoc());
        Loc.LanguageChanged += langHandler;
        Unloaded += (_, _) => Loc.LanguageChanged -= langHandler;
        ApplyLoc();
        SearchBoxInput.TextChanged += OnSearchTextChanged;
        InitFilterCombos();
    }

    /// <summary>Initialize with vault key and load encrypted search history.</summary>
    public void Initialize(byte[] vaultKey)
    {
        _vaultKey = vaultKey;
        LoadHistory();
    }

    void ApplyLoc()
    {
        SearchBoxInput.PlaceholderText = Loc.Get("MainWindow", "Search");
        ToolTipService.SetToolTip(AdvancedSearchBtn, Loc.Get("FileList", "AdvancedSearch"));
        ToolTipService.SetToolTip(RefreshBtn, Loc.Get("MainWindow", "Refresh"));
        ToolTipService.SetToolTip(CheckBtn, Loc.Get("MainWindow", "IntegrityCheck"));
        ToolTipService.SetToolTip(SettingsBtn, Loc.Get("SettingsWindow", "Title"));
        FilterTitle.Text = Loc.Get("FileList", "AdvancedSearch");
        FileTypeLabel.Text = Loc.Get("FileList", "FileType");
        FileSizeLabel.Text = Loc.Get("FileList", "FileSize");
        ModifiedDateLabel.Text = Loc.Get("FileList", "ModifiedDate");
        ResetFiltersBtn.Content = Loc.Get("FileList", "ResetFilters");

        // Refresh ComboBox item text
        RefreshComboItems();
        UpdateFilterButtonIndicator();
    }

    public UIElement GetDragElement() => DragRegion;
    public void FocusSearch() => SearchBoxInput.Focus(FocusState.Keyboard);

    // ── Search history ──

    void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFile) || _vaultKey == null) return;
            var blob = File.ReadAllBytes(HistoryFile);
            var json = _enc.DecryptBlob(blob, _vaultKey);
            var arr = JsonSerializer.Deserialize<string[]>(Encoding.UTF8.GetString(json));
            if (arr != null) _history = arr.Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        catch { }
    }

    async Task SaveHistoryAsync()
    {
        if (_vaultKey == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_history.Take(MaxHistory).ToArray()));
            var blob = _enc.EncryptBlob(json, _vaultKey);
            await File.WriteAllBytesAsync(HistoryFile, blob);
        }
        catch { }
    }

    void SaveHistory() { if (_vaultKey != null) _ = SaveHistoryAsync(); }

    void AddToHistory(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        _history.Remove(query);
        _history.Insert(0, query);
        if (_history.Count > MaxHistory) _history = _history.Take(MaxHistory).ToList();
        SaveHistory();
    }

    void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        var text = sender.Text;
        _currentFilter.TextQuery = text;
        SearchChanged?.Invoke(text);
        FilterChanged?.Invoke(_currentFilter);

        // Show history suggestions when text is empty and box is focused
        if (string.IsNullOrEmpty(text) && _history.Count > 0)
        {
            sender.ItemsSource = _history;
        }
        else if (!string.IsNullOrEmpty(text))
        {
            var matches = _history.Where(h => h.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            sender.ItemsSource = matches.Count > 0 ? matches : null;
        }
    }

    void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        sender.Text = args.SelectedItem as string ?? "";
    }

    void OnSearchGotFocus(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(SearchBoxInput.Text) && _history.Count > 0)
            SearchBoxInput.ItemsSource = _history;
    }

    void OnSearchLostFocus(object s, RoutedEventArgs e)
    {
        SearchBoxInput.ItemsSource = null;
        if (!string.IsNullOrWhiteSpace(SearchBoxInput.Text))
            AddToHistory(SearchBoxInput.Text);
    }

    // ── Advanced search ──

    void InitFilterCombos()
    {
        _suppressFilterEvent = true;

        // File type
        CategoryCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "CategoryAll"), Tag = FileCategory.All });
        CategoryCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "CategoryImage"), Tag = FileCategory.Image });
        CategoryCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "CategoryVideo"), Tag = FileCategory.Video });
        CategoryCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "CategoryAudio"), Tag = FileCategory.Audio });
        CategoryCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "CategoryDocument"), Tag = FileCategory.Document });
        CategoryCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "CategoryOther"), Tag = FileCategory.Other });
        CategoryCombo.SelectedIndex = 0;

        // File size
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeAny"), Tag = SizeRange.Any });
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeLt1MB"), Tag = SizeRange.Lt1MB });
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeLt10MB"), Tag = SizeRange.Lt10MB });
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeLt100MB"), Tag = SizeRange.Lt100MB });
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeGt1MB"), Tag = SizeRange.Gt1MB });
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeGt10MB"), Tag = SizeRange.Gt10MB });
        SizeCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "SizeGt100MB"), Tag = SizeRange.Gt100MB });
        SizeCombo.SelectedIndex = 0;

        // Modified date
        DateCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "DateAny"), Tag = DateRange.Any });
        DateCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "DateToday"), Tag = DateRange.Today });
        DateCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "DateThisWeek"), Tag = DateRange.ThisWeek });
        DateCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "DateThisMonth"), Tag = DateRange.ThisMonth });
        DateCombo.Items.Add(new ComboBoxItem { Content = Loc.Get("FileList", "DateThisYear"), Tag = DateRange.ThisYear });
        DateCombo.SelectedIndex = 0;

        _suppressFilterEvent = false;
    }

    void RefreshComboItems()
    {
        var categoryKeys = new[] { "CategoryAll", "CategoryImage", "CategoryVideo", "CategoryAudio", "CategoryDocument", "CategoryOther" };
        RefreshCombo(CategoryCombo, categoryKeys);

        var sizeKeys = new[] { "SizeAny", "SizeLt1MB", "SizeLt10MB", "SizeLt100MB", "SizeGt1MB", "SizeGt10MB", "SizeGt100MB" };
        RefreshCombo(SizeCombo, sizeKeys);

        var dateKeys = new[] { "DateAny", "DateToday", "DateThisWeek", "DateThisMonth", "DateThisYear" };
        RefreshCombo(DateCombo, dateKeys);
    }

    static void RefreshCombo(ComboBox combo, string[] locKeys)
    {
        for (int i = 0; i < combo.Items.Count && i < locKeys.Length; i++)
        {
            if (combo.Items[i] is ComboBoxItem item)
                item.Content = Loc.Get("FileList", locKeys[i]);
        }
    }

    void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressFilterEvent) return;

        _currentFilter.Category = GetSelectedEnum<FileCategory>(CategoryCombo, FileCategory.All);
        _currentFilter.Size = GetSelectedEnum<SizeRange>(SizeCombo, SizeRange.Any);
        _currentFilter.Date = GetSelectedEnum<DateRange>(DateCombo, DateRange.Any);

        FilterChanged?.Invoke(_currentFilter);
        UpdateFilterButtonIndicator();
    }

    static TEnum GetSelectedEnum<TEnum>(ComboBox combo, TEnum fallback) where TEnum : Enum
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is TEnum val)
            return val;
        return fallback;
    }

    void OnResetFilters(object sender, RoutedEventArgs e)
    {
        _suppressFilterEvent = true;
        CategoryCombo.SelectedIndex = 0;
        SizeCombo.SelectedIndex = 0;
        DateCombo.SelectedIndex = 0;
        _suppressFilterEvent = false;

        _currentFilter.Category = FileCategory.All;
        _currentFilter.Size = SizeRange.Any;
        _currentFilter.Date = DateRange.Any;

        FilterChanged?.Invoke(_currentFilter);
        UpdateFilterButtonIndicator();
    }

    void OnFlyoutClosing(object sender, object e)
    {
        // No additional action needed when Flyout closes
    }

    void OnRefreshClick(object sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);
    void OnCheckClick(object sender, RoutedEventArgs e) => IntegrityCheckRequested?.Invoke(this, EventArgs.Empty);
    void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    void UpdateFilterButtonIndicator()
    {
        var hasFilters = _currentFilter.HasAdvancedFilters;
        AdvancedSearchBtn.Foreground = hasFilters
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
            : null;
        ToolTipService.SetToolTip(AdvancedSearchBtn,
            hasFilters ? Loc.Get("FileList", "FilterActive") : Loc.Get("FileList", "AdvancedSearch"));
    }
}
