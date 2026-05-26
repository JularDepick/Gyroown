namespace Gyroown.Services;

/// <summary>
/// (stub)
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; init; }
    public byte[]? UserKey { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// (stub) — (stub)
/// (stub) → (stub) userKey → (stub) vaultKey。
/// (stub)
/// </summary>
public interface IPasswordService
{
    // ── (stub) ──

    /// <summary>(stub) userKey</summary>
    Task<PasswordValidationResult> ValidateAsync(object credential);

    /// <summary>(stub)</summary>
    Task SetupAsync(object credential);

    /// <summary>(stub)</summary>
    bool IsPasswordSet { get; }

    // ── (stub) ──

    /// <summary>(stub) (oldUserKey, newUserKey) (stub) vault-key.enc</summary>
    Task<(byte[] OldUserKey, byte[] NewUserKey)> ChangePasswordAsync(
        object oldCredential, object newCredential);

    // ── (stub) ──

    /// <summary>(stub) userKey）</summary>
    void Lock();

    /// <summary>(stub)</summary>
    bool IsLocked { get; }

    /// <summary>(stub)0 = (stub)</summary>
    int AutoLockTimeout { get; set; }

    // ── (stub) ──

    /// <summary>(stub)</summary>
    event EventHandler? Unlocked;

    /// <summary>(stub)</summary>
    event EventHandler? Locked;
}
