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
    private string _sortCol = "name";
    private bool _sortAsc = true;
    private string _filter = "";
    private string _filterPath = "/";

    public event EventHandler<IReadOnlyList<VaultFileItem>>? DragOutRequested;
    public event EventHandler<IReadOnlyList<string>>? DropInRequested;
    public event EventHandler<VaultFileItem>? ItemOpened;
    public event EventHandler<VaultFileItem>? RenameRequested;
    public event EventHandler<VaultFileItem>? ExportRequested;
    public Func<string, string, Task>? DecryptToFile { get; set; }

    public VaultFileListView()
    {
        InitializeComponent();
        FileList.ItemsSource = _items;
        FileGrid.ItemsSource = _items;

    }

    public void SetItems(IEnumerable<VaultFileItem> items)
    {
        _all.Clear();
        foreach (var i in items) _all.Add(i);
        ApplyFilter();
    }

    public async Task LoadPreviewsAsync(Services.VaultService vault)
    {
        foreach (var item in _all)
        {
            if (item.ContentType.StartsWith("image/"))
            {
                try
                {
                    var pid = vault.GetPreviewId(item.Id);
                    if (pid != null)
                    {
                        var data = await vault.GetPreviewData(pid);
                        if (data != null)
                        {
                            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            using var ms = new MemoryStream(data);
                            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                            item.PreviewImage = bmp;
                        }
                    }
                }
                catch { }
            }
        }
        ApplyFilter(); // refresh to show loaded previews
    }

    public IReadOnlyList<VaultFileItem> SelectedItems
    {
        get
        {
            var src = FileList.Visibility == Visibility.Visible ? FileList.SelectedItems : FileGrid.SelectedItems;
            return src.Cast<VaultFileItem>().ToList();
        }
    }

    public string Filter
    {
        get => _filter;
        set { _filter = value; ApplyFilter(); }
    }

    public string FilterPath
    {
        get => _filterPath;
        set { _filterPath = value ?? "/"; ApplyFilter(); }
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
            q = q.Where(i => i.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase));
        if (_filterPath != "/")
            q = q.Where(i => i.VirtualPath == _filterPath || i.VirtualPath.StartsWith(_filterPath + "/"));
        q = _sortCol switch
        {
            "size" => _sortAsc ? q.OrderBy(i => i.OriginalSize) : q.OrderByDescending(i => i.OriginalSize),
            "type" => _sortAsc ? q.OrderBy(i => i.ContentType) : q.OrderByDescending(i => i.ContentType),
            "date" => _sortAsc ? q.OrderBy(i => i.ModifiedAt) : q.OrderByDescending(i => i.ModifiedAt),
            _ => _sortAsc ? q.OrderBy(i => i.Name) : q.OrderByDescending(i => i.Name),
        };
        _items.Clear();
        foreach (var i in q) _items.Add(i);
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
                var tempPath = Path.Combine(tempDir, item.Name);
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
    }

    void OnDragOver(object s, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.Caption = Loc.Get("MainWindow", "EncryptStore");
    }

    async void OnDrop(object s, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            var si = await e.DataView.GetStorageItemsAsync();
            DropInRequested?.Invoke(this, si.Select(x => x.Path).ToList());
        }
    }

    // ── Interaction ──

    void OnDoubleTap(object s, DoubleTappedRoutedEventArgs e)
    {
        VaultFileItem? item = null;
        if (s is ListView lv && lv.SelectedItem is VaultFileItem fi) item = fi;
        else if (s is GridView gv && gv.SelectedItem is VaultFileItem fi2) item = fi2;
        if (item != null) ItemOpened?.Invoke(this, item);
    }

    void OnItemRightTap(object s, RightTappedRoutedEventArgs e)
    {
        if (s is not FrameworkElement fe || fe.DataContext is not VaultFileItem item) return;

        var menu = new MenuFlyout();
        var export = new MenuFlyoutItem { Text = Loc.Get("FileList", "Export") };
        export.Click += (_, _) => ExportRequested?.Invoke(this, item);
        var rename = new MenuFlyoutItem { Text = Loc.Get("FileList", "Rename") };
        rename.Click += (_, _) => RenameRequested?.Invoke(this, item);
        var delete = new MenuFlyoutItem { Text = Loc.Get("FileList", "Delete") };
        delete.Click += (_, _) => { _all.Remove(item); _items.Remove(item); };

        menu.Items.Add(export);
        menu.Items.Add(rename);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(delete);

        menu.ShowAt(fe, e.GetPosition(fe));
    }

    void OnListKeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Delete)
        {
            var sel = SelectedItems;
            foreach (var i in sel) { _all.Remove(i); _items.Remove(i); }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.F2)
        {
            var sel = (s is ListView lv ? lv.SelectedItem : (s is GridView gv ? gv.SelectedItem : null)) as VaultFileItem;
            if (sel != null) RenameRequested?.Invoke(this, sel);
            e.Handled = true;
        }
    }

    public void RemoveItems(IEnumerable<VaultFileItem> items)
    {
        foreach (var i in items.ToList()) { _all.Remove(i); _items.Remove(i); }
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
            defs[leftCol].Width = new GridLength(newLeft, GridUnitType.Pixel);
            defs[rightCol].Width = new GridLength(newRight, GridUnitType.Pixel);
            _splitterStartX = e.GetCurrentPoint(this).Position.X;
        }
    }

    void OnSplitterReleased(object s, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        _splitterCol = -1;
        (s as UIElement)?.ReleasePointerCapture(e.Pointer);
    }
}
