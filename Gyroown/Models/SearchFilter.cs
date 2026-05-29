namespace Gyroown.Models;

/// <summary>File type category.</summary>
public enum FileCategory
{
    All,
    Image,
    Video,
    Audio,
    Document,
    Other
}

/// <summary>File size range.</summary>
public enum SizeRange
{
    Any,
    Lt1MB,      // < 1 MB
    Lt10MB,     // < 10 MB
    Lt100MB,    // < 100 MB
    Gt1MB,      // > 1 MB
    Gt10MB,     // > 10 MB
    Gt100MB     // > 100 MB
}

/// <summary>Modified date range.</summary>
public enum DateRange
{
    Any,
    Today,
    ThisWeek,
    ThisMonth,
    ThisYear
}

/// <summary>Advanced search filter criteria.</summary>
public class SearchFilter
{
    /// <summary>Filename text search.</summary>
    public string TextQuery { get; set; } = "";

    /// <summary>File type category filter.</summary>
    public FileCategory Category { get; set; } = FileCategory.All;

    /// <summary>File size range filter.</summary>
    public SizeRange Size { get; set; } = SizeRange.Any;

    /// <summary>Modified date range filter.</summary>
    public DateRange Date { get; set; } = DateRange.Any;

    /// <summary>Whether any advanced filters are active.</summary>
    public bool HasAdvancedFilters =>
        Category != FileCategory.All ||
        Size != SizeRange.Any ||
        Date != DateRange.Any;

    /// <summary>Whether the filter is completely empty.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(TextQuery) && !HasAdvancedFilters;

    private const long KB = 1024;
    private const long MB = 1024 * KB;
    private const long GB = 1024 * MB;

    /// <summary>Check if a file item matches the current filter criteria.</summary>
    /// <param name="skipTextQuery">When true, skip TextQuery matching (used when text is already handled by inline filter).</param>
    public bool Matches(VaultFileItem item, bool skipTextQuery = false)
    {
        // Text search
        if (!skipTextQuery && !string.IsNullOrWhiteSpace(TextQuery) &&
            !item.Name.Contains(TextQuery, StringComparison.OrdinalIgnoreCase))
            return false;

        // File type category
        if (Category != FileCategory.All)
        {
            var cat = GetCategory(item.ContentType, item.IsFolder);
            if (cat != Category) return false;
        }

        // File size
        if (Size != SizeRange.Any)
        {
            var size = item.OriginalSize;
            bool match = Size switch
            {
                SizeRange.Lt1MB => size < MB,
                SizeRange.Lt10MB => size < 10 * MB,
                SizeRange.Lt100MB => size < 100 * MB,
                SizeRange.Gt1MB => size > MB,
                SizeRange.Gt10MB => size > 10 * MB,
                SizeRange.Gt100MB => size > 100 * MB,
                _ => true
            };
            if (!match) return false;
        }

        // Modified date
        if (Date != DateRange.Any)
        {
            var now = DateTime.Now;
            var mod = item.ModifiedAt;
            bool match = Date switch
            {
                DateRange.Today => mod.Date == now.Date,
                DateRange.ThisWeek => mod >= now.Date.AddDays(-(int)now.DayOfWeek),
                DateRange.ThisMonth => mod.Year == now.Year && mod.Month == now.Month,
                DateRange.ThisYear => mod.Year == now.Year,
                _ => true
            };
            if (!match) return false;
        }

        return true;
    }

    /// <summary>Determine file category from ContentType.</summary>
    public static FileCategory GetCategory(string contentType, bool isFolder)
    {
        if (isFolder) return FileCategory.All; // Folders are excluded from type filtering
        return contentType switch
        {
            string ct when ct.StartsWith("image/") => FileCategory.Image,
            string ct when ct.StartsWith("video/") => FileCategory.Video,
            string ct when ct.StartsWith("audio/") => FileCategory.Audio,
            string ct when ct.StartsWith("text/") || ct == "application/pdf"
                || ct.Contains("document") || ct.Contains("spreadsheet")
                || ct.Contains("presentation") || ct.Contains("word")
                || ct.Contains("excel") || ct.Contains("powerpoint")
                => FileCategory.Document,
            _ => FileCategory.Other
        };
    }
}
