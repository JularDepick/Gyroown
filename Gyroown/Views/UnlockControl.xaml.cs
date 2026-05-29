using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Gyroown.Services;

namespace Gyroown.Views;

public sealed partial class UnlockControl : UserControl
{
    private readonly PasswordService _pw;
    private readonly ConfigService? _config;
    private IPasswordControl? _ctrl;
    private int _fails;
    private int _lockoutLevel;
    private DateTime _lockUntil;
    private bool _validating;
    private readonly EventHandler _langChangedHandler;
    public event EventHandler<PasswordValidationResult>? Unlocked;

    // Escalating lockout: 3min / 5min / 10min (cap)
    private static readonly int[] LockoutThresholds = { 5, 10, 15 };
    private static readonly int[] LockoutMinutes = { 3, 5, 10 };

    public UnlockControl(PasswordService pw, ConfigService? config = null)
    {
        _pw = pw;
        _config = config;
        InitializeComponent();
        ApplyLoc();
        _langChangedHandler = (_, _) => ApplyLoc();
        Loc.LanguageChanged += _langChangedHandler;
        Unloaded += (_, _) => Loc.LanguageChanged -= _langChangedHandler;

        // Restore persisted lockout state
        if (_config != null)
        {
            var cfg = _config.Load();
            _fails = cfg.LockoutFails;
            if (cfg.LockoutUntilTicks > 0)
                _lockUntil = new DateTime(cfg.LockoutUntilTicks);
            // Recalculate lockout level from fail count
            for (int i = LockoutThresholds.Length - 1; i >= 0; i--)
                if (_fails >= LockoutThresholds[i]) { _lockoutLevel = i; break; }
        }

        var storedType = _pw.GetPasswordType() ?? "custom";
        LoadCtrl(storedType);
    }

    void ApplyLoc() { TitleText.Text = Loc.Get("UnlockWindow", "Title"); }

    void LoadCtrl(string t)
    {
        if (_ctrl != null) _ctrl.Validated -= OnValidated;
        _ctrl = t switch { "pin" => new PinPasswordControl(), "gesture" => new GesturePasswordControl(), "custom" => new CustomPasswordControl(), "picture" => new PicturePasswordControl(), _ => new CustomPasswordControl() };
        _ctrl.Validated += OnValidated;
        CtrlHost.Content = _ctrl;
        if (_ctrl is PicturePasswordControl pic) _ = pic.LoadStoredImageAsync();
        DispatcherQueue.TryEnqueue(() => _ctrl?.FocusInput());
    }

    async void OnValidated(object? s, EventArgs e)
    {
        if (_ctrl == null || _validating) return;
        _validating = true;
        try
        {
            if (DateTime.Now < _lockUntil)
            {
                var remain = (int)Math.Ceiling((_lockUntil - DateTime.Now).TotalSeconds);
                LockText.Text = remain >= 60 ? Loc.Format("UnlockWindow", "LockedMin", remain / 60) : Loc.Format("UnlockWindow", "Locked", remain);
                LockText.Visibility = Visibility.Visible; _ctrl.Clear(); return;
            }
            // Clear previous error/lock text before new attempt
            ErrorText.Visibility = Visibility.Collapsed;
            LockText.Visibility = Visibility.Collapsed;
            var r = await _pw.ValidateAsync(_ctrl.GetCredential());
            if (r.IsValid) { _fails = 0; _lockoutLevel = 0; PersistLockout(); Unlocked?.Invoke(this, r); return; }
            _fails++; ErrorText.Text = Loc.Format("UnlockWindow", "Wrong", _fails); ErrorText.Visibility = Visibility.Visible;
            for (int i = LockoutThresholds.Length - 1; i >= 0; i--)
            {
                if (_fails >= LockoutThresholds[i])
                {
                    _lockoutLevel = i;
                    break;
                }
            }
            if (_fails >= LockoutThresholds[0])
            {
                var min = LockoutMinutes[Math.Min(_lockoutLevel, LockoutMinutes.Length - 1)];
                _lockUntil = DateTime.Now.AddMinutes(min);
                LockText.Text = Loc.Format("UnlockWindow", "LockedMin", min);
                LockText.Visibility = Visibility.Visible;
            }
            PersistLockout();
            _ctrl.Clear();
        }
        catch (Exception ex) { ErrorText.Text = ex.Message; ErrorText.Visibility = Visibility.Visible; _ctrl.Clear(); }
        finally { _validating = false; }
    }

    void OnKeyDown(object s, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter && _ctrl != null) { OnValidated(this, EventArgs.Empty); e.Handled = true; } }

    private bool _charHandling;

    void OnCharReceived(UIElement s, CharacterReceivedRoutedEventArgs e)
    {
        if (_charHandling) return;
        var c = e.Character;
        if (!char.IsControl(c) && _ctrl != null)
        {
            _charHandling = true;
            try { _ctrl.FocusInput(); }
            finally { _charHandling = false; }
        }
    }

    void PersistLockout()
    {
        if (_config == null) return;
        var cfg = _config.Load();
        cfg.LockoutFails = _fails;
        cfg.LockoutUntilTicks = _lockUntil > DateTime.MinValue ? _lockUntil.Ticks : 0;
        _config.Save(cfg);
    }
}
