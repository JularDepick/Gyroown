namespace Gyroown.Models;

/// <summary>Vault file item.</summary>
public class VaultFileItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

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

    private Microsoft.UI.Xaml.Media.ImageSource? _previewImage;
    [System.Text.Json.Serialization.JsonIgnore]
    public Microsoft.UI.Xaml.Media.ImageSource? PreviewImage
    {
        get => _previewImage;
        set { _previewImage = value; OnPropertyChanged(); }
    }

    /// <summary>Return Segoe MDL2 icon glyph based on ContentType.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string IconGlyph => IsFolder ? "\uE8B7" : ContentType switch
    {
        string ct when ct.StartsWith("image/") => "\uE8B9",
        string ct when ct.StartsWith("video/") => "\uE8B2",
        string ct when ct.StartsWith("audio/") => "\uE8D6",
        "application/pdf" => "\uEA90",
        string ct when ct.StartsWith("text/") => "\uE8A5",
        _ => "\uE8A5"
    };

    private bool _isFavorited;
    /// <summary>Whether this item is marked as a favorite.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsFavorited
    {
        get => _isFavorited;
        set { _isFavorited = value; OnPropertyChanged(); OnPropertyChanged(nameof(FavoriteGlyph)); }
    }

    /// <summary>Star icon glyph for favorite status.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string FavoriteGlyph => IsFavorited ? "\uE735" : "\uE734";
}
