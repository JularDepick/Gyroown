using Microsoft.UI.Xaml;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Gyroown.Services;

namespace Gyroown;

public partial class App : Application
{
    private MainWindow? _w;
    public static Window? ActiveWindow { get; private set; }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int n);
    private const int SW_RESTORE = 9;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            LogService.Crash("Unhandled exception", e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (!EnsureSingle()) return;

        var pw = new PasswordService();
        var enc = new EncryptionService();
        var vault = new VaultService();
        Loc.Service = new LocalizationService();

        _w = new MainWindow(pw, enc, vault);
        ActiveWindow = _w;
        _w.Activate();
    }

    bool EnsureSingle()
    {
        using var current = Process.GetCurrentProcess();
        var n = current.ProcessName;
        var ps = Process.GetProcessesByName(n);
        try
        {
            if (ps.Length > 1)
            {
                var h = ps.Where(p => p.Id != Environment.ProcessId)
                          .Select(p => { try { return p.MainWindowHandle; } catch { return IntPtr.Zero; } })
                          .FirstOrDefault(h => h != IntPtr.Zero);
                if (h != IntPtr.Zero) { if (IsIconic(h)) ShowWindow(h, SW_RESTORE); SetForegroundWindow(h); }
                Environment.Exit(0); return false;
            }
            return true;
        }
        finally
        {
            foreach (var p in ps) p.Dispose();
        }
    }
}
