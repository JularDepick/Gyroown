using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gyroown.Services;

namespace Gyroown.Views;

/// <summary>
/// Multi-type password change control.
/// Phase 1: Verify old password (auto-detects stored password type).
/// Phase 2: Enter new password (with type selection).
/// Phase 3: Confirm new password.
/// Fires ChangeCompleted with (oldCredential, newCredential) on success.
/// </summary>
public sealed partial class PasswordChangeControl : UserControl
{
    private readonly PasswordService _pw;
    private IPasswordControl? _ctrl;
    private object? _oldCred;
    private object? _newCredFirst;
    private string _newPwType = "pin";
    private bool _confirming;

    /// <summary>Fires when the full change-password flow completes successfully.
    /// EventArgs: (object oldCredential, object newCredential)</summary>
    public event EventHandler<(object Old, object New)>? ChangeCompleted;

    public PasswordChangeControl(PasswordService pw)
    {
        _pw = pw;
        InitializeComponent();
        ApplyLoc();
        var handler = (EventHandler)((_, _) => ApplyLoc());
        Loc.LanguageChanged += handler;
        Unloaded += (_, _) => Loc.LanguageChanged -= handler;

        // Phase 1: load control matching stored password type
        var storedType = _pw.GetPasswordType() ?? "custom";
        LoadVerifyCtrl(storedType);
    }

    void ApplyLoc()
    {
        TitleText.Text = Loc.Get("SettingsWindow", "ChangePassword");
        if (!_confirming && _oldCred == null)
        {
            // Phase 1: verify old password
            StepHint.Text = Loc.Get("SettingsWindow", "StepVerify");
            VerifyBtn.Content = Loc.Get("Common", "OK");
        }
        else if (!_confirming)
        {
            // Phase 2: enter new password
            StepHint.Text = Loc.Get("SetupWindow", "StepEnter");
            NextBtn.Content = Loc.Get("SetupWindow", "Next");
        }
        else
        {
            // Phase 3: confirm new password
            StepHint.Text = Loc.Get("SetupWindow", "StepConfirm");
            BackBtn.Content = Loc.Get("SetupWindow", "Back");
            SetBtn.Content = Loc.Get("SetupWindow", "Set");
        }
        OptPin.Content = Loc.Get("SetupWindow", "Pin");
        OptGesture.Content = Loc.Get("SetupWindow", "Gesture");
        OptCustom.Content = Loc.Get("SetupWindow", "Custom");
        OptPicture.Content = Loc.Get("SetupWindow", "Picture");
    }

    // ── Phase 1: Verify old password ──

    void LoadVerifyCtrl(string type)
    {
        if (_ctrl != null) _ctrl.Validated -= OnOldValidated;
        _ctrl = type switch
        {
            "pin" => new PinPasswordControl(),
            "gesture" => new GesturePasswordControl(),
            "custom" => new CustomPasswordControl(),
            "picture" => new PicturePasswordControl(),
            _ => new CustomPasswordControl()
        };
        _ctrl.Validated += OnOldValidated;
        CtrlHost.Content = _ctrl;
        if (_ctrl is PicturePasswordControl pic) _ = pic.LoadStoredImageAsync();
    }

    void OnOldValidated(object? s, EventArgs e) => VerifyBtn.IsEnabled = true;

    async void OnVerify(object s, RoutedEventArgs e)
    {
        if (_ctrl == null) return;
        var cred = _ctrl.GetCredential();
        try
        {
            var r = await _pw.ValidateAsync(cred);
            if (!r.IsValid)
            {
                Err(Loc.Get("SettingsWindow", "ErrOldPwWrong"));
                _ctrl.Clear();
                VerifyBtn.IsEnabled = false;
                return;
            }
            _oldCred = cred;
            TransitionToNewPasswordPhase();
        }
        catch (Exception ex)
        {
            Err(ex.Message);
            _ctrl.Clear();
            VerifyBtn.IsEnabled = false;
        }
    }

    // ── Phase 2: Enter new password ──

    void TransitionToNewPasswordPhase()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        VerifyBtn.Visibility = Visibility.Collapsed;
        PwTypeSel.Visibility = Visibility.Visible;
        NextBtn.Visibility = Visibility.Visible;

        _newPwType = "pin";
        foreach (var child in PwTypeSel.Items)
        {
            if (child is RadioButton rb && rb.Tag?.ToString() == "pin")
            {
                rb.IsChecked = true;
                break;
            }
        }

