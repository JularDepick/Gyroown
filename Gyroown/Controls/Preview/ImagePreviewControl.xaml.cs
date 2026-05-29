using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Gyroown.Controls.Preview;

public sealed partial class ImagePreviewControl : UserControl
{
    private double _zoomFactor = 1.0;
    private bool _isPanning;
    private Windows.Foundation.Point _panStart;
    private double _panOffsetX, _panOffsetY;
    private bool _fitToWindow = true;
    private readonly DispatcherTimer _slideshowTimer = new();
    private bool _suppressFitToggle;

    public event EventHandler? SlideshowNext;
    public event EventHandler? CloseSlideshow;

    public ImagePreviewControl()
    {
        InitializeComponent();
        _slideshowTimer.Interval = TimeSpan.FromSeconds(3);
        _slideshowTimer.Tick += (_, _) => SlideshowNext?.Invoke(this, EventArgs.Empty);
        ImageScroller.ViewChanged += (_, _) => UpdateZoomDisplay();
        Loaded += (_, _) =>
        {
            if (_fitToWindow) FitToWindow();
        };
        SizeChanged += (_, _) =>
        {
            if (_fitToWindow) FitToWindow();
        };
    }

    public void SetImage(BitmapImage bmp)
    {
        PreviewImage.Source = bmp;
        if (_fitToWindow)
        {
            // Defer fitting to after layout
            DispatcherQueue.TryEnqueue(() => FitToWindow());
        }
        else
        {
            _zoomFactor = 1.0;
            ApplyZoom();
        }
    }

    // ── Zoom ──

    void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(ImageScroller);
        var delta = pt.Properties.MouseWheelDelta;
        var step = delta > 0 ? 0.15 : -0.15;
        var newZoom = Math.Clamp(_zoomFactor + step, 0.1, 20.0);

        if (Math.Abs(newZoom - _zoomFactor) < 0.001) return;

        // Zoom at mouse position
        var pos = pt.Position;
        var hRatio = ImageScroller.HorizontalOffset / Math.Max(1, ImageScroller.ExtentWidth - ImageScroller.ViewportWidth);
        var vRatio = ImageScroller.VerticalOffset / Math.Max(1, ImageScroller.ExtentHeight - ImageScroller.ViewportHeight);

        _zoomFactor = newZoom;
        _fitToWindow = false;
        _suppressFitToggle = true;
        FitToggle.IsChecked = false;
        _suppressFitToggle = false;
        ApplyZoom();

        // Restore scroll position approximately
        DispatcherQueue.TryEnqueue(() =>
        {
            var newH = hRatio * Math.Max(0, ImageScroller.ExtentWidth - ImageScroller.ViewportWidth);
            var newV = vRatio * Math.Max(0, ImageScroller.ExtentHeight - ImageScroller.ViewportHeight);
            ImageScroller.ChangeView(newH, newV, null, true);
        });

