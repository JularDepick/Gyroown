using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Gyroown.Models;
using Gyroown.Services;
using WinRT.Interop;

namespace Gyroown.Controls.Preview;

public sealed partial class FilePreviewWindow : Window
{
    private readonly VaultService _vault;
    private readonly IReadOnlyList<VaultFileItem> _files;
    private int _currentIndex;
    private int _loadGeneration;
    private readonly Func<string, Task<(string Name, byte[] Data)>> _loadContent;
    private ImagePreviewControl? _imageViewer;
    private TextPreviewControl? _textViewer;
    private VideoPreviewControl? _videoViewer;

    public FilePreviewWindow(
        VaultService vault,
        IReadOnlyList<VaultFileItem> files,
        int startIndex,
        Func<string, Task<(string Name, byte[] Data)>> loadContent)
    {
        _vault = vault;
        _files = files;
        _currentIndex = startIndex;
        _loadContent = loadContent;

        InitializeComponent();

        // Window configuration
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.ResizeClient(new Windows.Graphics.SizeInt32(1100, 750));
        appWindow.Title = $"{AppInfo.Name} — View";

        // Try to make window resizable
        try
        {
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
            }
        }
        catch { /* degrade gracefully */ }

        // Load current file
        _ = LoadCurrentFileAsync();

        // Cleanup media resources on window close
        Closed += (_, _) => CleanupViewers();
    }

    // ── Keyboard shortcuts ──

    void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Skip if text input is focused
        var focused = FocusManager.GetFocusedElement(this.Content.XamlRoot);
        if (focused is TextBox) return;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                Close();
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Left:
                NavigateTo(_currentIndex - 1);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Right:
                NavigateTo(_currentIndex + 1);
                e.Handled = true;
                break;
        }
    }

    // ── Navigation ──

    void OnPrevFile(object sender, RoutedEventArgs e) => NavigateTo(_currentIndex - 1);
    void OnNextFile(object sender, RoutedEventArgs e) => NavigateTo(_currentIndex + 1);
    void OnClose(object sender, RoutedEventArgs e) => Close();

    void NavigateTo(int index)
    {
        if (index < 0 || index >= _files.Count) return;
        _currentIndex = index;
        _ = LoadCurrentFileAsync();
    }

    async Task LoadCurrentFileAsync()
    {
        if (_currentIndex < 0 || _currentIndex >= _files.Count) return;

        var generation = ++_loadGeneration;
        var item = _files[_currentIndex];

        // Update header
        FileName.Text = item.Name;
        FileCounter.Text = $"{_currentIndex + 1} / {_files.Count}";

        // Update bottom bar
        FileFullName.Text = $"{item.VirtualPath}{item.Name}";
        FileSizeText.Text = item.FormattedSize;
        FileTypeText.Text = item.ContentType;
        FileDateText.Text = item.ModifiedAt.ToString("yyyy-MM-dd HH:mm:ss");

        // Update navigation buttons
        PrevBtn.IsEnabled = _currentIndex > 0;
        NextBtn.IsEnabled = _currentIndex < _files.Count - 1;

        // Cleanup previous viewer
        CleanupViewers();

        // Load content and show appropriate viewer
        try
        {
            var (name, data) = await _loadContent(item.Id);
            if (generation != _loadGeneration) return; // stale load, discard
            ShowViewer(item, data);
        }
        catch
        {
            if (generation != _loadGeneration) return;
            ViewerHost.Children.Add(new TextBlock
            {
                Text = $"Failed to load: {item.Name}",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            });
        }
    }

    void ShowViewer(VaultFileItem item, byte[] data)
    {
        var ct = item.ContentType;

        if (ct.StartsWith("image/"))
        {
            _imageViewer = new ImagePreviewControl();
            _imageViewer.SlideshowNext += (_, _) => NavigateTo(_currentIndex + 1);
            _imageViewer.CloseSlideshow += (_, _) => _imageViewer.StopSlideshow();
            ViewerHost.Children.Add(_imageViewer);
            _ = LoadImageAsync(data);
        }
        else if (ct.StartsWith("text/"))
        {
            _textViewer = new TextPreviewControl();
            _textViewer.LoadText(data, item.Name);
            ViewerHost.Children.Add(_textViewer);
        }
        else if (ct.StartsWith("video/") || ct.StartsWith("audio/"))
        {
            _videoViewer = new VideoPreviewControl();
            var ms = new MemoryStream(data);
            _videoViewer.LoadVideo(ms.AsRandomAccessStream(), ct);
            ViewerHost.Children.Add(_videoViewer);
        }
        else
        {
            // Generic info display for unsupported types
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8
            };
            panel.Children.Add(new FontIcon
            {
                Glyph = item.IconGlyph,
                FontSize = 48,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = item.Name,
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"{item.ContentType} — {item.FormattedSize}",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center
            });
            ViewerHost.Children.Add(panel);
        }
    }

    async Task LoadImageAsync(byte[] data)
    {
        try
        {
            var bmp = new BitmapImage();
            var ms = new MemoryStream(data);
            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
            _imageViewer?.SetImage(bmp);
        }
        catch { /* image load failure is non-fatal */ }
    }

    void CleanupViewers()
    {
        _videoViewer?.Cleanup();
        _videoViewer = null;
        _imageViewer?.StopSlideshow();
        _imageViewer = null;
        _textViewer = null;
        ViewerHost.Children.Clear();
    }
}
