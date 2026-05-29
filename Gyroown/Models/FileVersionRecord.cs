namespace Gyroown.Models;

/// <summary>File version record.</summary>
public class FileVersionRecord
{
    /// <summary>Version unique identifier.</summary>
    public string VersionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Parent file ID (SHA256 hash ID).</summary>
    public string FileId { get; init; } = string.Empty;

    /// <summary>Version sequence number (incrementing from 1).</summary>
    public int VersionNumber { get; set; }

    /// <summary>Version save timestamp.</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>Original file size for this version (bytes).</summary>
    public long OriginalSize { get; set; }

    /// <summary>Content type for this version.</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Version description (e.g. auto-backup, user note).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Formatted display of OriginalSize.</summary>
    public string FormattedSize => OriginalSize switch
    {
        < 1024 => $"{OriginalSize} B",
        < 1024 * 1024 => $"{OriginalSize / 1024.0:F2} KB",
        < 1024L * 1024 * 1024 => $"{OriginalSize / (1024.0 * 1024):F2} MB",
        _ => $"{OriginalSize / (1024.0 * 1024 * 1024):F2} GB"
    };
}
