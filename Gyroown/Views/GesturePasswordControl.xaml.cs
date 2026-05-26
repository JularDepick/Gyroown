using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Gyroown.Services;
using Windows.Foundation;

namespace Gyroown.Views;

public sealed partial class GesturePasswordControl : UserControl, IPasswordControl
{
    private Ellipse[] _dots = null!;
    private readonly List<int> _seq = new();
    private bool _drawing;
    public event EventHandler? Validated;

    public GesturePasswordControl()
    {
        InitializeComponent();
        _dots = new[] { E0, E1, E2, E3, E4, E5, E6, E7, E8 };
        Loc.LanguageChanged += (_, _) => ApplyLocalization();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        HintText.Text = Loc.Get("GestureControl", "Hint");
        CountText.Text = Loc.Format("GestureControl", "HintMin");
        ResetBtn.Content = Loc.Get("Common", "Reset");
    }

    private void OnPress(object s, PointerRoutedEventArgs e) { _drawing = true; _seq.Clear(); LineCanvas.Children.Clear(); ResetDots(); Hit(e.GetCurrentPoint(Grid).Position); }
    private void OnMove(object s, PointerRoutedEventArgs e) { if (_drawing) Hit(e.GetCurrentPoint(Grid).Position); }
    private void OnRelease(object s, PointerRoutedEventArgs e)
    {
        _drawing = false;
        if (_seq.Count >= 4) Validated?.Invoke(this, EventArgs.Empty);
        else CountText.Text = Loc.Format("GestureControl", "HintSelected", _seq.Count);
    }
    private void OnReset(object s, RoutedEventArgs e) => Clear();

    private void Hit(Point p)
    {
        for (int i = 0; i < _dots.Length; i++)
        {
            GetCenter(i, out double cx, out double cy);
            if (Math.Sqrt(Math.Pow(p.X - cx, 2) + Math.Pow(p.Y - cy, 2)) <= 36 && !_seq.Contains(i))
            {
                _seq.Add(i); _dots[i].Fill = new SolidColorBrush(Colors.SteelBlue);
                if (_seq.Count >= 2) { GetCenter(_seq[^2], out double px, out double py); LineCanvas.Children.Add(new Line { X1 = px, Y1 = py, X2 = cx, Y2 = cy, Stroke = new SolidColorBrush(Colors.SteelBlue), StrokeThickness = 3 }); }
                break;
            }
        }
    }

    private void GetCenter(int i, out double cx, out double cy)
    {
        var d = _dots[i];
        double l = d.Margin.Left, t = d.Margin.Top;
        if (d.HorizontalAlignment == HorizontalAlignment.Center) l = (Grid.ActualWidth - d.Width) / 2 + d.Margin.Left - d.Margin.Right;
        else if (d.HorizontalAlignment == HorizontalAlignment.Right) l = Grid.ActualWidth - d.Width - d.Margin.Right;
        if (d.VerticalAlignment == VerticalAlignment.Center) t = (Grid.ActualHeight - d.Height) / 2 + d.Margin.Top - d.Margin.Bottom;
        else if (d.VerticalAlignment == VerticalAlignment.Bottom) t = Grid.ActualHeight - d.Height - d.Margin.Bottom;
        cx = l + d.Width / 2; cy = t + d.Height / 2;
    }

    private void ResetDots() { foreach (var d in _dots) d.Fill = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]; }
    public object GetCredential() => _seq.ToArray();
    public void Clear() { _seq.Clear(); LineCanvas.Children.Clear(); ResetDots(); CountText.Text = Loc.Format("GestureControl", "HintMin"); }
}
