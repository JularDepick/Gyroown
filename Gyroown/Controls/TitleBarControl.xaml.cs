using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gyroown.Services;

namespace Gyroown.Controls;

public sealed partial class TitleBarControl : UserControl
{
    public event Action<string>? SearchChanged;

    public TitleBarControl()
    {
        InitializeComponent();
        Loc.LanguageChanged += (_, _) => ApplyLoc();
        ApplyLoc();
        SearchBoxInput.TextChanged += (_, _) => SearchChanged?.Invoke(SearchBoxInput.Text);
    }

    void ApplyLoc() => SearchBoxInput.PlaceholderText = Loc.Get("MainWindow", "Search");
    public UIElement GetDragElement() => DragRegion;
}
