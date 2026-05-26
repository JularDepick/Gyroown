using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gyroown.Models;
using Gyroown.Services;

namespace Gyroown.Views;

public sealed partial class CustomPasswordControl : UserControl, IPasswordControl
{
    private readonly PasswordConfig _cfg = new();
    public event EventHandler? Validated;

    public CustomPasswordControl()
    {
        InitializeComponent();
        Loc.LanguageChanged += (_, _) => ApplyLocalization();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        HintText.Text = Loc.Get("CustomControl", "Hint");
        Input.PlaceholderText = Loc.Get("CustomControl", "Placeholder");
        RangeText.Text = Loc.Get("CustomControl", "Range");
        ShowCb.Content = Loc.Get("CustomControl", "Show");
    }

    private void OnChanged(object s, RoutedEventArgs e)
    {
        var pw = Input.Password; ErrorText.Visibility = Visibility.Collapsed;
        if (pw.Length < _cfg.CustomMinLength) { RangeText.Text = $"{Loc.Get("CustomControl", "Range")} ({pw.Length})"; return; }
        foreach (var c in pw) if (!_cfg.AllowedChars.Contains(c)) { RangeText.Text = Loc.Format("CustomControl", "BadChar", c); return; }
        RangeText.Text = Loc.Format("CustomControl", "Valid", pw.Length);
        if (pw.Length >= _cfg.CustomMinLength && pw.Length <= _cfg.CustomMaxLength)
            Validated?.Invoke(this, EventArgs.Empty);
    }

    private void OnKey(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        { var pw = Input.Password; if (pw.Length >= _cfg.CustomMinLength && pw.Length <= _cfg.CustomMaxLength) Validated?.Invoke(this, EventArgs.Empty); }
    }

    private void OnShow(object s, RoutedEventArgs e) => Input.PasswordRevealMode = ShowCb.IsChecked == true ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;

    public object GetCredential() => Input.Password;
    public void Clear() { Input.Password = ""; RangeText.Text = Loc.Get("CustomControl", "Range"); ErrorText.Visibility = Visibility.Collapsed; }
}