        LoadNewPwCtrl(_newPwType);
        ApplyLoc();
    }

    void OnTypeChanged(object s, SelectionChangedEventArgs e)
    {
        if (PwTypeSel.SelectedItem is RadioButton rb && rb.Tag is string t)
        {
            _newPwType = t;
            LoadNewPwCtrl(t);
        }
    }

    void LoadNewPwCtrl(string type)
    {
        if (_ctrl != null) _ctrl.Validated -= OnOldValidated;
        _ctrl = type switch
        {
            "pin" => new PinPasswordControl(),
            "gesture" => new GesturePasswordControl(),
            "custom" => new CustomPasswordControl(),
            "picture" => new PicturePasswordControl(),
            _ => new CustomPasswordControl()
        };
        _ctrl.Validated += OnNewValidated;
        CtrlHost.Content = _ctrl;
        if (_ctrl is PicturePasswordControl pic) _ = pic.LoadStoredImageAsync();
        NextBtn.IsEnabled = false;
    }

    void OnNewValidated(object? s, EventArgs e) => NextBtn.IsEnabled = true;

    void OnNext(object s, RoutedEventArgs e)
    {
        if (_ctrl == null) return;
        var cred = _ctrl.GetCredential();
        if (!Valid(cred))
        {
            Err(Loc.Get("SetupWindow", "ErrInvalid"));
            _ctrl.Clear();
            NextBtn.IsEnabled = false;
            return;
        }
        _newCredFirst = cred;
        _confirming = true;

        // Transition to phase 3
        NextBtn.Visibility = Visibility.Collapsed;
        ConfirmPanel.Visibility = Visibility.Visible;
        PwTypeSel.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;

        _ctrl.Clear();
        _ctrl.Validated -= OnNewValidated;
        _ctrl.Validated += OnConfirmed;
        ApplyLoc();
    }

    // ── Phase 3: Confirm new password ──

    void OnConfirmed(object? s, EventArgs e) => SetBtn.IsEnabled = true;

    void OnBack(object s, RoutedEventArgs e)
    {
        // Return to phase 2
        _confirming = false;
        _newCredFirst = null;
        ConfirmPanel.Visibility = Visibility.Collapsed;
        NextBtn.Visibility = Visibility.Visible;
        NextBtn.IsEnabled = false;
        PwTypeSel.IsEnabled = true;
        SetBtn.IsEnabled = false;

        _ctrl!.Validated -= OnConfirmed;
        _ctrl.Validated += OnNewValidated;
        _ctrl.Clear();
        ApplyLoc();
    }

    void OnSet(object s, RoutedEventArgs e)
    {
        if (_ctrl == null || _newCredFirst == null || _oldCred == null) return;
        var second = _ctrl.GetCredential();
        if (!Match(_newCredFirst, second))
        {
            Err(Loc.Get("SetupWindow", "ErrMismatch"));
            _ctrl.Clear();
            SetBtn.IsEnabled = false;
            return;
        }
        // Clean up stored image if changing away from picture type
        if (_newPwType != "picture") ImageProtection.DeleteImage();
        ChangeCompleted?.Invoke(this, (_oldCred, _newCredFirst));
    }

    // ── Keyboard ──

    void OnKeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (_oldCred == null && VerifyBtn.Visibility == Visibility.Visible)
                OnVerify(this, new RoutedEventArgs());
            else if (!_confirming && NextBtn.Visibility == Visibility.Visible && NextBtn.IsEnabled)
                OnNext(this, new RoutedEventArgs());
            else if (_confirming && SetBtn.IsEnabled)
                OnSet(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape && _confirming)
        {
            OnBack(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    // ── Validation helpers ──

    static bool Valid(object c) => c switch
    {
        string s => s.Length >= 6,
        int[] seq => seq.Length >= 4,
        Array arr => arr.Length > 0,
        _ => false
    };

    static bool Match(object a, object b)
    {
        if (a is string sa && b is string sb) return sa == sb;
        if (a is int[] ia && b is int[] ib) return ia.SequenceEqual(ib);
        if (a is Array pa && b is Array pb && pa.Length == pb.Length)
        {
            for (int i = 0; i < pa.Length; i++)
            {
                var va = pa.GetValue(i);
                var vb = pb.GetValue(i);
                if (va is ValueTuple<double, double> ta && vb is ValueTuple<double, double> tb)
                {
                    if (Math.Abs(ta.Item1 - tb.Item1) >= 0.001 || Math.Abs(ta.Item2 - tb.Item2) >= 0.001)
                        return false;
                }
                else return false;
            }
            return pa.Length > 0;
        }
        return false;
    }

    void Err(string m)
    {
        ErrorText.Text = m;
        ErrorText.Visibility = Visibility.Visible;
    }
}
