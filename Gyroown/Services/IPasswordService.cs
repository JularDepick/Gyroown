namespace Gyroown.Services;

/// <summary>
/// Password validation result, containing validity, derived userKey, and error message.
/// </summary>
public class PasswordValidationResult
{
    public bool IsValid { get; init; }
    public byte[]? UserKey { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Password service interface — manages password setup, validation, change, and lock/unlock.
/// Validates password and derives userKey for vaultKey encryption/decryption.
/// Supports four types: PIN, gesture, custom text, and picture password.
/// </summary>
public interface IPasswordService
{
    // ── Password Validation ──

    /// <summary>Validate user credentials; returns derived userKey on success.</summary>
    Task<PasswordValidationResult> ValidateAsync(object credential);

    /// <summary>Set password for the first time: generate salt, store hash, and fire Unlocked event.</summary>
    Task SetupAsync(object credential);

    /// <summary>Whether a password has been set (checks if .gyropw file exists).</summary>
    bool IsPasswordSet { get; }

    // ── Password Change ──

    /// <summary>Change password: validate old credential then reset; returns (oldUserKey, newUserKey) for re-encrypting vault-key.enc.</summary>
    Task<(byte[] OldUserKey, byte[] NewUserKey)> ChangePasswordAsync(
        object oldCredential, object newCredential);

    // ── Lock / Unlock ──

    /// <summary>Lock the vault, clearing userKey from memory.</summary>
    void Lock();

    /// <summary>Whether the vault is locked (userKey is null).</summary>
    bool IsLocked { get; }

    /// <summary>Auto-lock timeout in seconds; 0 means no auto-lock.</summary>
    int AutoLockTimeout { get; set; }

    // ── Events ──

    /// <summary>Fired when vault is unlocked.</summary>
    event EventHandler? Unlocked;

    /// <summary>Fired when vault is locked.</summary>
    event EventHandler? Locked;

    // ── Password Type ──

    /// <summary>Returns the stored password type — "pin" / "gesture" / "custom" / "picture"; returns null if no password is set.</summary>
    string? GetPasswordType();
}
