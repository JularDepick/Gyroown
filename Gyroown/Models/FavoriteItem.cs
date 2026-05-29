namespace Gyroown.Models;

/// <summary>A bookmarked vault item (file or folder).</summary>
public class FavoriteItem
{
    /// <summary>Unique favorite record ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Vault item ID (matches VaultFileItem.Id).</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Display name (cached from the vault item).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Virtual path inside the vault (e.g. "/Photos/2024").</summary>
    public string ItemPath { get; set; } = "/";

    /// <summary>Whether the vault item is a folder.</summary>
    public bool IsFolder { get; set; }

    /// <summary>Content type (cached, empty for folders).</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Group name for organizing favorites.</summary>
    public string Group { get; set; } = "Default";

    /// <summary>Sort order within the group.</summary>
    public int Order { get; set; }

    /// <summary>Icon glyph based on type.</summary>
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
}
