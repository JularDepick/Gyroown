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

    /// <summary>Display selection info (count and total size).</summary>
    public void SetSelectionInfo(int count, long totalSize)
    {
        if (count == 0)
        {
            SelectionInfoText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
            return;
        }
        var sizeStr = totalSize switch
        {
            < 1024 => $"{totalSize} B",
            < 1024 * 1024 => $"{totalSize / 1024.0:F1} KB",
            < 1024L * 1024 * 1024 => $"{totalSize / (1024.0 * 1024):F1} MB",
            _ => $"{totalSize / (1024.0 * 1024 * 1024):F1} GB"
        };
        SelectionInfoText.Text = Loc.Format("StatusBar", "Selected", count, sizeStr);
        SelectionInfoText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
    }

    public void SetVaultPath(string path) => VaultPathText.Text = path;

    public void SetLocked(bool locked) =>
        LockStatusText.Text = locked
            ? Loc.Get("StatusBar", "Locked")
            : Loc.Get("StatusBar", "Unlocked");

    public void SetAccentBrush(SolidColorBrush brush)
    {
        LockIcon.Foreground = brush;
        LockStatusText.Foreground = brush;
    }
}
