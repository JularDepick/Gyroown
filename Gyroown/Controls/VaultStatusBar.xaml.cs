using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Gyroown.Services;

namespace Gyroown.Controls;

public sealed partial class VaultStatusBar : UserControl
{
    public VaultStatusBar() => InitializeComponent();

    public void SetItemCount(int count) =>
        ItemCountText.Text = count == 1
            ? Loc.Get("StatusBar", "OneItem")
            : Loc.Format("StatusBar", "Items", count);

    public void SetVaultPath(string path) => VaultPathText.Text = path;

    public void SetLocked(bool locked) =>
        LockStatusText.Text = locked
            ? Loc.Get("StatusBar", "Locked")
            : Loc.Get("StatusBar", "Encrypted");

    public void SetAccentBrush(SolidColorBrush brush)
    {
        LockIcon.Foreground = brush;
        LockStatusText.Foreground = brush;
    }
}
