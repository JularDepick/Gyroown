using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Gyroown.Services;

public class PasswordService : IPasswordService
{
    private const int SaltSize = 32, HashSize = 32, Iterations = 100_000, UserKeySize = 32;
    private readonly string _authDir, _passwordFile;
    private byte[]? _userKey;

    public bool IsPasswordSet => File.Exists(_passwordFile);
    public bool IsLocked => _userKey == null;
    public int AutoLockTimeout { get; set; }
    public event EventHandler? Unlocked, Locked;

    public PasswordService()
    {
        _authDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "auth");
        _passwordFile = Path.Combine(_authDir, ".gyropw");
    }

    public byte[]? GetStoredSalt()
    {
        if (!IsPasswordSet) return null;
        var data = JsonSerializer.Deserialize<PasswordFileData>(File.ReadAllText(_passwordFile), JsonConfig.Options);
        return data != null ? Convert.FromBase64String(data.Salt) : null;
    }

    public Task SetupAsync(object credential)
    {
        Directory.CreateDirectory(_authDir);
        VaultService.ProtectAuthDir();
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var credBytes = SerializeCredential(credential);
        _userKey = Rfc2898DeriveBytes.Pbkdf2(credBytes, salt, Iterations, HashAlgorithmName.SHA256, UserKeySize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(_userKey, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        var data = new PasswordFileData { Type = GetCredentialType(credential), Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(hash), Iterations = Iterations };
        File.WriteAllText(_passwordFile, JsonSerializer.Serialize(data, JsonConfig.Options));
        Unlocked?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task<PasswordValidationResult> ValidateAsync(object credential)
    {
        if (!IsPasswordSet) return Task.FromResult(new PasswordValidationResult { IsValid = false, ErrorMessage = "Password not set." });
        var data = JsonSerializer.Deserialize<PasswordFileData>(File.ReadAllText(_passwordFile), JsonConfig.Options);
        if (data == null) return Task.FromResult(new PasswordValidationResult { IsValid = false, ErrorMessage = "Password file corrupted." });
        var salt = Convert.FromBase64String(data.Salt);
        var storedHash = Convert.FromBase64String(data.Hash);
        var credBytes = SerializeCredential(credential);
        var userKey = Rfc2898DeriveBytes.Pbkdf2(credBytes, salt, data.Iterations, HashAlgorithmName.SHA256, UserKeySize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(userKey, salt, data.Iterations, HashAlgorithmName.SHA256, HashSize);
        if (!CryptographicOperations.FixedTimeEquals(hash, storedHash))
            return Task.FromResult(new PasswordValidationResult { IsValid = false, ErrorMessage = "Incorrect password." });
        _userKey = userKey;
        Unlocked?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(new PasswordValidationResult { IsValid = true, UserKey = userKey });
    }

    public async Task<(byte[] OldUserKey, byte[] NewUserKey)> ChangePasswordAsync(object oldCred, object newCred)
    {
        var r = await ValidateAsync(oldCred);
        if (!r.IsValid) throw new InvalidOperationException("Old password is incorrect.");
        var oldUk = r.UserKey!;
        await SetupAsync(newCred);
        return (oldUk, _userKey!);
    }

    public void Lock() { _userKey = null; Locked?.Invoke(this, EventArgs.Empty); }

    static byte[] SerializeCredential(object c) => c switch
    {
        string s => Encoding.UTF8.GetBytes(s),
        int[] seq => Encoding.UTF8.GetBytes(string.Join(",", seq)),
        Array arr => Encoding.UTF8.GetBytes(string.Join(";", arr.Cast<object>().Select(o => o?.ToString() ?? ""))),
        _ => throw new ArgumentException($"Unknown credential type: {c.GetType()}")
    };

    static string GetCredentialType(object c) => c switch { string s when s.Length <= 6 && s.All(char.IsDigit) => "pin", int[] => "gesture", string => "custom", Array => "picture", _ => "unknown" };
}

public class PasswordFileData { public string Type { get; set; } = "custom"; public string Salt { get; set; } = ""; public string Hash { get; set; } = ""; public int Iterations { get; set; } = 100_000; }
