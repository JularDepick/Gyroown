using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gyroown.Models;

namespace Gyroown.Services;

/// <summary>
/// File version history service — manages version records for vault files.
/// Directory structure: {vaultRoot}/versions/{fileHashId}/
/// Each version stored as: v{versionNumber}.gyroverdata (encrypted data) + v{versionNumber}.gyrovermeta (encrypted metadata)
/// </summary>
public class VersionHistoryService
{
    private readonly EncryptionService _enc;
    private readonly string _versionsDir;
    private int _maxVersions = 10;

    /// <summary>Maximum number of versions to retain.</summary>
    public int MaxVersions
    {
        get => _maxVersions;
        set => _maxVersions = Math.Max(1, value);
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="enc">Encryption service instance.</param>
    /// <param name="vaultRoot">Vault root directory path (e.g. ~/.Gyroown).</param>
    public VersionHistoryService(EncryptionService enc, string vaultRoot)
    {
        _enc = enc;
        _versionsDir = Path.Combine(vaultRoot, "versions");
    }

    /// <summary>Ensure the versions directory exists.</summary>
    private void EnsureDir(string fileId)
    {
        Directory.CreateDirectory(_versionsDir);
        Directory.CreateDirectory(Path.Combine(_versionsDir, fileId));
    }

    /// <summary>Get the version directory path for the specified file.</summary>
    private string GetFileVersionsDir(string fileId) => Path.Combine(_versionsDir, fileId);

    /// <summary>
    /// Save the current file as a new version (called before overwrite).
    /// </summary>
    /// <param name="fileId">SHA256 hash ID of the file.</param>
    /// <param name="data">File raw data (encrypted or to be encrypted).</param>
    /// <param name="privateKey">RSA private key (for encryption).</param>
    /// <param name="originalSize">Original file size.</param>
    /// <param name="contentType">Content type.</param>
    /// <param name="description">Version description.</param>
    /// <returns>Version record.</returns>
    public FileVersionRecord SaveVersion(string fileId, byte[] data, byte[] privateKey,
        long originalSize, string contentType, string description = "")
    {
        EnsureDir(fileId);

        var versions = ListVersions(fileId, privateKey);
        var nextNumber = versions.Count > 0 ? versions.Max(v => v.VersionNumber) + 1 : 1;

        var record = new FileVersionRecord
        {
            FileId = fileId,
            VersionNumber = nextNumber,
            OriginalSize = originalSize,
            ContentType = contentType,
            Description = description
        };

        var dir = GetFileVersionsDir(fileId);
        var dataPath = Path.Combine(dir, $"v{nextNumber}.gyroverdata");
        var metaPath = Path.Combine(dir, $"v{nextNumber}.gyrovermeta");

        // Encrypt and store version data
        var encData = _enc.EncryptBlob(data, privateKey);
        File.WriteAllBytes(dataPath, encData);

        // Encrypt and store version metadata
        var metaJson = JsonSerializer.Serialize(record, JsonConfig.Options);
        var encMeta = _enc.EncryptBlob(Encoding.UTF8.GetBytes(metaJson), privateKey);
        File.WriteAllBytes(metaPath, encMeta);

        // Clean up old versions exceeding the limit
        CleanOldVersions(fileId, privateKey);

        return record;
    }

    /// <summary>
    /// Read data from file path and save as a new version.
    /// </summary>
    public FileVersionRecord SaveVersionFromFile(string fileId, string filePath, byte[] privateKey,
        long originalSize, string contentType, string description = "")
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found", filePath);

        var data = File.ReadAllBytes(filePath);
        return SaveVersion(fileId, data, privateKey, originalSize, contentType, description);
    }

    /// <summary>
    /// List all versions of the specified file (sorted by version number ascending).
    /// </summary>
    public IReadOnlyList<FileVersionRecord> ListVersions(string fileId, byte[] privateKey)
    {
        var dir = GetFileVersionsDir(fileId);
        if (!Directory.Exists(dir))
            return Array.Empty<FileVersionRecord>();

        var records = new List<FileVersionRecord>();
        foreach (var metaFile in Directory.GetFiles(dir, "*.gyrovermeta"))
        {
            try
            {
                var encMeta = File.ReadAllBytes(metaFile);
                var json = Encoding.UTF8.GetString(_enc.DecryptBlob(encMeta, privateKey));
                var record = JsonSerializer.Deserialize<FileVersionRecord>(json, JsonConfig.Options);
                if (record != null) records.Add(record);
            }
            catch (Exception ex)
            {
                LogService.Warn($"ListVersions: failed to read version meta '{metaFile}': {ex.Message}");
            }
        }

        return records.OrderBy(v => v.VersionNumber).ToList();
    }

