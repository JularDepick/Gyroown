namespace Gyroown.Models;

/// <summary>
/// Password policy configuration.
/// </summary>
public class PasswordConfig
{
    /// <summary>Current password type.</summary>
    public PasswordType Type { get; set; } = PasswordType.Custom;

    /// <summary>Minimum PIN digit count.</summary>
    public int PinMinLength { get; set; } = 6;

    /// <summary>Minimum gesture connection points.</summary>
    public int GestureMinPoints { get; set; } = 4;

    /// <summary>Minimum custom password length.</summary>
    public int CustomMinLength { get; set; } = 6;

    /// <summary>Maximum custom password length.</summary>
    public int CustomMaxLength { get; set; } = 32;

    /// <summary>Minimum picture password tap points.</summary>
    public int PictureMinPoints { get; set; } = 3;

    /// <summary>Consecutive failure lockout threshold.</summary>
    public int LockoutThreshold { get; set; } = 5;

    /// <summary>Lockout duration in seconds.</summary>
    public int LockoutDurationSec { get; set; } = 30;

    /// <summary>Picture password tap tolerance (percentage of image short edge, 0.0-1.0).</summary>
    public double PictureToleranceRatio { get; set; } = 0.05;

    /// <summary>Allowed password characters (custom password).</summary>
    public string AllowedChars { get; set; } =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:'\",.<>?/|~`";
}
