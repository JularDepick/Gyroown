using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gyroown.Services;

namespace Gyroown.Views;

public sealed partial class UnlockControl : UserControl
{
    private readonly PasswordService _pw;
    private IPasswordControl? _ctrl;
    private int _fails;
    private DateTime _lockUntil;
    public event EventHandler<PasswordValidationResult>? Unlocked;

    public UnlockControl(PasswordService pw)
    {
        _pw = pw;
        InitializeComponent();
        ApplyLoc();
        Loc.LanguageChanged += (_, _) => ApplyLoc();
        LoadCtrl("custom");
    }

    void ApplyLoc() { TitleText.Text = Loc.Get("UnlockWindow", "Title"); }

    void LoadCtrl(string t)
    { if (_ctrl != null) _ctrl.Validated -= OnValidated; _ctrl = t switch { "pin" => new PinPasswordControl(), "gesture" => new GesturePasswordControl(), "custom" => new CustomPasswordControl(), "picture" => new PicturePasswordControl(), _ => new CustomPasswordControl() }; _ctrl.Validated += OnValidated; CtrlHost.Content = _ctrl; }

    async void OnValidated(object? s, EventArgs e)
    {
        if (_ctrl == null) return;
        if (DateTime.Now < _lockUntil) { LockText.Text = Loc.Format("UnlockWindow", "Locked", (int)(_lockUntil - DateTime.Now).TotalSeconds); LockText.Visibility = Visibility.Visible; _ctrl.Clear(); return; }
        try
        {
            var r = await _pw.ValidateAsync(_ctrl.GetCredential());
            if (r.IsValid) { _fails = 0; Unlocked?.Invoke(this, r); return; }
            _fails++; ErrorText.Text = Loc.Format("UnlockWindow", "Wrong", _fails); ErrorText.Visibility = Visibility.Visible;
            if (_fails >= 5) { _lockUntil = DateTime.Now.AddSeconds(30); LockText.Text = Loc.Format("UnlockWindow", "Locked", 30); LockText.Visibility = Visibility.Visible; }
            _ctrl.Clear();
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; ErrorText.Visibility = Visibility.Visible; _ctrl.Clear(); }
    }

    void OnKeyDown(object s, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter && _ctrl != null) { OnValidated(this, EventArgs.Empty); e.Handled = true; } }
}
