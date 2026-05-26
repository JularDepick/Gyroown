namespace Gyroown.Models;

/// <summary>
/// 密码策略配置
/// </summary>
public class PasswordConfig
{
    /// <summary>当前密码类型</summary>
    public PasswordType Type { get; set; } = PasswordType.Custom;

    /// <summary>最小 PIN 位数</summary>
    public int PinMinLength { get; set; } = 6;

    /// <summary>手势最少连接点数</summary>
    public int GestureMinPoints { get; set; } = 4;

    /// <summary>自定义密码最小长度</summary>
    public int CustomMinLength { get; set; } = 6;

    /// <summary>自定义密码最大长度</summary>
    public int CustomMaxLength { get; set; } = 32;

    /// <summary>图片密码最少点数</summary>
    public int PictureMinPoints { get; set; } = 3;

    /// <summary>连续错误锁定阈值</summary>
    public int LockoutThreshold { get; set; } = 5;

    /// <summary>锁定时间（秒）</summary>
    public int LockoutDurationSec { get; set; } = 30;

    /// <summary>图片密码点击容差（图片短边的百分比，0.0-1.0）</summary>
    public double PictureToleranceRatio { get; set; } = 0.05;

    /// <summary>允许的密码字符 (自定义密码)</summary>
    public string AllowedChars { get; set; } =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:'\",.<>?/|~`";
}
