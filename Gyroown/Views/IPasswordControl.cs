namespace Gyroown.Views;

/// <summary>
/// Unified password input control interface — abstracts input interaction for different password types.
/// Used by UnlockWindow / PasswordSetupWindow, implemented by PinPasswordControl,
/// GesturePasswordControl, CustomPasswordControl, PicturePasswordControl.
/// </summary>
public interface IPasswordControl
{
    /// <summary>Get the user-entered credential object (string/int[]/coordinate array, depending on password type).</summary>
    object GetCredential();

    /// <summary>Clear the input state in the control.</summary>
    void Clear();

    /// <summary>Focus the primary input element.</summary>
    void FocusInput();

    /// <summary>Fired when the user completes input (clicks confirm).</summary>
    event EventHandler? Validated;
}
