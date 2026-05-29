using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Gyroown.Services;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Gyroown.Models;
using System.Collections.ObjectModel;

namespace Gyroown.Controls;

public sealed partial class VaultFileListView : UserControl
{
    private readonly ObservableCollection<VaultFileItem> _items = new();
    private readonly ObservableCollection<VaultFileItem> _all = new();
    private readonly Dictionary<string, Microsoft.UI.Xaml.Media.Imaging.BitmapImage> _previewCache = new();
    private Services.VaultService? _vault;
    private string _sortCol = "name";
    private bool _sortAsc = true;
    private string _filter = "";
    private string _filterPath = "/";
    private SearchFilter _searchFilter = new();

    public event EventHandler<IReadOnlyList<VaultFileItem>>? DragOutRequested;
    public event EventHandler<IReadOnlyList<string>>? DropInRequested;
    public event EventHandler<VaultFileItem>? ItemOpened;
    public event EventHandler<VaultFileItem>? RenameRequested;
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
        FileList.ContainerContentChanging += OnContainerContentChanging;
        FileGrid.ContainerContentChanging += OnContainerContentChanging;
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
            catch { }
            args.Handled = true;
        }
    }

    public IReadOnlyList<VaultFileItem> SelectedItems
    {
        get
        {
            var src = FileList.Visibility == Visibility.Visible ? FileList.SelectedItems : FileGrid.SelectedItems;
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

        q = _sortCol switch
        {
            "size" => _sortAsc ? q.OrderBy(i => i.OriginalSize) : q.OrderByDescending(i => i.OriginalSize),
            "type" => _sortAsc ? q.OrderBy(i => i.ContentType) : q.OrderByDescending(i => i.ContentType),
            "date" => _sortAsc ? q.OrderBy(i => i.ModifiedAt) : q.OrderByDescending(i => i.ModifiedAt),
            _ => _sortAsc ? q.OrderBy(i => i.Name) : q.OrderByDescending(i => i.Name),
        };
        _items.Clear();
        foreach (var i in q) _items.Add(i);

        // Show empty state when filter has no results or folder is empty
        var hasFilter = !string.IsNullOrWhiteSpace(_filter) || _searchFilter.HasAdvancedFilters;
        var showEmpty = _items.Count == 0 && (hasFilter || _filterPath != "/");
        EmptyState.Visibility = showEmpty ? Visibility.Visible : Visibility.Collapsed;
        var emptySearchText = _searchFilter.IsEmpty ? _filter : _searchFilter.TextQuery;
        EmptyStateText.Text = hasFilter
            ? string.Format(Loc.Get("FileList", "NoResults"),
                string.IsNullOrWhiteSpace(emptySearchText) ? Loc.Get("FileList", "AdvancedSearch") : emptySearchText)
            : Loc.Get("FileList", "EmptyFolder");
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

    void OnViewChanged(object s, RoutedEventArgs e)
    {
        if (s is not ToggleButton b) return;
        DetailsBtn.IsChecked = b.Tag?.ToString() == "details";
        IconsBtn.IsChecked = b.Tag?.ToString() == "icons";
        TilesBtn.IsChecked = b.Tag?.ToString() == "tiles";
        var isDetails = DetailsBtn.IsChecked == true;
        HeaderRow.Visibility = isDetails ? Visibility.Visible : Visibility.Collapsed;
        FileList.Visibility = isDetails ? Visibility.Visible : Visibility.Collapsed;
        FileGrid.Visibility = !isDetails ? Visibility.Visible : Visibility.Collapsed;
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
                catch { }
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
                    try { File.Delete(f); } catch { }
                }
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
        catch { }
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

    // ── Interaction ──

    void OnDoubleTap(object s, DoubleTappedRoutedEventArgs e)
    {
        // Only open if clicking on an actual item, not empty space
        VaultFileItem? item = null;
        if (s is ListView lv && lv.SelectedItem is VaultFileItem fi) item = fi;
        else if (s is GridView gv && gv.SelectedItem is VaultFileItem fi2) item = fi2;
        if (item != null) ItemOpened?.Invoke(this, item);
    }

    void OnEmptyTap(object s, TappedRoutedEventArgs e)
    {
        // Clicking empty space deselects all
        if (s is ListView lv)
        {
            var hit = e.OriginalSource as FrameworkElement;
            if (hit?.DataContext is not VaultFileItem) lv.SelectedItem = null;
        }
        else if (s is GridView gv)
        {
            var hit = e.OriginalSource as FrameworkElement;
            if (hit?.DataContext is not VaultFileItem) gv.SelectedItem = null;
        }
    }

    void OnItemRightTap(object s, RightTappedRoutedEventArgs e)
    {
        if (s is not FrameworkElement fe || fe.DataContext is not VaultFileItem item) return;

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
            // Batch operations
            var batchExport = new MenuFlyoutItem { Text = string.Format(Loc.Get("FileList", "BatchExport"), sel.Count), Icon = new FontIcon { Glyph = "\uEDE1" } };
            batchExport.Click += (_, _) => BatchExportRequested?.Invoke(this, sel);
            var batchDelete = new MenuFlyoutItem { Text = string.Format(Loc.Get("FileList", "BatchDelete"), sel.Count), Icon = new FontIcon { Glyph = "\uE74D" } };
            batchDelete.Click += (_, _) => BatchDeleteRequested?.Invoke(this, sel);

            menu.Items.Add(batchExport);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(batchDelete);
        }
        else
        {
            // Single item operations
            var favText = item.IsFavorited ? Loc.Get("FileList", "RemoveFavorite") : Loc.Get("FileList", "AddFavorite");
            var favGlyph = item.IsFavorited ? "\uE735" : "\uE734";
            var fav = new MenuFlyoutItem { Text = favText, Icon = new FontIcon { Glyph = favGlyph } };
            fav.Click += (_, _) => FavoriteToggleRequested?.Invoke(this, item);
            var open = new MenuFlyoutItem { Text = Loc.Get("FileList", "Open"), Icon = new FontIcon { Glyph = "\uE76E" } };
            open.Click += (_, _) => ItemOpened?.Invoke(this, item);
            var export = new MenuFlyoutItem { Text = Loc.Get("FileList", "Export"), Icon = new FontIcon { Glyph = "\uEDE1" } };
            export.Click += (_, _) => ExportRequested?.Invoke(this, item);
            var rename = new MenuFlyoutItem { Text = Loc.Get("FileList", "Rename"), Icon = new FontIcon { Glyph = "\uE8AC" } };
            rename.Click += (_, _) => RenameRequested?.Invoke(this, item);
            var delete = new MenuFlyoutItem { Text = Loc.Get("FileList", "Delete"), Icon = new FontIcon { Glyph = "\uE74D" } };
            delete.Click += (_, _) => BatchDeleteRequested?.Invoke(this, new List<VaultFileItem> { item });

            menu.Items.Add(fav);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(export);
            menu.Items.Add(rename);
            if (!item.IsFolder)
            {
                var versionHistory = new MenuFlyoutItem { Text = Loc.Get("FileList", "VersionHistory"), Icon = new FontIcon { Glyph = "\uE777" } };
                versionHistory.Click += (_, _) => VersionHistoryRequested?.Invoke(this, item);
                menu.Items.Add(versionHistory);
            }
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(delete);
        }

        menu.ShowAt(fe, e.GetPosition(fe));
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
            if (sel != null) RenameRequested?.Invoke(this, sel);
            e.Handled = true;
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
        if (FileList.Visibility == Visibility.Visible)
            FileList.SelectAll();
        else if (FileGrid.Visibility == Visibility.Visible)
            FileGrid.SelectAll();
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
}
