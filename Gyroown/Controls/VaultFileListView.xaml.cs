using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Automation;
using Gyroown.Services;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Gyroown.Models;
using System.Collections.ObjectModel;

namespace Gyroown.Controls;

public sealed partial class VaultFileListView : UserControl
{
    private ObservableCollection<VaultFileItem> _items = new();
    private readonly ObservableCollection<VaultFileItem> _all = new();
    private readonly Dictionary<string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> _previewCache = new();
    private Services.VaultService? _vault;
    private string _sortCol = "name";
    private bool _sortAsc = true;
    private string _filter = "";
    private string _filterPath = "/";
    private SearchFilter _searchFilter = new();

    // Column visibility (for right-click show/hide menu)
    private bool _showSizeCol = true;
    private bool _showTypeCol = true;
    private bool _showDateCol = true;

    public event EventHandler<IReadOnlyList<VaultFileItem>>? DragOutRequested;
    public event EventHandler<IReadOnlyList<string>>? DropInRequested;
    public event EventHandler<VaultFileItem>? ItemOpened;
    public event EventHandler<VaultFileItem>? RenameRequested;
    public event EventHandler<(VaultFileItem Item, string NewName)>? InlineRenameRequested;
    public event EventHandler<VaultFileItem>? PropertiesRequested;
    public event EventHandler? NewFolderRequested;
    public event EventHandler? RefreshRequested;
    public event EventHandler<VaultFileItem>? ExportRequested;
    public event EventHandler<VaultFileItem>? VersionHistoryRequested;
    public event EventHandler<IReadOnlyList<VaultFileItem>>? BatchDeleteRequested;
    public event EventHandler<IReadOnlyList<VaultFileItem>>? BatchExportRequested;
    public event EventHandler? SelectionChanged;
    /// <summary>Raised when user taps the favorite star on an item.</summary>
    public event EventHandler<VaultFileItem>? FavoriteToggleRequested;
    public Func<string, string, Task>? DecryptToFile { get; set; }

    public VaultFileListView()
    {
        InitializeComponent();
        FileList.ItemsSource = _items;
        FileGrid.ItemsSource = _items;
        FileGridMedium.ItemsSource = _items;
        FileGridSmall.ItemsSource = _items;
        FileListCompact.ItemsSource = _items;
        FileList.ContainerContentChanging += OnContainerContentChanging;
        FileGrid.ContainerContentChanging += OnContainerContentChanging;
        FileGridMedium.ContainerContentChanging += OnContainerContentChanging;
        FileGridSmall.ContainerContentChanging += OnContainerContentChanging;
        var langHandler = (EventHandler)((_, _) => ApplyLoc());
        Loc.LanguageChanged += langHandler;
        Unloaded += (_, _) => Loc.LanguageChanged -= langHandler;
        ApplyLoc();
    }

    void ApplyLoc()
    {
        AutomationProperties.SetName(ViewModeBtn, Loc.Get("Common", "ViewMode"));
        ViewDetailsItem.Text = Loc.Get("Common", "DetailsView");
        ViewLargeIconsItem.Text = Loc.Get("Common", "LargeIcons");
        ViewMediumIconsItem.Text = Loc.Get("Common", "MediumIcons");
        ViewSmallIconsItem.Text = Loc.Get("Common", "SmallIcons");
        ViewListItem.Text = Loc.Get("Common", "ListView");
    }

    public void SetItems(IEnumerable<VaultFileItem> items)
    {
        _all.Clear();
        foreach (var i in items) _all.Add(i);
        ApplyFilter();
    }

    public async Task LoadPreviewsAsync(Services.VaultService vault)
    {
        _vault = vault;
        // Previews are now loaded lazily via ContainerContentChanging
    }

