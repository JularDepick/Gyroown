using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Gyroown.Services;
using Windows.Foundation;

namespace Gyroown.Views;

public sealed partial class PicturePasswordControl : UserControl, IPasswordControl
{
    private readonly List<(double X, double Y)> _pts = new();
    private const double R = 18;
    public event EventHandler? Validated;

    public PicturePasswordControl()
    {
        InitializeComponent();
        var handler = (EventHandler)((_, _) => ApplyLocalization());
        Loc.LanguageChanged += handler;
        Unloaded += (_, _) => Loc.LanguageChanged -= handler;
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        HintText.Text = Loc.Format("PictureControl", "Hint", 3);
        CountText.Text = Loc.Format("PictureControl", "Count", 0, 3);
        ResetBtn.Content = Loc.Get("Common", "Reset");
        SelectBtn.Content = Loc.Get("PictureControl", "Select");
    }

    public void LoadImage(string path)
    {
        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        bmp.UriSource = new Uri(path);
        Image.Source = bmp;
    }

    private void OnPress(object s, PointerRoutedEventArgs e)
    {
        if (Image.Source == null) return;
        var p = e.GetCurrentPoint(Container).Position;
        var w = Container.ActualWidth; var h = Container.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double rx = p.X / w, ry = p.Y / h;
        _pts.Add((rx, ry));
        Draw(rx, ry, _pts.Count);
        CountText.Text = Loc.Format("PictureControl", "Count", _pts.Count, 3);
        if (_pts.Count >= 3) Validated?.Invoke(this, EventArgs.Empty);
    }

    private void Draw(double rx, double ry, int n)
    {
        var w = Container.ActualWidth; var h = Container.ActualHeight;
        var accent = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        var e = new Ellipse { Width = R * 2, Height = R * 2, Fill = accent, Opacity = 0.85 };
        Canvas.SetLeft(e, rx * w - R); Canvas.SetTop(e, ry * h - R);
        MarkCanvas.Children.Add(e);
        var t = new TextBlock { Text = n.ToString(), Foreground = new SolidColorBrush(Colors.White), FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 13, Width = R * 2, Height = R * 2, TextAlignment = TextAlignment.Center, HorizontalTextAlignment = TextAlignment.Center };
        Canvas.SetLeft(t, rx * w - R); Canvas.SetTop(t, ry * h - R);
        MarkCanvas.Children.Add(t);
    }

    private void OnReset(object s, RoutedEventArgs e) => Clear();

    private async void OnSelect(object s, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.ActiveWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            var bytes = await File.ReadAllBytesAsync(file.Path);
            ImageProtection.SaveImage(bytes);
            LoadImage(file.Path);
        }
    }

    /// <summary>Load the stored encrypted image for the unlock screen.</summary>
    public async Task LoadStoredImageAsync()
    {
        var data = ImageProtection.LoadImage();
        if (data == null) return;
        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        var ms = new MemoryStream(data);
        await bmp.SetSourceAsync(ms.AsRandomAccessStream());
        Image.Source = bmp;
    }

    public static bool HasStoredImage => ImageProtection.HasStoredImage;

    public object GetCredential() => _pts.ToArray();
    public void Clear() { _pts.Clear(); MarkCanvas.Children.Clear(); CountText.Text = Loc.Format("PictureControl", "Count", 0, 3); }
    public void FocusInput() => Focus(FocusState.Programmatic);
}
