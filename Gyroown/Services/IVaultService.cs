using Gyroown.Models;

namespace Gyroown.Services;

/// <summary>
/// (stub) — (stub)
/// (stub)
/// (stub)
/// </summary>
public interface IVaultService
{
    // ── (stub) ──

    /// <summary>(stub)</summary>
    bool IsInitialized { get; }

    /// <summary>(stub)</summary>
    void Initialize(byte[] privateKey, byte[] publicKey);

    /// <summary>(stub)</summary>
    string VaultPath { get; }

    // ── (stub) CRUD ──

    /// <summary>(stub)</summary>
    IReadOnlyList<VaultFileItem> ListItems(string virtualPath = "/");

    /// <summary>(stub)</summary>
    VaultFolder GetFolderTree();

    /// <summary>(stub)</summary>
    Task<VaultFileItem> ImportItemAsync(Stream data, string name, string virtualPath = "/",
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>(stub)</summary>
    Task ExportItemAsync(string itemId, Stream outStream,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>(stub)</summary>
    void DeleteItem(string itemId);

    /// <summary>(stub)</summary>
    void MoveItem(string itemId, string newVirtualPath);

    /// <summary>(stub)</summary>
    void RenameItem(string itemId, string newName);

    // ── (stub) ──

    /// <summary>(stub)</summary>
    void CreateFolder(string virtualPath);

    /// <summary>(stub)</summary>
    void DeleteFolder(string virtualPath);
}