    /// <summary>
    /// Restore data for the specified version.
    /// </summary>
    /// <param name="fileId">File ID.</param>
    /// <param name="versionNumber">Version number.</param>
    /// <param name="privateKey">RSA private key.</param>
    /// <returns>Decrypted original data.</returns>
    public byte[] RestoreVersion(string fileId, int versionNumber, byte[] privateKey)
    {
        var dir = GetFileVersionsDir(fileId);
        var dataPath = Path.Combine(dir, $"v{versionNumber}.gyroverdata");

        if (!File.Exists(dataPath))
            throw new FileNotFoundException($"Version {versionNumber} not found for file {fileId}");

        var encData = File.ReadAllBytes(dataPath);
        return _enc.DecryptBlob(encData, privateKey);
    }

    /// <summary>
    /// Get the metadata record for the specified version.
    /// </summary>
    public FileVersionRecord? GetVersionRecord(string fileId, int versionNumber, byte[] privateKey)
    {
        var dir = GetFileVersionsDir(fileId);
        var metaPath = Path.Combine(dir, $"v{versionNumber}.gyrovermeta");

        if (!File.Exists(metaPath)) return null;

        try
        {
            var encMeta = File.ReadAllBytes(metaPath);
            var json = Encoding.UTF8.GetString(_enc.DecryptBlob(encMeta, privateKey));
            return JsonSerializer.Deserialize<FileVersionRecord>(json, JsonConfig.Options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clean up old versions exceeding the MaxVersions limit.
    /// </summary>
    private void CleanOldVersions(string fileId, byte[] privateKey)
    {
        var versions = ListVersions(fileId, privateKey);
        if (versions.Count <= _maxVersions) return;

        var toRemove = versions
            .OrderBy(v => v.VersionNumber)
            .Take(versions.Count - _maxVersions)
            .ToList();

        var dir = GetFileVersionsDir(fileId);
        foreach (var v in toRemove)
        {
            DeleteVersionFile(dir, v.VersionNumber);
        }
    }

    /// <summary>
    /// Clean version history for the specified file, keeping only the most recent maxVersions versions.
    /// </summary>
    public int CleanVersions(string fileId, byte[] privateKey, int? maxVersions = null)
    {
        var limit = maxVersions ?? _maxVersions;
        var versions = ListVersions(fileId, privateKey);

        if (versions.Count <= limit) return 0;

        var toRemove = versions
            .OrderBy(v => v.VersionNumber)
            .Take(versions.Count - limit)
            .ToList();

        var dir = GetFileVersionsDir(fileId);
        foreach (var v in toRemove)
        {
            DeleteVersionFile(dir, v.VersionNumber);
        }

        return toRemove.Count;
    }

    /// <summary>
    /// Delete all version history for the specified file (called when a file is deleted).
    /// </summary>
    public void DeleteAllVersions(string fileId)
    {
        var dir = GetFileVersionsDir(fileId);
        if (!Directory.Exists(dir)) return;

        try
        {
            // Securely delete all version data
            foreach (var file in Directory.GetFiles(dir))
            {
                SecureDelete(file);
                File.Delete(file);
            }
            Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            LogService.Warn($"DeleteAllVersions: failed to clean versions for '{fileId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Get the version count for the specified file.
    /// </summary>
    public int GetVersionCount(string fileId)
    {
        var dir = GetFileVersionsDir(fileId);
        if (!Directory.Exists(dir)) return 0;
        return Directory.GetFiles(dir, "*.gyrovermeta").Length;
    }

    /// <summary>
    /// Check whether the specified file has version history.
    /// </summary>
    public bool HasVersions(string fileId)
    {
        return GetVersionCount(fileId) > 0;
    }

    /// <summary>Delete a single version file (data + metadata).</summary>
    private void DeleteVersionFile(string dir, int versionNumber)
    {
        var dataPath = Path.Combine(dir, $"v{versionNumber}.gyroverdata");
        var metaPath = Path.Combine(dir, $"v{versionNumber}.gyrovermeta");

        if (File.Exists(dataPath)) { SecureDelete(dataPath); File.Delete(dataPath); }
        if (File.Exists(metaPath)) { SecureDelete(metaPath); File.Delete(metaPath); }
    }

    /// <summary>Secure delete: overwrite with random data then delete.</summary>
    private static void SecureDelete(string path)
    {
        try
        {
            var sz = new FileInfo(path).Length;
            var r = RandomNumberGenerator.GetBytes((int)Math.Min(sz, 4096));
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);
            for (long p = 0; p < sz; p += r.Length)
                fs.Write(r, 0, (int)Math.Min(r.Length, sz - p));
            fs.Flush();
        }
        catch { /* best effort */ }
    }
}
