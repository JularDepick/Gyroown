using System.Runtime.InteropServices;

namespace Gyroown.Controls;

public static class CursorHelper
{
    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    private static readonly IntPtr SizeWE = LoadCursor(IntPtr.Zero, 32644);
    private static readonly IntPtr Arrow = LoadCursor(IntPtr.Zero, 32512);

    public static void ShowResize()
    {
        SetCursor(SizeWE);
    }

    public static void ShowArrow()
    {
        SetCursor(Arrow);
    }

    public static void ApplyResizeCursor(Microsoft.UI.Xaml.UIElement element) { }
    public static void ApplyArrowCursor(Microsoft.UI.Xaml.UIElement element) { }
}
