using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Gyroown.Services;

public class PasswordService : IPasswordService
{
    private const int SaltSize = 32, HashSize = 32, Iterations = 100_000, UserKeySize = 32;
    private readonly string _authDir, _passwordFile;
    private readonly object _lock = new();
    private byte[]? _userKey;

    public bool IsPasswordSet => File.Exists(_passwordFile);
    public bool IsLocked { get { lock (_lock) return _userKey == null; } }
    public int AutoLockTimeout { get; set; }
    public event EventHandler? Unlocked, Locked;

    public PasswordService()
    {
        _authDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "auth");
        _passwordFile = Path.Combine(_authDir, ".gyropw");
    }

    public byte[]? GetStoredSalt()
    {
        try
        {
            if (!IsPasswordSet) return null;
            var data = JsonSerializer.Deserialize<PasswordFileData>(File.ReadAllText(_passwordFile), JsonConfig.Options);
            if (data?.Salt == null) return null;
            return Convert.FromBase64String(data.Salt);
        }
        catch (Exception ex) { LogService.Warn($"PasswordService.GetStoredSalt: {ex.Message}"); return null; }
    }

    public Task SetupAsync(object credential)
    {
        Directory.CreateDirectory(_authDir);
        VaultService.ProtectAuthDir();
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var credBytes = SerializeCredential(credential);
        var userKey = Rfc2898DeriveBytes.Pbkdf2(credBytes, salt, Iterations, HashAlgorithmName.SHA256, UserKeySize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(userKey, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        var type = GetCredentialType(credential);
        var data = new PasswordFileData { Type = type, Salt = Convert.ToBase64String(salt), Hash = Convert.ToBase64String(hash), Iterations = Iterations };
        if (type == "picture" && credential is (double, double)[] points)
            data.PicturePoints = string.Join(";", points.Select(p => $"{p.Item1},{p.Item2}"));
        File.WriteAllText(_passwordFile, JsonSerializer.Serialize(data, JsonConfig.Options));
        lock (_lock) _userKey = userKey;
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

        // Picture password: use squared Euclidean distance tolerance comparison
        if (data.Type == "picture" && credential is (double, double)[] inputPoints && !string.IsNullOrEmpty(data.PicturePoints))
        {
            var storedPoints = ParsePicturePoints(data.PicturePoints);
            if (storedPoints.Length != inputPoints.Length)
                return Task.FromResult(new PasswordValidationResult { IsValid = false, ErrorMessage = "Incorrect password." });

            var tolerance = new Models.PasswordConfig().PictureToleranceRatio; // 0.05
            var toleranceSquared = tolerance * tolerance;
            bool allMatch = true;
            for (int i = 0; i < storedPoints.Length; i++)
            {
                var dx = storedPoints[i].Item1 - inputPoints[i].Item1;
                var dy = storedPoints[i].Item2 - inputPoints[i].Item2;
                var distanceSquared = dx * dx + dy * dy;
                if (distanceSquared > toleranceSquared) allMatch = false;
            }

            if (!allMatch)
                return Task.FromResult(new PasswordValidationResult { IsValid = false, ErrorMessage = "Incorrect password." });

            // Match: derive key from stored coordinates for consistency
            var storedCredBytes = Encoding.UTF8.GetBytes(data.PicturePoints);
            var userKey = Rfc2898DeriveBytes.Pbkdf2(storedCredBytes, salt, data.Iterations, HashAlgorithmName.SHA256, UserKeySize);
            lock (_lock) _userKey = userKey;
            Unlocked?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(new PasswordValidationResult { IsValid = true, UserKey = userKey });
        }

        // Standard hash comparison for non-picture passwords
        var stdCredBytes = SerializeCredential(credential);
        var stdUserKey = Rfc2898DeriveBytes.Pbkdf2(stdCredBytes, salt, data.Iterations, HashAlgorithmName.SHA256, UserKeySize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(stdUserKey, salt, data.Iterations, HashAlgorithmName.SHA256, HashSize);
        if (!CryptographicOperations.FixedTimeEquals(hash, storedHash))
            return Task.FromResult(new PasswordValidationResult { IsValid = false, ErrorMessage = "Incorrect password." });
        lock (_lock) _userKey = stdUserKey;
        Unlocked?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(new PasswordValidationResult { IsValid = true, UserKey = stdUserKey });
    }

    static (double, double)[] ParsePicturePoints(string raw)
    {
        var pts = new List<(double, double)>();
        foreach (var part in raw.Split(';'))
        {
            var nums = part.Split(',');
            if (nums.Length >= 2 && double.TryParse(nums[0].Trim(), out var x) && double.TryParse(nums[1].Trim(), out var y))
                pts.Add((x, y));
        }
        return pts.ToArray();
    }

    public async Task<(byte[] OldUserKey, byte[] NewUserKey)> ChangePasswordAsync(object oldCred, object newCred)
    {
        var r = await ValidateAsync(oldCred);
        if (!r.IsValid) throw new InvalidOperationException("Old password is incorrect.");
        var oldUk = r.UserKey!;
        await SetupAsync(newCred);
        return (oldUk, _userKey!);
    }

    public void Lock()
    {
        lock (_lock) { if (_userKey != null) { Array.Clear(_userKey); _userKey = null; } }
        Locked?.Invoke(this, EventArgs.Empty);
    }

    public string? GetPasswordType()
    {
        if (!IsPasswordSet) return null;
        var data = JsonSerializer.Deserialize<PasswordFileData>(File.ReadAllText(_passwordFile), JsonConfig.Options);
        return data?.Type;
    }

    static byte[] SerializeCredential(object c) => c switch
    {
        string s => Encoding.UTF8.GetBytes(s),
        int[] seq => Encoding.UTF8.GetBytes(string.Join(",", seq)),
        Array arr when arr.Length > 0 && arr.GetValue(0) is ValueTuple<double, double> => 
            Encoding.UTF8.GetBytes(string.Join(";", arr.Cast<(double X, double Y)>().Select(t => $"{t.X},{t.Y}"))),
        Array arr => Encoding.UTF8.GetBytes(string.Join(";", arr.Cast<object>().Select(o => o?.ToString() ?? ""))),
        _ => throw new ArgumentException($"Unknown credential type: {c.GetType()}")
    };

    static string GetCredentialType(object c) => c switch { string s when s.Length <= 6 && s.All(char.IsDigit) => "pin", int[] => "gesture", string => "custom", Array => "picture", _ => "unknown" };
}

public class PasswordFileData { public string Type { get; set; } = "custom"; public string Salt { get; set; } = ""; public string Hash { get; set; } = ""; public int Iterations { get; set; } = 100_000; public string? StoredCredential { get; set; } public string? PicturePoints { get; set; } }
