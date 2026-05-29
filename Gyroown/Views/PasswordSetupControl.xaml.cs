using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gyroown.Services;

namespace Gyroown.Views;

public sealed partial class PasswordSetupControl : UserControl
{
    private readonly PasswordService _pw;
    private IPasswordControl? _ctrl;
    private string _type = "pin";
    private object? _first;
    private bool _confirming, _insuranceStep;
    private string? _insuranceEmail;
    private string? _insuranceToken;
    public event EventHandler<object>? SetupCompleted;
    public string? CapturedInsuranceEmail => _insuranceEmail;
    public string? CapturedInsuranceToken => _insuranceToken;

    public PasswordSetupControl(PasswordService pw)
    {
        _pw = pw;
        InitializeComponent();
        ApplyLoc();
        var handler = (EventHandler)((_, _) => ApplyLoc());
        Loc.LanguageChanged += handler;
        Unloaded += (_, _) => Loc.LanguageChanged -= handler;
        LoadCtrl(_type);
    }

    void ApplyLoc()
    {
        TitleText.Text = Loc.Get("SetupWindow", "Title");
        if (_insuranceStep) { StepHint.Text = Loc.Get("SetupWindow", "StepInsurance"); return; }
        StepHint.Text = _confirming ? Loc.Get("SetupWindow", "StepConfirm") : Loc.Get("SetupWindow", "StepEnter");
        NextBtn.Content = Loc.Get("SetupWindow", "Next");
        BackBtn.Content = Loc.Get("SetupWindow", "Back");
        SetBtn.Content = Loc.Get("SetupWindow", "Set");
        OptPin.Content = Loc.Get("SetupWindow", "Pin");
        OptGesture.Content = Loc.Get("SetupWindow", "Gesture");
        OptCustom.Content = Loc.Get("SetupWindow", "Custom");
        OptPicture.Content = Loc.Get("SetupWindow", "Picture");
        InsuranceTitle.Text = Loc.Get("SetupWindow", "InsuranceTitle");
        InsuranceDesc.Text = Loc.Get("SetupWindow", "InsuranceDesc");
        SendCodeBtn.Content = Loc.Get("SetupWindow", "SendCode");
        VerifyCodeBtn.Content = Loc.Get("SetupWindow", "VerifyCode");
        SkipInsuranceBtn.Content = Loc.Get("SetupWindow", "Skip");
        DoneBtn.Content = Loc.Get("SetupWindow", "Done");
    }

    void OnTypeChanged(object s, SelectionChangedEventArgs e)
    { if (PwTypeSel.SelectedItem is RadioButton rb && rb.Tag is string t) { _type = t; LoadCtrl(t); } }

    void LoadCtrl(string t)
    { if (_ctrl != null) _ctrl.Validated -= OnEntered; _ctrl = t switch { "pin" => new PinPasswordControl(), "gesture" => new GesturePasswordControl(), "custom" => new CustomPasswordControl(), "picture" => new PicturePasswordControl(), _ => new CustomPasswordControl() }; _ctrl.Validated += OnEntered; CtrlHost.Content = _ctrl; }

    void OnEntered(object? s, EventArgs e) { if (!_confirming) NextBtn.IsEnabled = true; }

    void OnNext(object s, RoutedEventArgs e)
    {
        if (_ctrl == null) return; _first = _ctrl.GetCredential();
        if (!Valid(_first)) { Err(Loc.Get("SetupWindow", "ErrInvalid")); _ctrl.Clear(); return; }
        _confirming = true; StepHint.Text = Loc.Get("SetupWindow", "StepConfirm");
        NextBtn.Visibility = Visibility.Collapsed; ConfirmPanel.Visibility = Visibility.Visible;
        PwTypeSel.IsEnabled = false; ErrorText.Visibility = Visibility.Collapsed;
        _ctrl.Clear(); _ctrl.Validated -= OnEntered; _ctrl.Validated += OnConfirmed;
    }

    void OnConfirmed(object? s, EventArgs e) => SetBtn.IsEnabled = true;

