using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gyroown.Services;

namespace Gyroown.Views;

public sealed partial class PinPasswordControl : UserControl, IPasswordControl
{
    private readonly PasswordBox[] _digits;
    private bool _submitted;
    public event EventHandler? Validated;

    public PinPasswordControl()
    {
        InitializeComponent();
        _digits = new[] { D0, D1, D2, D3, D4, D5 };
        foreach (var d in _digits) d.KeyDown += OnKeyDown;
        Loaded += (_, _) => D0.Focus(FocusState.Programmatic);
        var handler = (EventHandler)((_, _) => ApplyLocalization());
        Loc.LanguageChanged += handler;
        Unloaded += (_, _) => Loc.LanguageChanged -= handler;
        ApplyLocalization();
    }

    private void ApplyLocalization() => HintText.Text = Loc.Get("PinControl", "Hint");

    private void OnDigit(object s, RoutedEventArgs e)
    {
        if (s is not PasswordBox b || string.IsNullOrEmpty(b.Password)) return;
        if (!char.IsDigit(b.Password[^1])) { b.Password = ""; ErrorText.Text = Loc.Get("PinControl", "DigitOnly"); ErrorText.Visibility = Visibility.Visible; return; }
        ErrorText.Visibility = Visibility.Collapsed;
        var i = Array.IndexOf(_digits, b);
        if (i < 5) { _digits[i + 1].Focus(FocusState.Programmatic); _submitted = false; }
        else if (!_submitted) { _submitted = true; Validated?.Invoke(this, EventArgs.Empty); }
    }

    private void OnKeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Back) return;
        if (s is not PasswordBox b) return;
        var i = Array.IndexOf(_digits, b);
        if (string.IsNullOrEmpty(b.Password) && i > 0)
        {
            _digits[i - 1].Password = "";
            _digits[i - 1].Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    public object GetCredential() => string.Concat(_digits.Select(d => d.Password));
    public void Clear() { foreach (var d in _digits) d.Password = ""; _submitted = false; D0.Focus(FocusState.Programmatic); }
    public void FocusInput() => D0.Focus(FocusState.Programmatic);
}