    private async void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue) return;

        var item = args.Item as VaultFileItem;
        if (item == null || (!item.ContentType.StartsWith("image/") && !item.ContentType.StartsWith("video/")) || _vault == null) return;

        if (args.Phase == 0)
        {
            if (_previewCache.TryGetValue(item.Id, out var cached))
            {
                item.PreviewImage = cached;
                args.Handled = true;
                return;
            }
            args.RegisterUpdateCallback(1, OnContainerContentChanging);
            args.Handled = true;
        }
        else if (args.Phase == 1)
        {
            try
            {
                var pid = _vault.GetPreviewId(item.Id);
                if (pid != null)
                {
                    var data = await _vault.GetPreviewData(pid);
                    if (data != null)
                    {
                        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        var ms = new MemoryStream(data);
                        await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                        _previewCache[item.Id] = bmp;
                        item.PreviewImage = bmp;
                    }
                }
            }
            catch (Exception ex) { LogService.Debug($"LoadPreview failed for {item.Name}: {ex.Message}"); }
            args.Handled = true;
        }
    }

    public IReadOnlyList<VaultFileItem> SelectedItems
    {
        get
        {
            var src = _currentViewMode switch
            {
                "details" => FileList.SelectedItems,
                "large" => FileGrid.SelectedItems,
                "medium" => FileGridMedium.SelectedItems,
                "small" => FileGridSmall.SelectedItems,
                "list" => FileListCompact.SelectedItems,
                _ => FileList.SelectedItems
            };
            return src.Cast<VaultFileItem>().ToList();
        }
    }

    /// <summary>Get visible non-folder items for preview window navigation.</summary>
    public IReadOnlyList<VaultFileItem> GetVisibleFileItems() =>
        _items.Where(i => !i.IsFolder).ToList();

    public string Filter
    {
        get => _filter;
        set { _filter = value; ApplyFilter(); }
    }

    /// <summary>Set or get advanced search filter criteria.</summary>
    public SearchFilter SearchFilter
    {
        get => _searchFilter;
        set { _searchFilter = value ?? new SearchFilter(); ApplyFilter(); }
    }

    public string FilterPath
    {
        get => _filterPath;
        set
        {
            _filterPath = value ?? "/";
            _previewCache.Clear(); // Clear cache when switching directories
            ApplyFilter();
        }
    }

    // ── Sorting ──

    void SortByName(object s, RoutedEventArgs e) => SetSort("name");
    void SortBySize(object s, RoutedEventArgs e) => SetSort("size");
    void SortByType(object s, RoutedEventArgs e) => SetSort("type");
    void SortByDate(object s, RoutedEventArgs e) => SetSort("date");

    /// <summary>Set sort column and direction programmatically.</summary>
    public void SetSort(string col, bool ascending)
    {
        _sortCol = col;
        _sortAsc = ascending;
        ApplyFilter();
        UpdateSortHeaders();
    }

    void SetSort(string col)
    {
        if (_sortCol == col) _sortAsc = !_sortAsc; else { _sortCol = col; _sortAsc = true; }
        ApplyFilter();
        UpdateSortHeaders();
    }

    void UpdateSortHeaders()
    {
        var arrow = _sortAsc ? " ▲" : " ▼";
        SortName.Content = Loc.Get("FileList", "Name") + (_sortCol == "name" ? arrow : "");
        SortSize.Content = Loc.Get("FileList", "Size") + (_sortCol == "size" ? arrow : "");
        SortType.Content = Loc.Get("FileList", "Type") + (_sortCol == "type" ? arrow : "");
        SortDate.Content = Loc.Get("FileList", "Date") + (_sortCol == "date" ? arrow : "");
    }

    void ApplyFilter()
    {
        var q = _all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_filter))
        {
            // Parse advanced search syntax (type: / size: / date:)
            var parts = ParseSearchQuery(_filter);
            if (parts.NamePart != null)
                q = q.Where(i => i.Name.Contains(parts.NamePart, StringComparison.OrdinalIgnoreCase));
            if (parts.TypeFilter != null)
                q = q.Where(i => i.ContentType.Contains(parts.TypeFilter, StringComparison.OrdinalIgnoreCase));
            if (parts.MinSize != null)
                q = q.Where(i => i.OriginalSize >= parts.MinSize.Value);
            if (parts.MaxSize != null)
                q = q.Where(i => i.OriginalSize <= parts.MaxSize.Value);
            if (parts.DateFilter != null)
            {
                var cutoff = parts.DateFilter.Value;
                q = q.Where(i => i.ModifiedAt >= cutoff);
            }
        }
        if (_filterPath != "/")
            q = q.Where(i => i.VirtualPath == _filterPath || i.VirtualPath.StartsWith(_filterPath + "/"));

        // Advanced filters (type, size, date combined criteria)
        if (_searchFilter.HasAdvancedFilters)
        {
            var hasInlineText = !string.IsNullOrWhiteSpace(_filter);
            q = q.Where(i => _searchFilter.Matches(i, skipTextQuery: hasInlineText));
        }

        // Folders always before files, then apply column sort
        q = _sortCol switch
        {
            "size" => _sortAsc
                ? q.OrderBy(i => !i.IsFolder).ThenBy(i => i.OriginalSize)
                : q.OrderBy(i => !i.IsFolder).ThenByDescending(i => i.OriginalSize),
            "type" => _sortAsc
                ? q.OrderBy(i => !i.IsFolder).ThenBy(i => i.ContentType)
                : q.OrderBy(i => !i.IsFolder).ThenByDescending(i => i.ContentType),
            "date" => _sortAsc
                ? q.OrderBy(i => !i.IsFolder).ThenBy(i => i.ModifiedAt)
                : q.OrderBy(i => !i.IsFolder).ThenByDescending(i => i.ModifiedAt),
            _ => _sortAsc
                ? q.OrderBy(i => !i.IsFolder).ThenBy(i => i.Name)
                : q.OrderBy(i => !i.IsFolder).ThenByDescending(i => i.Name),
        };

        // Replace ItemsSource to force full re-render
        _items = new ObservableCollection<VaultFileItem>(q);

        // Mark search matches for highlighting
        var searchQuery = _filter ?? "";
        foreach (var item in _items)
        {
            item.IsSearchMatch = !string.IsNullOrWhiteSpace(searchQuery)
                && item.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
        }

        FileList.ItemsSource = _items;
        FileGrid.ItemsSource = _items;
        FileGridMedium.ItemsSource = _items;
        FileGridSmall.ItemsSource = _items;
        FileListCompact.ItemsSource = _items;

        // Show empty state when filter has no results or folder is empty
        var hasFilter = !string.IsNullOrWhiteSpace(_filter) || _searchFilter.HasAdvancedFilters;
        var showEmpty = _items.Count == 0 && (hasFilter || _filterPath != "/");
        EmptyState.Visibility = showEmpty ? Visibility.Visible : Visibility.Collapsed;
        ClearFiltersBtn.Visibility = hasFilter ? Visibility.Visible : Visibility.Collapsed;
        ClearFiltersBtn.Content = Loc.Get("FileList", "ResetFilters");
        var emptySearchText = _searchFilter.IsEmpty ? _filter : _searchFilter.TextQuery;
        EmptyStateText.Text = hasFilter
            ? string.Format(Loc.Get("FileList", "NoResults"),
                string.IsNullOrWhiteSpace(emptySearchText) ? Loc.Get("FileList", "AdvancedSearch") : emptySearchText)
            : Loc.Get("FileList", "EmptyFolder");
    }

    void OnClearFilters(object s, RoutedEventArgs e)
    {
        Filter = "";
        SearchFilter = new SearchFilter();
    }

    static (string? NamePart, string? TypeFilter, long? MinSize, long? MaxSize, DateTime? DateFilter) ParseSearchQuery(string query)
    {
        string? name = null, type = null;
        long? minSize = null, maxSize = null;
        DateTime? dateFilter = null;

        var remaining = new List<string>();
        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var lower = word.ToLowerInvariant();
            if (lower.StartsWith("type:") && word.Length > 5)
            {
                type = word[5..];
            }
            else if (lower.StartsWith("size:") && word.Length > 5)
            {
                var sizeStr = word[5..];
                if (ParseSizeFilter(sizeStr, out var min, out var max))
                {
                    minSize = min;
                    maxSize = max;
                }
            }
            else if (lower.StartsWith("date:") && word.Length > 5)
            {
                dateFilter = ParseDateFilter(word[5..]);
            }
            else
            {
                remaining.Add(word);
            }
        }
        if (remaining.Count > 0) name = string.Join(" ", remaining);
        return (name, type, minSize, maxSize, dateFilter);
    }

    static bool ParseSizeFilter(string s, out long? min, out long? max)
    {
        min = max = null;
        if (string.IsNullOrEmpty(s)) return false;

        bool isGreater = s.StartsWith(">");
        bool isLess = s.StartsWith("<");
        var numStr = s.TrimStart('>', '<');

        long multiplier = 1;
        if (numStr.EndsWith("gb", StringComparison.OrdinalIgnoreCase)) { multiplier = 1024L * 1024 * 1024; numStr = numStr[..^2]; }
        else if (numStr.EndsWith("mb", StringComparison.OrdinalIgnoreCase)) { multiplier = 1024L * 1024; numStr = numStr[..^2]; }
        else if (numStr.EndsWith("kb", StringComparison.OrdinalIgnoreCase)) { multiplier = 1024; numStr = numStr[..^2]; }
        else if (numStr.EndsWith("b", StringComparison.OrdinalIgnoreCase)) { multiplier = 1; numStr = numStr[..^1]; }

        if (!double.TryParse(numStr, out var num)) return false;
        var bytes = (long)(num * multiplier);

        if (isGreater) min = bytes;
        else if (isLess) max = bytes;
        else { min = max = bytes; } // exact
        return true;
    }

    static DateTime? ParseDateFilter(string s)
    {
        var now = DateTime.Now;
        return s.ToLowerInvariant() switch
        {
            "today" => now.Date,
            "yesterday" => now.Date.AddDays(-1),
            "week" => now.AddDays(-7),
            "month" => now.AddMonths(-1),
            "year" => now.AddYears(-1),
            _ => DateTime.TryParse(s, out var d) ? d : null
        };
    }

    // ── View toggle ──

    string _currentViewMode = "details";

    void OnViewModeChanged(object s, RoutedEventArgs e)
    {
        if (s is not MenuFlyoutItem item) return;
        var mode = item.Tag?.ToString() ?? "details";
        _currentViewMode = mode;

        // Hide all views
        FileList.Visibility = Visibility.Collapsed;
        FileGrid.Visibility = Visibility.Collapsed;
        FileGridMedium.Visibility = Visibility.Collapsed;
        FileGridSmall.Visibility = Visibility.Collapsed;
        FileListCompact.Visibility = Visibility.Collapsed;
        HeaderRow.Visibility = Visibility.Collapsed;

        // Show selected view
        switch (mode)
        {
            case "details":
                FileList.Visibility = Visibility.Visible;
                HeaderRow.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A1", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
                break;
            case "large":
                FileGrid.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A9", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
                break;
            case "medium":
                FileGridMedium.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A9", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 12 };
                break;
            case "small":
                FileGridSmall.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A9", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 10 };
                break;
            case "list":
                FileListCompact.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A1", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
                break;
        }
    }

    /// <summary>Switch to a specific view mode (details, large, medium, small, list).</summary>
    public void SwitchToView(string mode)
    {
        _currentViewMode = mode;

        // Hide all views
        FileList.Visibility = Visibility.Collapsed;
        FileGrid.Visibility = Visibility.Collapsed;
        FileGridMedium.Visibility = Visibility.Collapsed;
        FileGridSmall.Visibility = Visibility.Collapsed;
        FileListCompact.Visibility = Visibility.Collapsed;
        HeaderRow.Visibility = Visibility.Collapsed;

        // Show selected view
        switch (mode)
        {
            case "details":
                FileList.Visibility = Visibility.Visible;
                HeaderRow.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A1", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
                break;
            case "large":
                FileGrid.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A9", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
                break;
            case "medium":
                FileGridMedium.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A9", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 12 };
                break;
            case "small":
                FileGridSmall.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A9", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 10 };
                break;
            case "list":
                FileListCompact.Visibility = Visibility.Visible;
                ViewModeBtn.Content = new FontIcon { Glyph = "\uE8A1", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
                break;
        }
    }

    // ── Visual tree helper ──

    /// <summary>Walk up the visual tree to find the item container, then return its content.</summary>
    static VaultFileItem? FindItemFromSource(FrameworkElement? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is ListViewItem lvi) return lvi.Content as VaultFileItem;
            if (current is GridViewItem gvi) return gvi.Content as VaultFileItem;
            current = VisualTreeHelper.GetParent(current) as FrameworkElement;
        }
        return null;
    }

    // ── Interaction (at list/grid level, not DataTemplate level) ──

    void OnDoubleTap(object s, DoubleTappedRoutedEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        var item = FindItemFromSource(source);
        if (item != null)
        {
            e.Handled = true;
            ItemOpened?.Invoke(this, item);
        }
    }

    void OnListTapped(object s, TappedRoutedEventArgs e)
    {
        // Let ListView handle selection naturally
    }

    void OnListRightTapped(object s, RightTappedRoutedEventArgs e)
    {
        var source = e.OriginalSource as FrameworkElement;
        var item = FindItemFromSource(source);

        // Empty area context menu
        if (item == null)
        {
            var emptyMenu = new MenuFlyout();
            var newFolder = new MenuFlyoutItem { Text = Loc.Get("MainWindow", "NewFolder"), Icon = new FontIcon { Glyph = "\uE8A6" } };
            newFolder.Click += (_, _) => NewFolderRequested?.Invoke(this, EventArgs.Empty);
            var refresh = new MenuFlyoutItem { Text = Loc.Get("MainWindow", "Refresh"), Icon = new FontIcon { Glyph = "\uE72C" } };
            refresh.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

            emptyMenu.Items.Add(newFolder);
            emptyMenu.Items.Add(new MenuFlyoutSeparator());
            emptyMenu.Items.Add(refresh);

            var emptySource = source ?? (s as FrameworkElement);
            emptyMenu.ShowAt(emptySource!, e.GetPosition(emptySource));
            return;
        }

        // If the right-clicked item is not part of current selection, select only it
        var sel = SelectedItems;
        if (!sel.Contains(item))
        {
            if (FileList.Visibility == Visibility.Visible) FileList.SelectedItem = item;
            else FileGrid.SelectedItem = item;
            sel = new List<VaultFileItem> { item };
        }

        var menu = new MenuFlyout();

        if (sel.Count > 1)
        {
            var batchExport = new MenuFlyoutItem { Text = string.Format(Loc.Get("FileList", "BatchExport"), sel.Count), Icon = new FontIcon { Glyph = "\uE898" } };
            batchExport.Click += (_, _) => BatchExportRequested?.Invoke(this, sel);
            var batchDelete = new MenuFlyoutItem { Text = string.Format(Loc.Get("FileList", "BatchDelete"), sel.Count), Icon = new FontIcon { Glyph = "\uE74D" } };
            batchDelete.Click += (_, _) => BatchDeleteRequested?.Invoke(this, sel);

            menu.Items.Add(batchExport);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(batchDelete);
        }
        else
        {
            var favText = item.IsFavorited ? Loc.Get("FileList", "RemoveFavorite") : Loc.Get("FileList", "AddFavorite");
            var favGlyph = item.IsFavorited ? "\uE735" : "\uE734";
            var fav = new MenuFlyoutItem { Text = favText, Icon = new FontIcon { Glyph = favGlyph } };
            fav.Click += (_, _) => FavoriteToggleRequested?.Invoke(this, item);
            var open = new MenuFlyoutItem { Text = Loc.Get("FileList", "Open"), Icon = new FontIcon { Glyph = "\uE715" } };
            open.Click += (_, _) => ItemOpened?.Invoke(this, item);
            var export = new MenuFlyoutItem { Text = Loc.Get("FileList", "Export"), Icon = new FontIcon { Glyph = "\uE898" } };
            export.Click += (_, _) => ExportRequested?.Invoke(this, item);
            var rename = new MenuFlyoutItem { Text = Loc.Get("FileList", "Rename"), Icon = new FontIcon { Glyph = "\uE8AC" } };
            rename.Click += (_, _) => RenameRequested?.Invoke(this, item);
            var delete = new MenuFlyoutItem { Text = Loc.Get("FileList", "Delete"), Icon = new FontIcon { Glyph = "\uE74D" } };
            delete.Click += (_, _) => BatchDeleteRequested?.Invoke(this, new List<VaultFileItem> { item });

            menu.Items.Add(fav);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(open);
            menu.Items.Add(export);
            menu.Items.Add(rename);
            if (!item.IsFolder)
            {
                var versionHistory = new MenuFlyoutItem { Text = Loc.Get("FileList", "VersionHistory"), Icon = new FontIcon { Glyph = "\uE81C" } };
                versionHistory.Click += (_, _) => VersionHistoryRequested?.Invoke(this, item);
                menu.Items.Add(versionHistory);
            }
            var properties = new MenuFlyoutItem { Text = Loc.Get("FileList", "Properties"), Icon = new FontIcon { Glyph = "\uE946" } };
            properties.Click += (_, _) => PropertiesRequested?.Invoke(this, item);
            menu.Items.Add(properties);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(delete);
        }

        var feSource = source ?? (s as FrameworkElement);
        menu.ShowAt(feSource, e.GetPosition(feSource));
    }

    void OnListKeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete)
        {
            var sel = SelectedItems;
            if (sel.Count > 0)
                BatchDeleteRequested?.Invoke(this, sel);
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.F2)
        {
            var sel = (s is ListView lv ? lv.SelectedItem : (s is GridView gv ? gv.SelectedItem : null)) as VaultFileItem;
            if (sel != null) StartInlineRename(sel);
            e.Handled = true;
        }
    }

    // ── Inline rename ──

    TextBox? _activeRenameBox;
    VaultFileItem? _renamingItem;

    void StartInlineRename(VaultFileItem item)
    {
        // Find the container for this item
        var container = FileList.ContainerFromItem(item) as ListViewItem;
        if (container?.ContentTemplateRoot is not Grid grid || grid.Children.Count < 2) return;

        // Find the name TextBlock (column 1)
        if (grid.Children[1] is not TextBlock nameBlock) return;

        _renamingItem = item;
        _activeRenameBox = new TextBox
        {
            Text = item.Name,
            FontSize = nameBlock.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 100,
            Style = (Style)Application.Current.Resources["DefaultTextBoxStyle"]
        };

        // Select filename without extension
        var dotIndex = item.Name.LastIndexOf('.');
        if (dotIndex > 0)
        {
            _activeRenameBox.Select(0, dotIndex);
        }
        else
        {
            _activeRenameBox.SelectAll();
        }

        // Replace TextBlock with TextBox
        var col = Grid.GetColumn(nameBlock);
        var row = Grid.GetRow(nameBlock);
        nameBlock.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_activeRenameBox, col);
        Grid.SetRow(_activeRenameBox, row);
        grid.Children.Add(_activeRenameBox);

        _activeRenameBox.KeyDown += OnRenameKeyDown;
        _activeRenameBox.LostFocus += OnRenameLostFocus;
        _activeRenameBox.Loaded += (_, _) => _activeRenameBox.Focus(FocusState.Keyboard);
    }

    void OnRenameKeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelRename();
            e.Handled = true;
        }
    }

    void OnRenameLostFocus(object s, RoutedEventArgs e)
    {
        // Delay slightly to avoid conflicts with other focus events
        DispatcherQueue.TryEnqueue(() => CommitRename());
    }

    void CommitRename()
    {
        if (_activeRenameBox == null || _renamingItem == null) return;

        var newName = _activeRenameBox.Text?.Trim();
        var oldItem = _renamingItem;
        var box = _activeRenameBox;

        _activeRenameBox = null;
        _renamingItem = null;

        // Remove TextBox and restore TextBlock
        if (box.Parent is Grid grid)
        {
            grid.Children.Remove(box);
            // Find and restore the name TextBlock
            foreach (var child in grid.Children)
            {
                if (child is TextBlock tb && Grid.GetColumn(tb) == 1)
                {
                    tb.Visibility = Visibility.Visible;
                    break;
                }
            }
        }

        // Apply rename if name changed
        if (!string.IsNullOrWhiteSpace(newName) && newName != oldItem.Name)
        {
            InlineRenameRequested?.Invoke(this, (oldItem, newName));
        }
    }

    void CancelRename()
    {
        if (_activeRenameBox == null) return;

        var box = _activeRenameBox;
        _activeRenameBox = null;
        _renamingItem = null;

        if (box.Parent is Grid grid)
        {
            grid.Children.Remove(box);
            foreach (var child in grid.Children)
            {
                if (child is TextBlock tb && Grid.GetColumn(tb) == 1)
                {
                    tb.Visibility = Visibility.Visible;
                    break;
                }
            }
        }
    }

    void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Get total original size of selected items.</summary>
    public long GetSelectedTotalSize() => SelectedItems.Sum(i => i.OriginalSize);

    public void RemoveItems(IEnumerable<VaultFileItem> items)
    {
        foreach (var i in items.ToList()) { _all.Remove(i); _items.Remove(i); _previewCache.Remove(i.Id); }
    }

    public void SelectAll()
    {
        switch (_currentViewMode)
        {
            case "details": FileList.SelectAll(); break;
            case "large": FileGrid.SelectAll(); break;
            case "medium": FileGridMedium.SelectAll(); break;
            case "small": FileGridSmall.SelectAll(); break;
            case "list": FileListCompact.SelectAll(); break;
        }
    }

    public void FocusFirstItem()
    {
        if (_items.Count == 0) return;
        var first = _items[0];
        switch (_currentViewMode)
        {
            case "details": FileList.SelectedItem = first; FileList.ScrollIntoView(first); break;
            case "large": FileGrid.SelectedItem = first; FileGrid.ScrollIntoView(first); break;
            case "medium": FileGridMedium.SelectedItem = first; FileGridMedium.ScrollIntoView(first); break;
            case "small": FileGridSmall.SelectedItem = first; FileGridSmall.ScrollIntoView(first); break;
            case "list": FileListCompact.SelectedItem = first; FileListCompact.ScrollIntoView(first); break;
        }
    }

    public void FocusLastItem()
    {
        if (_items.Count == 0) return;
        var last = _items[_items.Count - 1];
        switch (_currentViewMode)
        {
            case "details": FileList.SelectedItem = last; FileList.ScrollIntoView(last); break;
            case "large": FileGrid.SelectedItem = last; FileGrid.ScrollIntoView(last); break;
            case "medium": FileGridMedium.SelectedItem = last; FileGridMedium.ScrollIntoView(last); break;
            case "small": FileGridSmall.SelectedItem = last; FileGridSmall.ScrollIntoView(last); break;
            case "list": FileListCompact.SelectedItem = last; FileListCompact.ScrollIntoView(last); break;
        }
    }

    // ── Drag-drop ──

    async void OnDragStart(object s, DragItemsStartingEventArgs e)
    {
        try
        {
            var items = e.Items.Cast<VaultFileItem>().ToList();
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;

            // Decrypt items to temp and provide as StorageItems
            var tempDir = Path.Combine(Path.GetTempPath(), "GyroownDragOut");
            Directory.CreateDirectory(tempDir);
            var files = new List<Windows.Storage.StorageFile>();

            foreach (var item in items)
            {
                try
                {
                    var uniqueName = items.Count(i => i.Name == item.Name) > 1
                        ? $"{Path.GetFileNameWithoutExtension(item.Name)}_{item.Id[..8]}{Path.GetExtension(item.Name)}"
                        : item.Name;
                    var tempPath = Path.Combine(tempDir, uniqueName);
                    if (DecryptToFile != null)
                        await DecryptToFile(item.Id, tempPath);
                    var sf = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempPath);
                    files.Add(sf);
                }
                catch (Exception ex) { LogService.Warn($"DragOut decrypt failed for {item.Name}: {ex.Message}"); }
            }

            if (files.Count > 0)
            {
                e.Data.SetStorageItems(files);
                e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            }

            DragOutRequested?.Invoke(this, items);

            // Schedule cleanup of temp files after a delay (drag needs files alive during transfer)
            _ = CleanupDragOutTempAsync(tempDir);
        }
        catch (Exception ex)
        {
            LogService.Error($"VaultFileListView.OnDragStart: {ex}");
        }
    }

    static async Task CleanupDragOutTempAsync(string tempDir)
    {
        await Task.Delay(30_000); // 30s grace period for drag transfer to complete
        try
        {
            if (Directory.Exists(tempDir))
            {
                foreach (var f in Directory.GetFiles(tempDir))
                {
                    try { File.Delete(f); } catch (Exception ex) { LogService.Debug($"Cleanup temp file failed: {ex.Message}"); }
                }
                try { Directory.Delete(tempDir, true); } catch (Exception ex) { LogService.Debug($"Cleanup temp dir failed: {ex.Message}"); }
            }
        }
        catch (Exception ex) { LogService.Debug($"CleanupDragOutTemp failed: {ex.Message}"); }
    }

    void OnDragOver(object s, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.Caption = Loc.Get("MainWindow", "EncryptStore");
    }

    async void OnDrop(object s, DragEventArgs e)
    {
        try
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var si = await e.DataView.GetStorageItemsAsync();
                DropInRequested?.Invoke(this, si.Select(x => x.Path).ToList());
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"VaultFileListView.OnDrop: {ex}");
        }
    }

    // ── Column resizing ──
    private int _splitterCol = -1;
    private double _splitterStartX;

    void OnSplitterPressed(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (s is Border b) _splitterCol = Grid.GetColumn(b);
        _splitterStartX = e.GetCurrentPoint(this).Position.X;
        (s as UIElement)?.CapturePointer(e.Pointer);
    }

    void OnSplitterMoved(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_splitterCol < 0) return;
        var dx = e.GetCurrentPoint(this).Position.X - _splitterStartX;
        var defs = HeaderRow.ColumnDefinitions;
        // Resize the columns on either side of the splitter
        var leftCol = _splitterCol - 1;
        var rightCol = _splitterCol + 1;
        if (leftCol >= 0 && rightCol < defs.Count)
        {
            var leftW = defs[leftCol].ActualWidth;
            var rightW = defs[rightCol].ActualWidth;
            var newLeft = Math.Max(40, leftW + dx);
            var newRight = Math.Max(40, rightW - dx);
            // Use star sizing to maintain proportions on window resize
            var total = newLeft + newRight;
            if (total > 0)
            {
                defs[leftCol].Width = new GridLength(newLeft / total, GridUnitType.Star);
                defs[rightCol].Width = new GridLength(newRight / total, GridUnitType.Star);
            }
            _splitterStartX = e.GetCurrentPoint(this).Position.X;
        }
    }

    void OnSplitterReleased(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _splitterCol = -1;
        (s as UIElement)?.ReleasePointerCapture(e.Pointer);
    }

    void OnSplitterDoubleTapped(object s, DoubleTappedRoutedEventArgs e)
    {
        if (s is not Border b) return;
        var col = Grid.GetColumn(b);
        var defs = HeaderRow.ColumnDefinitions;

        // Auto-fit: measure max content width in the left column
        var leftCol = col - 1;
        if (leftCol < 0) return;

        double maxWidth = 40; // minimum
        foreach (var item in _items)
        {
            string text = leftCol switch
            {
                2 => item.Name,
                4 => item.FormattedSize,
                6 => item.ContentType,
                8 => item.ModifiedAt.ToString("yyyy-MM-dd HH:mm"),
                _ => ""
            };
            // Approximate text width (14px font, ~8px per char average)
            maxWidth = Math.Max(maxWidth, text.Length * 8 + 32);
        }

        // Cap at 400px
        maxWidth = Math.Min(maxWidth, 400);

        var rightCol = col + 1;
        if (rightCol < defs.Count)
        {
            var leftW = defs[leftCol].ActualWidth;
            var rightW = defs[rightCol].ActualWidth;
            var diff = maxWidth - leftW;
            var newRight = Math.Max(40, rightW - diff);
            var total = maxWidth + newRight;
            if (total > 0)
            {
                defs[leftCol].Width = new GridLength(maxWidth / total, GridUnitType.Star);
                defs[rightCol].Width = new GridLength(newRight / total, GridUnitType.Star);
            }
        }
        e.Handled = true;
    }

    // ── Column header right-click menu ──

    void OnHeaderRightTapped(object s, RightTappedRoutedEventArgs e)
    {
        var menu = new MenuFlyout();

        var sizeItem = new ToggleMenuFlyoutItem { Text = Loc.Get("FileList", "Size"), IsChecked = _showSizeCol };
        sizeItem.Click += (_, _) => ToggleColumn("size", sizeItem);
        var typeItem = new ToggleMenuFlyoutItem { Text = Loc.Get("FileList", "Type"), IsChecked = _showTypeCol };
        typeItem.Click += (_, _) => ToggleColumn("type", typeItem);
        var dateItem = new ToggleMenuFlyoutItem { Text = Loc.Get("FileList", "Date"), IsChecked = _showDateCol };
        dateItem.Click += (_, _) => ToggleColumn("date", dateItem);

        menu.Items.Add(sizeItem);
        menu.Items.Add(typeItem);
        menu.Items.Add(dateItem);

        var feSource = s as FrameworkElement;
        menu.ShowAt(feSource!, e.GetPosition(feSource));
    }

    void ToggleColumn(string col, ToggleMenuFlyoutItem item)
    {
        switch (col)
        {
            case "size":
                _showSizeCol = !_showSizeCol;
                item.IsChecked = _showSizeCol;
                SortSize.Visibility = _showSizeCol ? Visibility.Visible : Visibility.Collapsed;
                Split2.Visibility = _showSizeCol ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "type":
                _showTypeCol = !_showTypeCol;
                item.IsChecked = _showTypeCol;
                SortType.Visibility = _showTypeCol ? Visibility.Visible : Visibility.Collapsed;
                Split3.Visibility = _showTypeCol ? Visibility.Visible : Visibility.Collapsed;
                break;
            case "date":
                _showDateCol = !_showDateCol;
                item.IsChecked = _showDateCol;
                SortDate.Visibility = _showDateCol ? Visibility.Visible : Visibility.Collapsed;
                break;
        }
        UpdateHeaderColumnVisibility();
    }

    void UpdateHeaderColumnVisibility()
    {
        // Update column definitions to collapse hidden columns
        var defs = HeaderRow.ColumnDefinitions;
        // Column 4 = size, 6 = type, 8 = date
        defs[4].Width = _showSizeCol ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        defs[6].Width = _showTypeCol ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        defs[8].Width = _showDateCol ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        // Update DataTemplate column visibility via code
        UpdateDetailsTemplateColumns();
    }

    void UpdateDetailsTemplateColumns()
    {
        // Walk through visible items and toggle column TextBlocks
        foreach (var item in FileList.Items)
        {
            if (FileList.ContainerFromItem(item) is ListViewItem container && container.ContentTemplateRoot is Grid grid)
            {
                // Column 2 = size, 3 = type, 4 = date
                if (grid.Children.Count >= 5)
                {
                    if (grid.Children[2] is FrameworkElement sizeEl) sizeEl.Visibility = _showSizeCol ? Visibility.Visible : Visibility.Collapsed;
                    if (grid.Children[3] is FrameworkElement typeEl) typeEl.Visibility = _showTypeCol ? Visibility.Visible : Visibility.Collapsed;
                    if (grid.Children[4] is FrameworkElement dateEl) dateEl.Visibility = _showDateCol ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    // ── Rubber band selection ──

    private Windows.Foundation.Point _rubberBandStart;
    private bool _rubberBandActive;
    private Microsoft.UI.Xaml.Shapes.Rectangle? _rubberBandRect;

    void OnRubberBandPressed(object s, PointerRoutedEventArgs e)
    {
        var pos = e.GetCurrentPoint(RubberBandCanvas).Position;
        _rubberBandStart = pos;
        _rubberBandActive = true;

        // Create rubber band rectangle
        _rubberBandRect = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 120, 215)),
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(150, 0, 120, 215)),
            StrokeThickness = 1,
            Width = 0,
            Height = 0
        };
        Canvas.SetLeft(_rubberBandRect, pos.X);
        Canvas.SetTop(_rubberBandRect, pos.Y);
        RubberBandCanvas.Children.Add(_rubberBandRect);

        RubberBandCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    void OnRubberBandMoved(object s, PointerRoutedEventArgs e)
    {
        if (!_rubberBandActive || _rubberBandRect == null) return;

        var pos = e.GetCurrentPoint(RubberBandCanvas).Position;
        var x = Math.Min(_rubberBandStart.X, pos.X);
        var y = Math.Min(_rubberBandStart.Y, pos.Y);
        var w = Math.Abs(pos.X - _rubberBandStart.X);
        var h = Math.Abs(pos.Y - _rubberBandStart.Y);

        Canvas.SetLeft(_rubberBandRect, x);
        Canvas.SetTop(_rubberBandRect, y);
        _rubberBandRect.Width = w;
        _rubberBandRect.Height = h;

        // Select items within the rubber band
        SelectItemsInRegion(new Windows.Foundation.Rect(x, y, w, h));
        e.Handled = true;
    }

    void OnRubberBandReleased(object s, PointerRoutedEventArgs e)
    {
        _rubberBandActive = false;
        RubberBandCanvas.Children.Clear();
        _rubberBandRect = null;
        RubberBandCanvas.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    void SelectItemsInRegion(Windows.Foundation.Rect region)
    {
        // Get the active list view
        var activeView = _currentViewMode switch
        {
            "details" => (ItemsControl)FileList,
            "large" => (ItemsControl)FileGrid,
            "medium" => (ItemsControl)FileGridMedium,
            "small" => (ItemsControl)FileGridSmall,
            "list" => (ItemsControl)FileListCompact,
            _ => (ItemsControl)FileList
        };

        // Clear current selection
        if (activeView is ListView lv) lv.SelectedItems.Clear();
        else if (activeView is GridView gv) gv.SelectedItems.Clear();

        // Check each item
        foreach (var item in _items)
        {
            var container = activeView.ContainerFromItem(item) as FrameworkElement;
            if (container == null) continue;

            var bounds = container.TransformToVisual(RubberBandCanvas)
                .TransformBounds(new Windows.Foundation.Rect(0, 0, container.ActualWidth, container.ActualHeight));

            if (Intersects(region, bounds))
            {
                if (activeView is ListView lv2) lv2.SelectedItems.Add(item);
                else if (activeView is GridView gv2) gv2.SelectedItems.Add(item);
            }
        }
    }

    static bool Intersects(Windows.Foundation.Rect a, Windows.Foundation.Rect b) =>
        a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
}
