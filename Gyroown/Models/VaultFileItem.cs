namespace Gyroown.Models;

/// <summary>Vault file item.</summary>
public class VaultFileItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string VirtualPath { get; set; } = "/";
    public long EncryptedSize { get; set; }
    public long OriginalSize { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public bool IsFolder { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string FormattedSize => OriginalSize switch
    {
        < 1024 => $"{OriginalSize} B",
        < 1024 * 1024 => $"{OriginalSize / 1024.0:F2} KB",
        < 1024L * 1024 * 1024 => $"{OriginalSize / (1024.0 * 1024):F2} MB",
        _ => $"{OriginalSize / (1024.0 * 1024 * 1024):F2} GB"
    };

    [System.Text.Json.Serialization.JsonIgnore]
    public Microsoft.UI.Xaml.Media.ImageSource? PreviewImage { get; set; }
}