    void OnBack(object s, RoutedEventArgs e)
    {
        if (_insuranceStep)
        {
            _insuranceStep = false; InsurancePanel.Visibility = Visibility.Collapsed;
            ConfirmPanel.Visibility = Visibility.Visible; PwTypeSel.IsEnabled = false;
            StepHint.Text = Loc.Get("SetupWindow", "StepConfirm"); return;
        }
        _confirming = false; _first = null; StepHint.Text = Loc.Get("SetupWindow", "StepEnter");
        NextBtn.Visibility = Visibility.Visible; ConfirmPanel.Visibility = Visibility.Collapsed;
        NextBtn.IsEnabled = false; PwTypeSel.IsEnabled = true; SetBtn.IsEnabled = false;
        _ctrl!.Validated -= OnConfirmed; _ctrl.Validated += OnEntered; _ctrl.Clear();
    }

    async void OnSet(object s, RoutedEventArgs e)
    {
        if (_ctrl == null || _first == null) return; var s2 = _ctrl.GetCredential();
        if (!Match(_first, s2)) { Err(Loc.Get("SetupWindow", "ErrMismatch")); _ctrl.Clear(); SetBtn.IsEnabled = false; return; }
        try
        {
            await _pw.SetupAsync(_first);
            // Show insurance step inline
            _insuranceStep = true;
            CtrlHost.Visibility = Visibility.Collapsed;
            PwTypeSel.Visibility = Visibility.Collapsed;
            ConfirmPanel.Visibility = Visibility.Collapsed;
            InsurancePanel.Visibility = Visibility.Visible;
            StepHint.Text = Loc.Get("SetupWindow", "StepInsurance");
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { Err($"{Loc.Get("SetupWindow", "ErrFailed")}: {ex.Message}"); }
    }

    async void OnSendCode(object s, RoutedEventArgs e)
    {
        try
        {
            var email = InsuranceEmail.Text?.Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains('@')) { InsuranceStatus.Text = Loc.Get("SetupWindow", "InvalidEmail"); return; }
            _insuranceEmail = email;
            var r = await InsuranceService.RequestCodeAsync(email);
            InsuranceStatus.Text = r.Success ? Loc.Get("SetupWindow", "CodeSent") : Loc.Get("SetupWindow", "InsuranceCodeFail");
            InsuranceCode.Visibility = Visibility.Visible;
            VerifyCodeBtn.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    async void OnVerifyCode(object s, RoutedEventArgs e)
    {
        try
        {
            if (_insuranceEmail == null) return;
            var r = await InsuranceService.VerifyCodeAsync(_insuranceEmail, InsuranceCode.Text?.Trim() ?? "");
            if (r.Success) { _insuranceToken = r.Data as string; InsuranceStatus.Text = Loc.Get("SetupWindow", "InsuranceDone"); DoneBtn.Visibility = Visibility.Visible; }
            else InsuranceStatus.Text = Loc.Get("SetupWindow", "InsuranceVerifyFail");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    void OnSkipInsurance(object s, RoutedEventArgs e) => Finish();

    void OnDone(object s, RoutedEventArgs e) => Finish();

    void Finish() { var cred = _first!; _first = null; _ctrl?.Clear(); SetupCompleted?.Invoke(this, cred); }

    void OnKeyDown(object s, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (_insuranceStep) return;
            if (!_confirming && NextBtn.Visibility == Visibility.Visible && NextBtn.IsEnabled) OnNext(this, new());
            else if (_confirming && SetBtn.IsEnabled) OnSet(this, new());
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape && _confirming && !_insuranceStep) { OnBack(this, new()); e.Handled = true; }
    }

    static bool Valid(object c) => c switch { string s => s.Length >= 6, int[] seq => seq.Length >= 4, Array arr => arr.Length > 0, _ => false };
    static bool Match(object a, object b)
    {
        if (a is string sa && b is string sb) return sa == sb;
        if (a is int[] ia && b is int[] ib) return ia.SequenceEqual(ib);
        if (a is Array pa && b is Array pb && pa.Length == pb.Length)
        { for (int i = 0; i < pa.Length; i++) { var va = pa.GetValue(i); var vb = pb.GetValue(i); if (va is ValueTuple<double, double> ta && vb is ValueTuple<double, double> tb) { if (Math.Abs(ta.Item1 - tb.Item1) >= 0.001 || Math.Abs(ta.Item2 - tb.Item2) >= 0.001) return false; } else return false; } return pa.Length > 0; }
        return false;
    }
    void Err(string m) { ErrorText.Text = m; ErrorText.Visibility = Visibility.Visible; }
}
