using System.Text;
using System.Text.Json;

namespace Gyroown.Services;

/// <summary>
/// Key insurance HTTP client stubs.
/// Flow: RequestCode(email) �� VerifyCode(email, code) �� Upload(token, email, insPriv).
/// All methods are stubs. See docs/api/key-insurance.md for full specification.
/// </summary>
public static class InsuranceService
{
    public static string InsuranceFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".Gyroown", "auth", "insurance.gyrock");

    public static bool IsEnabled => File.Exists(InsuranceFilePath);

    public static void SaveLocal(byte[] blob)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InsuranceFilePath)!);
        File.WriteAllBytes(InsuranceFilePath, blob);
    }

    public static void DeleteLocal()
    {
        if (File.Exists(InsuranceFilePath)) File.Delete(InsuranceFilePath);
    }

    /// <summary>POST /insurance/request-code �� sends verification code to email.</summary>
    public static async Task<ApiResult> RequestCodeAsync(string email, CancellationToken ct = default)
    {
        await Task.Delay(300, ct);
        return new ApiResult { Success = true, Message = "Code sent (stub)" };
    }

    /// <summary>POST /insurance/verify-code �� validates code, returns identity token.</summary>
    public static async Task<ApiResult> VerifyCodeAsync(string email, string code, CancellationToken ct = default)
    {
        await Task.Delay(300, ct);
        return new ApiResult { Success = true, Message = "Verified (stub)", Data = "token-stub-abc123" };
    }

    /// <summary>POST /insurance/upload �� background upload of encrypted insurance key.</summary>
    public static async Task UploadAsync(string email, string token, byte[] insPriv, CancellationToken ct = default)
    {
        await Task.Delay(500, ct);
        // Stub: would POST to server with email + token + base64(insPriv)
    }
}

public class ApiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
}
