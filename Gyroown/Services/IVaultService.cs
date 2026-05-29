using Gyroown.Models;

namespace Gyroown.Services;

/// <summary>
/// Vault service interface — manages encrypted file storage, retrieval, import/export, and folder operations.
/// Each file is stored as data/{hashID}.gyrodt + meta/{hashID}.gyromt, both encrypted with the vault key pair.
/// File listing is obtained by scanning the meta/ directory; no separate index file is needed.
/// </summary>
public interface IVaultService
{
    // ── Initialization ──

    /// <summary>Whether the vault is initialized (private key loaded).</summary>
    bool IsInitialized { get; }

    /// <summary>Initialize the vault with the provided RSA key pair, creating data/, meta/, preview/ directories.</summary>
    void Initialize(byte[] privateKey, byte[] publicKey);

    /// <summary>Vault data file storage directory path.</summary>
    string VaultPath { get; }

    // ── File CRUD ──

    /// <summary>List all file and folder items under the specified virtual path.</summary>
    IReadOnlyList<VaultFileItem> ListItems(string virtualPath = "/");

    /// <summary>Get the folder tree structure (loaded from .tree.gyrojson or creates default structure).</summary>
    VaultFolder GetFolderTree();

    /// <summary>Import file: SHA256 content-hash deduplication, chunked AES-GCM encrypted storage, with image preview generation.</summary>
    Task<VaultFileItem> ImportItemAsync(Stream data, string name, string virtualPath = "/",
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Export file: decrypt by chunks or as a whole, stream-write to output stream.</summary>
    Task ExportItemAsync(string itemId, Stream outStream,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Securely delete file: overwrite disk data then remove data file, metadata, and preview.</summary>
    void DeleteItem(string itemId);

    /// <summary>Move file to a new virtual path (update metadata).</summary>
    void MoveItem(string itemId, string newVirtualPath);

    /// <summary>Rename file (update name in metadata).</summary>
    void RenameItem(string itemId, string newName);

    // ── Folder Operations ──

    /// <summary>Create folder: generate folder metadata and update the folder tree.</summary>
    void CreateFolder(string virtualPath);

    /// <summary>Delete folder and all files within it (recursive deletion).</summary>
    void DeleteFolder(string virtualPath);

    // ── Version Management ──

    /// <summary>Get the version history list for the specified file.</summary>
    IReadOnlyList<FileVersionRecord> GetVersionHistory(string fileId);

    /// <summary>Restore file to the specified version.</summary>
    Task RestoreFileVersionAsync(string fileId, int versionNumber, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Save the current file version (called before overwrite).</summary>
    FileVersionRecord? SaveCurrentVersion(string fileId, string description = "");
}
