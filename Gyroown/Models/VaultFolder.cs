namespace Gyroown.Models;

/// <summary>Virtual folder in vault tree.</summary>
public class VaultFolder
{
    public string Name { get; set; } = string.Empty;
    public string VirtualPath { get; set; } = "/";
    public List<VaultFolder> SubFolders { get; set; } = new();
    public List<VaultFileItem> Files { get; set; } = new();
}