        ShowZoomIndicator();
        e.Handled = true;
    }

    void OnZoomIn(object sender, RoutedEventArgs e)
    {
        _zoomFactor = Math.Clamp(_zoomFactor + 0.25, 0.1, 20.0);
        _fitToWindow = false;
        _suppressFitToggle = true;
        FitToggle.IsChecked = false;
        _suppressFitToggle = false;
        ApplyZoom();
        ShowZoomIndicator();
    }

    void OnZoomOut(object sender, RoutedEventArgs e)
    {
        _zoomFactor = Math.Clamp(_zoomFactor - 0.25, 0.1, 20.0);
        _fitToWindow = false;
        _suppressFitToggle = true;
        FitToggle.IsChecked = false;
        _suppressFitToggle = false;
        ApplyZoom();
        ShowZoomIndicator();
    }

    void OnResetZoom(object sender, RoutedEventArgs e)
    {
        if (_fitToWindow)
        {
            FitToWindow();
        }
        else
        {
            _zoomFactor = 1.0;
            ApplyZoom();
        }
        ShowZoomIndicator();
    }

    void FitToWindow()
    {
        if (PreviewImage.Source == null) return;
        var availW = ImageScroller.ViewportWidth;
        var availH = ImageScroller.ViewportHeight;
        if (availW < 1 || availH < 1) return;

        // Get natural image size
        if (PreviewImage.Source is BitmapImage bmp && bmp.PixelWidth > 0 && bmp.PixelHeight > 0)
        {
            var scaleW = availW / bmp.PixelWidth;
            var scaleH = availH / bmp.PixelHeight;
            _zoomFactor = Math.Min(scaleW, scaleH);
            // Don't zoom beyond natural size unless needed
            _zoomFactor = Math.Min(_zoomFactor, 1.0);
        }
        else
        {
            _zoomFactor = 1.0;
        }

        ApplyZoom();
        // Center the image
        DispatcherQueue.TryEnqueue(() =>
        {
            ImageScroller.ChangeView(0, 0, null, true);
        });
    }

    void ApplyZoom()
    {
        ImageScroller.ChangeView(null, null, (float)_zoomFactor, false);
        UpdateZoomDisplay();
    }

    void UpdateZoomDisplay()
    {
        var zoom = ImageScroller.ZoomFactor;
        ZoomPercent.Text = $"{(int)(zoom * 100)}%";
    }

    DispatcherTimer? _zoomIndicatorTimer;

    void ShowZoomIndicator()
    {
        ZoomIndicator.Text = $"{(int)(_zoomFactor * 100)}%";
        ZoomIndicator.Visibility = Visibility.Visible;
        _zoomIndicatorTimer?.Stop();
        _zoomIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _zoomIndicatorTimer.Tick += (_, _) =>
        {
            ZoomIndicator.Visibility = Visibility.Collapsed;
            _zoomIndicatorTimer.Stop();
        };
        _zoomIndicatorTimer.Start();
    }

    // ── Pan (drag) ──

    void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            var pt = e.GetCurrentPoint(ImageScroller);
            if (pt.Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _panStart = pt.Position;
                _panOffsetX = ImageScroller.HorizontalOffset;
                _panOffsetY = ImageScroller.VerticalOffset;
                (sender as UIElement)?.CapturePointer(e.Pointer);
                if (Window.Current?.CoreWindow != null) Window.Current.CoreWindow.PointerCursor = null;
            }
        }
    }

    void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning) return;
        var pt = e.GetCurrentPoint(ImageScroller);
        var dx = _panStart.X - pt.Position.X;
        var dy = _panStart.Y - pt.Position.Y;
        ImageScroller.ChangeView(_panOffsetX + dx, _panOffsetY + dy, null, false);
    }

    void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            (sender as UIElement)?.ReleasePointerCapture(e.Pointer);
        }
    }

    // ── Fit toggle ──

    void OnFitToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressFitToggle) return;
        _fitToWindow = FitToggle.IsChecked == true;
        if (_fitToWindow)
            FitToWindow();
        else
        {
            _zoomFactor = 1.0;
            ApplyZoom();
        }
    }

    // ── Double tap to toggle fit ──

    void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_fitToWindow)
        {
            _fitToWindow = false;
            _suppressFitToggle = true;
            FitToggle.IsChecked = false;
            _suppressFitToggle = false;
            _zoomFactor = 1.0;
            ApplyZoom();
        }
        else
        {
            _fitToWindow = true;
            _suppressFitToggle = true;
            FitToggle.IsChecked = true;
            _suppressFitToggle = false;
            FitToWindow();
        }
        ShowZoomIndicator();
        e.Handled = true;
    }

    // ── Slideshow ──

    void OnSlideshowToggled(object sender, RoutedEventArgs e)
    {
        if (SlideshowBtn.IsChecked == true)
            _slideshowTimer.Start();
        else
        {
            _slideshowTimer.Stop();
            if (!_stoppingSlideshow) CloseSlideshow?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool _stoppingSlideshow;

    public void StopSlideshow()
    {
        if (_stoppingSlideshow) return;
        _stoppingSlideshow = true;
        try
        {
            _slideshowTimer.Stop();
            SlideshowBtn.IsChecked = false;
        }
        finally { _stoppingSlideshow = false; }
    }
}
