using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gyroown.Models;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Storage.FileProperties;

namespace Gyroown.Services;

/// <summary>
/// Each original file �?data/{hashID}.gyrodt + meta/{hashID}.gyromt.
/// Both files encrypted with the vault key pair via EncryptionService.EncryptBlob/DecryptBlob.
/// File listing by scanning meta/ directory �?no separate index file.
/// </summary>
public class VaultService : IVaultService
{
    private readonly string _dataDir, _metaDir, _prevDir, _treeFile, _vaultRoot;
    private readonly EncryptionService _enc = new();
    private readonly ConfigService _config = new();
    private VersionHistoryService? _versionHistory;
    // P1 OOM mitigation: warn when file exceeds this threshold.
    // Future versions will replace full-buffer reads with true streaming.
    private const long LargeFileWarningThreshold = 100L * 1024 * 1024; // 100 MB
    private byte[]? _priv;
    private string _currentPath = "/";

    public bool IsInitialized => _priv != null;
    public string VaultPath => _dataDir;

    public VaultService()
    {
        _vaultRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown");
        _dataDir = Path.Combine(_vaultRoot, "data");
        _metaDir = Path.Combine(_vaultRoot, "meta");
        _prevDir = Path.Combine(_vaultRoot, "preview");
        _treeFile = Path.Combine(_metaDir, ".tree.gyrojson");
    }

    public void Initialize(byte[] priv, byte[] pub)
    { _priv = priv; _config.Initialize(priv); _versionHistory = new VersionHistoryService(_enc, _vaultRoot); Directory.CreateDirectory(_dataDir); Directory.CreateDirectory(_metaDir); Directory.CreateDirectory(_prevDir); }

    // ┢�┢� Integrity ┢�┢�

    /// <summary>Check that auth directory and required files exist.</summary>
    public static bool IsVaultIntact()
    {
        var auth = AuthDir;
        return Directory.Exists(auth) &&
               File.Exists(Path.Combine(auth, ".gyropw")) &&
               File.Exists(Path.Combine(auth, ".gyrock"));
    }

    public static string AuthDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown", "auth");

    /// <summary>Mark auth directory as hidden to reduce accidental deletion.</summary>
    public static void ProtectAuthDir()
    {
        try
        {
            var attr = File.GetAttributes(AuthDir);
            File.SetAttributes(AuthDir, attr | FileAttributes.Hidden);
            foreach (var f in Directory.GetFiles(AuthDir))
                File.SetAttributes(f, File.GetAttributes(f) | FileAttributes.Hidden);
        }
        catch { /* best effort */ }
    }

    /// <summary>
    /// meta/ and data/ are strongly bound. Returns true if both exist (or both absent).
    /// If only one exists, the vault is in an inconsistent state.
    /// </summary>
    public static bool AreDataAndMetaBound()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown");
        var hasData = Directory.Exists(Path.Combine(root, "data"));
        var hasMeta = Directory.Exists(Path.Combine(root, "meta"));
        return hasData == hasMeta; // both or neither
    }

    /// <summary>
    /// Scan meta/ and data/ directories for orphaned and undecryptable files.
    /// Returns (orphanedMeta, orphanedData, pairedCount, undecryptable).
    /// </summary>
    public (List<string> OrphanedMeta, List<string> OrphanedData, int Paired, List<string> Undecryptable) CheckIntegrity()
    {
        var metaIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dataIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orphanedMeta = new List<string>();
        var orphanedData = new List<string>();
        var undecryptable = new List<string>();
        int paired = 0;

        if (Directory.Exists(_metaDir))
            foreach (var f in Directory.GetFiles(_metaDir, "*.gyromt"))
                metaIds.Add(Path.GetFileNameWithoutExtension(f));

        if (Directory.Exists(_dataDir))
        {
            // Single-file data: data/{id}.gyrodt
            foreach (var f in Directory.GetFiles(_dataDir, "*.gyrodt"))
                dataIds.Add(Path.GetFileNameWithoutExtension(f));
            // Chunked data: data/{id}/c*.gyrodt
            foreach (var d in Directory.GetDirectories(_dataDir))
            {
                var dirName = Path.GetFileName(d);
                if (Directory.GetFiles(d, "c*.gyrodt").Length > 0)
                    dataIds.Add(dirName);
            }
        }

        LogService.Info($"CheckIntegrity: metaIds=[{string.Join(",", metaIds.Take(5))}] dataIds=[{string.Join(",", dataIds.Take(5))}]");

        foreach (var id in metaIds)
        {
            if (dataIds.Contains(id)) { paired++; dataIds.Remove(id); }
            else orphanedMeta.Add(id);
        }
        orphanedData.AddRange(dataIds);

        // Verify paired files are decryptable
        if (_priv != null)
        {
            foreach (var id in metaIds.Except(orphanedMeta).ToList())
            {
                try
                {
                    if (!CanDecrypt(id))
                    {
                        undecryptable.Add(id);
                        paired--;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Warn($"CheckIntegrity: CanDecrypt failed for '{id}': {ex.Message}");
                    undecryptable.Add(id);
                    paired--;
                }
            }
        }

        return (orphanedMeta, orphanedData, paired, undecryptable);
    }

    /// <summary>Check if a paired file's meta and data can be decrypted.</summary>
    private bool CanDecrypt(string id)
    {
        try
        {
            var mp = Path.Combine(_metaDir, id + ".gyromt");
            if (!File.Exists(mp)) return false;

            var metaBlob = File.ReadAllBytes(mp);
            if (metaBlob.Length == 0) return false;
            _enc.DecryptBlob(metaBlob, _priv!);

            // Determine storage format: single file or chunked directory
            var singlePath = Path.Combine(_dataDir, id + ".gyrodt");
            var chunkDir = Path.Combine(_dataDir, id);
            var chunkPath = Path.Combine(chunkDir, "c0000.gyrodt");

            if (File.Exists(singlePath))
            {
                var dataBlob = File.ReadAllBytes(singlePath);
                if (dataBlob.Length == 0) return false;
                _enc.DecryptBlob(dataBlob, _priv!);
            }
            else if (File.Exists(chunkPath))
            {
                var chunkBlob = File.ReadAllBytes(chunkPath);
                if (chunkBlob.Length == 0) return false;
                _enc.DecryptBlob(chunkBlob, _priv!);
            }
            else
            {
                return false;
            }

            return true;
        }
        catch { return false; }
    }

    /// <summary>Clean orphaned files.</summary>
    public void CleanOrphans(List<string> orphanedMeta, List<string> orphanedData)
    {
        foreach (var id in orphanedMeta)
        {
            var mp = Path.Combine(_metaDir, id + ".gyromt");
            if (File.Exists(mp)) File.Delete(mp);
        }
        foreach (var id in orphanedData)
        {
            var dp = Path.Combine(_dataDir, id + ".gyrodt");
            if (File.Exists(dp)) { SecureDelete(dp); File.Delete(dp); }
            var chunkDir = Path.Combine(_dataDir, id);
            if (Directory.Exists(chunkDir)) Directory.Delete(chunkDir, true);
        }
    }

    /// <summary>Clean undecryptable files (both meta and data).</summary>
    public void CleanUndecryptable(List<string> undecryptable)
    {
        foreach (var id in undecryptable)
        {
            var mp = Path.Combine(_metaDir, id + ".gyromt");
            if (File.Exists(mp)) File.Delete(mp);
            var dp = Path.Combine(_dataDir, id + ".gyrodt");
            if (File.Exists(dp)) { SecureDelete(dp); File.Delete(dp); }
            // Also clean chunk subdirectory if exists
            var chunkDir = Path.Combine(_dataDir, id);
            if (Directory.Exists(chunkDir)) Directory.Delete(chunkDir, true);
        }
    }

    public IReadOnlyList<VaultFileItem> ListItems(string virtualPath = "/")
    {
        var items = new List<VaultFileItem>();
        if (!Directory.Exists(_metaDir)) return items;
        foreach (var f in Directory.GetFiles(_metaDir, "*.gyromt"))
        {
            try
            {
                var id = Path.GetFileNameWithoutExtension(f);
                if (_priv == null) continue;
                var blob = File.ReadAllBytes(f);
                if (blob.Length == 0) continue;
                var json = _enc.DecryptBlob(blob, _priv);
                var m = JsonSerializer.Deserialize<MetaFile>(Encoding.UTF8.GetString(json), JsonConfig.Options);
                if (m == null) continue;

                if (m.IsFolder)
                {
                    var folderParent = Path.GetDirectoryName(m.VirtualPath.Replace('\\', '/'))?.Replace('\\', '/') ?? "/";
                    if (string.IsNullOrEmpty(folderParent)) folderParent = "/";
                    if (folderParent != virtualPath && m.VirtualPath != virtualPath) continue;
                }
                else
                {
                    if (m.VirtualPath != virtualPath) continue;
                }

                items.Add(new VaultFileItem
                {
                    Id = id, Name = m.Name, VirtualPath = m.VirtualPath,
                    OriginalSize = m.OriginalSize,
                    EncryptedSize = CalcEncryptedSize(id),
                    ContentType = m.ContentType, CreatedAt = m.Created, ModifiedAt = m.Modified, IsFolder = m.IsFolder
                });
            }
            catch (CryptographicException ex) { LogService.Error($"ListItems: crypto error on {f}: {ex.Message}"); }
            catch (Exception ex) { LogService.Error($"ListItems: error on {f}: {ex.Message}"); }
        }
        return items;
    }

    private long CalcEncryptedSize(string id)
    {
        var single = Path.Combine(_dataDir, id + ".gyrodt");
        if (File.Exists(single)) return new FileInfo(single).Length;
        var chunkDir = Path.Combine(_dataDir, id);
        if (Directory.Exists(chunkDir))
        {
            long total = 0;
            foreach (var f in Directory.GetFiles(chunkDir, "c*.gyrodt"))
                total += new FileInfo(f).Length;
            return total;
        }
        return 0;
    }

    public VaultFolder GetFolderTree()
    {
        if (File.Exists(_treeFile)) return LoadTree();
        var root = new VaultFolder { Name = "Gyroown", VirtualPath = "/" };
        SaveTree(root);
        return root;
    }

    public async Task<VaultFileItem> ImportItemAsync(Stream data, string name, string virtualPath = "/",
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnsureInit();

        // Stream source to temp file with constant memory (~1MB buffer)
        var tmpPath = Path.GetTempFileName();
        try
        {
            long rawLength = 0;
            using var sha = SHA256.Create();
            var buf = new byte[1024 * 1024]; // 1MB read buffer
            await using (var tmpFs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, buf.Length, FileOptions.SequentialScan))
            {
                int bytesRead;
                while ((bytesRead = await data.ReadAsync(buf, ct)) > 0)
                {
                    sha.TransformBlock(buf, 0, bytesRead, null, 0);
                    await tmpFs.WriteAsync(buf.AsMemory(0, bytesRead), ct);
                    rawLength += bytesRead;
                }
                sha.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
                await tmpFs.FlushAsync(ct);
            }

            // P1 OOM mitigation: warn on large files so operators can plan capacity.
            // Future version: full streaming encryption to eliminate in-memory buffering.
            if (rawLength > LargeFileWarningThreshold)
                LogService.Warn($"ImportItemAsync: file '{name}' is {rawLength / (1024 * 1024)} MB, exceeds {LargeFileWarningThreshold / (1024 * 1024)} MB threshold. " +
                    "Large file may cause high memory usage. Streaming optimization planned for future release.");

            // Content-hash ID: first 32 chars of SHA256 — deduplication
            var id = Convert.ToHexString(sha.Hash!)[..32].ToLowerInvariant();

            var contentType = InferType(name);

            // Before overwrite: check if a file with the same name already exists
            var existingItem = ListItems(_currentPath).FirstOrDefault(i => i.Name == name && !i.IsFolder);
            if (existingItem != null && existingItem.Id != id)
            {
                SaveCurrentVersion(existingItem.Id, $"{Loc.Get("VersionHistory", "AutoBackup")}: {name}");

                // Clean up old data and meta files to prevent orphans
                var oldDp = Path.Combine(_dataDir, existingItem.Id + ".gyrodt");
                if (File.Exists(oldDp)) { SecureDelete(oldDp); File.Delete(oldDp); }
                var oldChunkDir = Path.Combine(_dataDir, existingItem.Id);
                if (Directory.Exists(oldChunkDir))
                {
                    foreach (var cf in Directory.GetFiles(oldChunkDir, "c*.gyrodt"))
                    { SecureDelete(cf); File.Delete(cf); }
                    try { Directory.Delete(oldChunkDir, true); } catch { }
                }
                var oldMp = Path.Combine(_metaDir, existingItem.Id + ".gyromt");
                if (File.Exists(oldMp)) File.Delete(oldMp);
            }

            var meta = new MetaFile { Name = name, VirtualPath = _currentPath, OriginalSize = rawLength, ContentType = contentType };

            // Generate preview for image/video types (cap at 50MB to avoid OOM)
            var cfg = _config?.Load() ?? new CoreConfig();
            if (cfg.GeneratePreviews && (IsImageType(contentType) || IsVideoType(contentType)) && rawLength <= 50L * 1024 * 1024)
            {
                meta.PreviewId = await GeneratePreview(tmpPath, contentType, ct);
            }

            // Chunked storage based on config — encrypt directly from temp file
            var chunkSize = ConfigService.ChunkSizeForTier(cfg.ChunkTier);
            long encSize = await StoreChunkedStream(tmpPath, rawLength, id, chunkSize, meta);

            var encMeta = _enc.EncryptBlob(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(meta, JsonConfig.Options)), _priv!);
            File.WriteAllBytes(Path.Combine(_metaDir, id + ".gyromt"), encMeta);

            progress?.Report(1.0);
            return new VaultFileItem { Id = id, Name = name, VirtualPath = _currentPath, OriginalSize = rawLength, EncryptedSize = encSize, ContentType = meta.ContentType };
        }
        finally
        {
            try { SecureDelete(tmpPath); File.Delete(tmpPath); } catch { /* best effort */ }
        }
    }

    public async Task ExportItemAsync(string itemId, Stream outStream,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnsureInit();
        var meta = LoadMeta(itemId);

        // P1 OOM mitigation: warn on large files so operators can plan capacity.
        // Future version: true streaming decryption to eliminate in-memory buffering.
        if (meta.OriginalSize > LargeFileWarningThreshold)
            LogService.Warn($"ExportItemAsync: item '{itemId}' is {meta.OriginalSize / (1024 * 1024)} MB, exceeds {LargeFileWarningThreshold / (1024 * 1024)} MB threshold. " +
                "Large file may cause high memory usage during export. Streaming optimization planned for future release.");

        if (meta.ChunkCount > 0)
        {
            // Chunked: read each chunk from disk via FileStream, decrypt, write to output.
            // Each chunk is independently encrypted (separate AES-GCM blob), so we
            // only need one chunk in memory at a time. Release encrypted buffer
            // immediately after decryption to keep peak memory ≈ 1× chunk size.
            for (int i = 0; i < meta.ChunkCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var cp = Path.Combine(_dataDir, itemId, "c" + i.ToString("x4") + ".gyrodt");
                if (!File.Exists(cp)) throw new FileNotFoundException("Chunk " + i + " not found");
                byte[] encChunk;
                await using (var fs = new FileStream(cp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                {
                    encChunk = new byte[fs.Length];
                    await fs.ReadExactlyAsync(encChunk, ct);
                }
                // Decrypt, then immediately release the encrypted buffer
                var plainChunk = _enc.DecryptBlob(encChunk, _priv!);
                encChunk = null!;
                await outStream.WriteAsync(plainChunk, ct);
                progress?.Report((double)(i + 1) / meta.ChunkCount);
            }
        }
        else
        {
            // Non-chunked: AES-GCM requires full ciphertext for tag verification.
            // Use FileStream with SequentialScan hint for efficient OS-level paging.
            var dp = Path.Combine(_dataDir, itemId + ".gyrodt");
            if (!File.Exists(dp)) throw new FileNotFoundException("Item not found");
            byte[] encData;
            await using (var fs = new FileStream(dp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                encData = new byte[fs.Length];
                await fs.ReadExactlyAsync(encData, ct);
            }
            var plain = _enc.DecryptBlob(encData, _priv!);
            // Release encrypted buffer before writing plaintext to halve peak memory
            encData = null!;
            await outStream.WriteAsync(plain, ct);
            progress?.Report(1.0);
        }
        await outStream.FlushAsync(ct);
    }

    public void DeleteItem(string id)
    {
        EnsureInit();
        // Clean up version history
        _versionHistory?.DeleteAllVersions(id);

        var dp = Path.Combine(_dataDir, id + ".gyrodt"); var mp = Path.Combine(_metaDir, id + ".gyromt");
        if (File.Exists(dp)) { SecureDelete(dp); File.Delete(dp); }
        // Delete chunk subdirectory if any
        var chunkDir = Path.Combine(_dataDir, id);
        if (Directory.Exists(chunkDir))
        {
            foreach (var f in Directory.GetFiles(chunkDir, "c*.gyrodt"))
            { SecureDelete(f); File.Delete(f); }
            Directory.Delete(chunkDir);
        }
        if (File.Exists(mp))
        {
            // Also delete preview if exists
            try
            {
                var m = LoadMeta(id);
                if (m.PreviewId != null) { var pp = Path.Combine(_prevDir, m.PreviewId + ".gyropv"); if (File.Exists(pp)) { SecureDelete(pp); File.Delete(pp); } }
            }
            catch { }
            File.Delete(mp);
        }
    }

    public void MoveItem(string id, string np)
    {
        EnsureInit();
        if (np.Contains("..")) throw new ArgumentException("Path traversal is not allowed.", nameof(np));
        var m = LoadMeta(id); m.VirtualPath = np; SaveMeta(id, m);
    }
    public void RenameItem(string id, string nn)
    {
        EnsureInit();
        if (nn.Contains("..")) throw new ArgumentException("Path traversal is not allowed.", nameof(nn));
        var m = LoadMeta(id); m.Name = nn; SaveMeta(id, m);
    }

    public void CreateFolder(string name)
    {
        EnsureInit();
        var virtualPath = _currentPath == "/" ? "/" + name : _currentPath + "/" + name;
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(virtualPath)))[..32].ToLowerInvariant();
        var m = new MetaFile { Name = name, VirtualPath = virtualPath, IsFolder = true, ContentType = "folder" };
        File.WriteAllBytes(Path.Combine(_metaDir, id + ".gyromt"), _enc.EncryptBlob(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m, JsonConfig.Options)), _priv!));

        // Update tree
        var tree = LoadTree();
        AddFolderToTree(tree, virtualPath);
        SaveTree(tree);
    }

    void AddFolderToTree(VaultFolder parent, string path)
    {
        var parts = path.TrimStart('/').Split('/');
        var cur = parent;
        for (int i = 0; i < parts.Length; i++)
        {
            var existing = cur.SubFolders.FirstOrDefault(f => f.Name == parts[i]);
            if (existing == null)
            {
                existing = new VaultFolder { Name = parts[i], VirtualPath = string.Join("/", parts.Take(i + 1)).Insert(0, "/") };
                cur.SubFolders.Add(existing);
            }
            cur = existing;
        }
    }

    public void DeleteFolder(string virtualPath)
    {
        EnsureInit();
        // Delete all items in this folder and subfolders
        foreach (var f in Directory.GetFiles(_metaDir, "*.gyromt"))
        {
            try
            {
                var m = LoadMeta(Path.GetFileNameWithoutExtension(f));
                if (m.VirtualPath == virtualPath || m.VirtualPath.StartsWith(virtualPath + "/"))
                    DeleteItem(Path.GetFileNameWithoutExtension(f));
            }
            catch (Exception ex) { LogService.Warn($"DeleteFolder: skipping corrupted meta '{f}': {ex.Message}"); }
        }

        // Remove from tree
        var tree = LoadTree();
        RemoveFolderFromTree(tree, virtualPath);
        SaveTree(tree);
    }

    void RemoveFolderFromTree(VaultFolder parent, string path)
    {
        var parts = path.TrimStart('/').Split('/');
        var cur = parent;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            cur = cur.SubFolders.FirstOrDefault(f => f.Name == parts[i]);
            if (cur == null) return;
        }
        cur.SubFolders.RemoveAll(f => f.Name == parts[^1]);
    }

    MetaFile LoadMeta(string id)
    {
        var blob = File.ReadAllBytes(Path.Combine(_metaDir, id + ".gyromt"));
        var json = Encoding.UTF8.GetString(_enc.DecryptBlob(blob, _priv!));
        return JsonSerializer.Deserialize<MetaFile>(json, JsonConfig.Options)
            ?? throw new InvalidOperationException($"Corrupted metadata for item '{id}'");
    }

    void SaveMeta(string id, MetaFile m)
    {
        m.Modified = DateTime.Now;
        File.WriteAllBytes(Path.Combine(_metaDir, id + ".gyromt"), _enc.EncryptBlob(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m, JsonConfig.Options)), _priv!));
    }

    static void SecureDelete(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var sz = new FileInfo(path).Length;
            if (sz == 0) return;
            var r = RandomNumberGenerator.GetBytes((int)Math.Min(sz, 4096));
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);
            for (long p = 0; p < sz; p += r.Length) fs.Write(r, 0, (int)Math.Min(r.Length, sz - p));
            fs.Flush();
        }
        catch (Exception ex) { LogService.Warn($"SecureDelete: failed for '{path}': {ex.Message}"); }
    }

    static string InferType(string n) => Path.GetExtension(n).ToLowerInvariant() switch
    {
        ".txt" => "text/plain", ".pdf" => "application/pdf",
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif", ".bmp" => "image/bmp", ".webp" => "image/webp",
        ".mp4" => "video/mp4", ".avi" => "video/avi", ".mov" => "video/quicktime",
        ".mkv" => "video/x-matroska", ".wmv" => "video/x-ms-wmv",
        _ => "application/octet-stream"
    };

    static bool IsImageType(string ct) => ct.StartsWith("image/");
    static bool IsVideoType(string ct) => ct.StartsWith("video/");

    async Task<string?> GeneratePreview(string filePath, string contentType, CancellationToken ct)
    {
        try
        {
            if (IsImageType(contentType))
                return await GenerateImagePreview(filePath, ct);
            if (IsVideoType(contentType))
                return await GenerateVideoPreview(filePath, ct);
        }
        catch { /* preview generation failure is non-fatal */ }
        return null;
    }

    async Task<string> GenerateImagePreview(string filePath, CancellationToken ct)
    {
        // Use FileStream directly instead of loading entire file into memory
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(fs.AsRandomAccessStream());
        var frame = await decoder.GetFrameAsync(0);

        // Resize to max 256px on longest side
        uint w = frame.PixelWidth, h = frame.PixelHeight;
        const uint maxDim = 256;
        if (w > maxDim || h > maxDim)
        {
            double ratio = Math.Min((double)maxDim / w, (double)maxDim / h);
            w = (uint)(w * ratio); h = (uint)(h * ratio);
        }

        var transform = new Windows.Graphics.Imaging.BitmapTransform { ScaledWidth = w, ScaledHeight = h };
        var pixelData = await frame.GetPixelDataAsync(
            Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
            transform,
            Windows.Graphics.Imaging.ExifOrientationMode.IgnoreExifOrientation,
            Windows.Graphics.Imaging.ColorManagementMode.DoNotColorManage);

        var pixels = pixelData.DetachPixelData();
        using var outStream = new MemoryStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.JpegEncoderId, outStream.AsRandomAccessStream());
        encoder.SetPixelData(
            Windows.Graphics.Imaging.BitmapPixelFormat.Rgba8,
            Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
            w, h, 96, 96, pixels);

        // Ensure �?500KB by adjusting quality
        double quality = 0.75;
        while (true)
        {
            var prop = new Windows.Graphics.Imaging.BitmapPropertySet();
            var qualityValue = new Windows.Graphics.Imaging.BitmapTypedValue(
                quality, Windows.Foundation.PropertyType.Single);
            prop.Add("ImageQuality", qualityValue);
            await encoder.BitmapProperties.SetPropertiesAsync(prop);

            outStream.SetLength(0);
            await encoder.FlushAsync();
            if (outStream.Length <= 1024 * 1024 || quality <= 0.1) break;
            quality -= 0.1;
        }

        var previewId = Convert.ToHexString(SHA256.HashData(outStream.ToArray()))[..32].ToLowerInvariant();
        var prevData = _enc.EncryptBlob(outStream.ToArray(), _priv!);
        File.WriteAllBytes(Path.Combine(_prevDir, previewId + ".gyropv"), prevData);
        return previewId;
    }

    /// <summary>
    /// Generate video thumbnail using Shell thumbnail provider (StorageFile.GetThumbnailAsync).
    /// The Shell generates thumbnails for video files automatically; this is the most
    /// reliable approach for WinUI 3 desktop apps without external dependencies.
    /// </summary>
    async Task<string?> GenerateVideoPreview(string filePath, CancellationToken ct)
    {
        try
        {
            // Use Windows Shell thumbnail provider to extract video frame
            var storageFile = await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath);
            using var thumbnail = await storageFile.GetThumbnailAsync(
                ThumbnailMode.SingleItem, 256, ThumbnailOptions.UseCurrentScale);

            if (thumbnail == null || thumbnail.Size == 0) return null;

            // Decode thumbnail into SoftwareBitmap and re-encode as JPEG
            var decoder = await BitmapDecoder.CreateAsync(thumbnail);
            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(
                BitmapEncoder.JpegEncoderId, outStream);
            encoder.SetSoftwareBitmap(bitmap);

            // Adjust quality to keep ≤500KB
            double quality = 0.75;
            while (true)
            {
                var prop = new BitmapPropertySet();
                var qualityValue = new BitmapTypedValue(
                    quality, Windows.Foundation.PropertyType.Single);
                prop.Add("ImageQuality", qualityValue);
                await encoder.BitmapProperties.SetPropertiesAsync(prop);

                outStream.Size = 0;
                await encoder.FlushAsync();
                if (outStream.Size <= 1024 * 1024 || quality <= 0.1) break;
                quality -= 0.1;
            }

            // Read encoded JPEG into byte array
            outStream.Seek(0);
            using var ms = new MemoryStream();
            await outStream.AsStreamForRead().CopyToAsync(ms, ct);

            if (ms.Length == 0) return null;

            // Encrypt and store preview
            var previewId = Convert.ToHexString(SHA256.HashData(ms.ToArray()))[..32].ToLowerInvariant();
            var prevData = _enc.EncryptBlob(ms.ToArray(), _priv!);
            File.WriteAllBytes(Path.Combine(_prevDir, previewId + ".gyropv"), prevData);
            return previewId;
        }
        catch
        {
            // Video preview generation failure is non-fatal
            LogService.Warn($"GenerateVideoPreview: failed to generate thumbnail for '{filePath}'");
            return null;
        }
    }

    public string? GetPreviewId(string itemId)
    {
        try
        {
            var mp = Path.Combine(_metaDir, itemId + ".gyromt");
            if (!File.Exists(mp) || _priv == null) return null;
            var blob = File.ReadAllBytes(mp);
            var json = _enc.DecryptBlob(blob, _priv);
            var m = JsonSerializer.Deserialize<MetaFile>(System.Text.Encoding.UTF8.GetString(json), JsonConfig.Options);
            return m?.PreviewId;
        }
        catch { return null; }
    }

    public async Task<byte[]?> GetPreviewData(string previewId)
    {
        if (previewId.Contains("..") || previewId.Contains('/') || previewId.Contains('\\')) return null;
        var path = Path.Combine(_prevDir, previewId + ".gyropv");
        if (!File.Exists(path) || _priv == null) return null;
        var blob = await File.ReadAllBytesAsync(path);
        return _enc.DecryptBlob(blob, _priv);
    }
    /// <summary>
    /// Streaming chunked storage: reads from a temp file on disk, encrypts each chunk,
    /// and writes encrypted chunks to the vault. Memory usage is bounded by chunkSize.
    /// </summary>
    async Task<long> StoreChunkedStream(string filePath, long fileLength, string id, int chunkSize, MetaFile meta)
    {
        long encSize = 0;
        if (fileLength > chunkSize)
        {
            meta.ChunkCount = (int)Math.Ceiling((double)fileLength / chunkSize);
            meta.ChunkSize = chunkSize;
            var chunkDir = Path.Combine(_dataDir, id);
            Directory.CreateDirectory(chunkDir);
            var readBuf = new byte[chunkSize];
            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, FileOptions.SequentialScan);
            for (int i = 0; i < meta.ChunkCount; i++)
            {
                int totalRead = 0;
                while (totalRead < chunkSize)
                {
                    var n = await fs.ReadAsync(readBuf.AsMemory(totalRead, chunkSize - totalRead));
                    if (n == 0) break;
                    totalRead += n;
                }
                var slice = new byte[totalRead];
                Array.Copy(readBuf, slice, totalRead);
                var encChunk = _enc.EncryptBlob(slice, _priv!);
                File.WriteAllBytes(Path.Combine(chunkDir, "c" + i.ToString("x4") + ".gyrodt"), encChunk);
                encSize += encChunk.Length;
            }
        }
        else
        {
            // Small file (≤ chunkSize): read via FileStream, encrypt as single blob.
            // Release raw buffer immediately after encryption to minimize peak memory.
            byte[] raw;
            await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                raw = new byte[(int)fs.Length];
                await fs.ReadExactlyAsync(raw);
            }
            var encData = _enc.EncryptBlob(raw, _priv!);
            raw = null!;
            File.WriteAllBytes(Path.Combine(_dataDir, id + ".gyrodt"), encData);
            encSize = encData.Length;
        }
        return encSize;
    }


    // ── Version Management Helpers ──

    /// <summary>Get the version history list for the specified file.</summary>
    public IReadOnlyList<FileVersionRecord> GetVersionHistory(string fileId)
    {
        EnsureInit();
        if (_versionHistory == null) return Array.Empty<FileVersionRecord>();
        return _versionHistory.ListVersions(fileId, _priv!);
    }

    /// <summary>Restore file to the specified version.</summary>
    public async Task RestoreFileVersionAsync(string fileId, int versionNumber, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnsureInit();
        if (_versionHistory == null) throw new InvalidOperationException("Version history service not initialized");

        // Restore version data
        var data = _versionHistory.RestoreVersion(fileId, versionNumber, _priv!);

        // Read current metadata
        var meta = LoadMeta(fileId);

        // Overwrite data file
        var cfg = _config?.Load() ?? new CoreConfig();
        var chunkSize = ConfigService.ChunkSizeForTier(cfg.ChunkTier);

        if (data.Length > chunkSize)
        {
            // Chunked storage
            var chunkDir = Path.Combine(_dataDir, fileId);
            if (Directory.Exists(chunkDir))
            {
                foreach (var f in Directory.GetFiles(chunkDir, "c*.gyrodt"))
                { SecureDelete(f); File.Delete(f); }
            }
            else Directory.CreateDirectory(chunkDir);

            meta.ChunkCount = (int)Math.Ceiling((double)data.Length / chunkSize);
            meta.ChunkSize = chunkSize;

            for (int i = 0; i < meta.ChunkCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                var offset = i * chunkSize;
                var length = Math.Min(chunkSize, data.Length - offset);
                var slice = new byte[length];
                Array.Copy(data, offset, slice, 0, length);
                var encChunk = _enc.EncryptBlob(slice, _priv!);
                File.WriteAllBytes(Path.Combine(chunkDir, "c" + i.ToString("x4") + ".gyrodt"), encChunk);
                progress?.Report((double)(i + 1) / meta.ChunkCount);
            }

            // Clean up old single-file data
            var singlePath = Path.Combine(_dataDir, fileId + ".gyrodt");
            if (File.Exists(singlePath)) { SecureDelete(singlePath); File.Delete(singlePath); }
        }
        else
        {
            // Single-file storage
            var singlePath = Path.Combine(_dataDir, fileId + ".gyrodt");
            var encData = _enc.EncryptBlob(data, _priv!);
            File.WriteAllBytes(singlePath, encData);

            // Clean up old chunked data
            var chunkDir = Path.Combine(_dataDir, fileId);
            if (Directory.Exists(chunkDir))
            {
                foreach (var f in Directory.GetFiles(chunkDir, "c*.gyrodt"))
                { SecureDelete(f); File.Delete(f); }
                Directory.Delete(chunkDir);
            }

            meta.ChunkCount = 0;
            meta.ChunkSize = 0;
            progress?.Report(1.0);
        }

        // Update metadata
        meta.OriginalSize = data.Length;
        SaveMeta(fileId, meta);
    }

    /// <summary>Save the current file version (called before overwrite).</summary>
    public FileVersionRecord? SaveCurrentVersion(string fileId, string description = "")
    {
        EnsureInit();
        if (_versionHistory == null || _priv == null) return null;

        try
        {
            var meta = LoadMeta(fileId);
            byte[] data;

            if (meta.ChunkCount > 0)
            {
                // Chunked file: merge all chunks
                using var ms = new MemoryStream();
                for (int i = 0; i < meta.ChunkCount; i++)
                {
                    var cp = Path.Combine(_dataDir, fileId, "c" + i.ToString("x4") + ".gyrodt");
                    if (!File.Exists(cp))
                    {
                        LogService.Warn($"SaveCurrentVersion: chunk {i} missing for '{fileId}', cannot save complete version");
                        return null;
                    }
                    var encChunk = File.ReadAllBytes(cp);
                    var plainChunk = _enc.DecryptBlob(encChunk, _priv);
                    ms.Write(plainChunk);
                }
                data = ms.ToArray();
            }
            else
            {
                // Single file
                var dp = Path.Combine(_dataDir, fileId + ".gyrodt");
                if (!File.Exists(dp)) return null;
                var encData = File.ReadAllBytes(dp);
                data = _enc.DecryptBlob(encData, _priv);
            }

            return _versionHistory.SaveVersion(fileId, data, _priv, meta.OriginalSize, meta.ContentType, description);
        }
        catch (Exception ex)
        {
            LogService.Warn($"SaveCurrentVersion: failed for '{fileId}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Restore file to the specified version (synchronous, returns decrypted data).</summary>
    public byte[] RestoreFileVersion(string fileId, int versionNumber)
    {
        EnsureInit();
        if (_versionHistory == null) throw new InvalidOperationException("Version history service not initialized");
        return _versionHistory.RestoreVersion(fileId, versionNumber, _priv!);
    }

    /// <summary>Get the private key (for internal version management use only).</summary>
    internal byte[] GetPrivateKey()
    {
        EnsureInit();
        return _priv!;
    }

    public string CurrentPath => _currentPath;
    public void SetCurrentPath(string path) { _currentPath = path; }

    public ConfigService GetConfig() => _config;

    // ���� Folder tree persistence ����
    VaultFolder LoadTree()
    {
        try
        {
            if (File.Exists(_treeFile) && _priv != null)
            {
                var blob = File.ReadAllBytes(_treeFile);
                var json = _enc.DecryptBlob(blob, _priv);
                return JsonSerializer.Deserialize<VaultFolder>(Encoding.UTF8.GetString(json), JsonConfig.Options) ?? new VaultFolder { Name = "Gyroown", VirtualPath = "/" };
            }
        }
        catch (Exception ex) { LogService.Warn($"VaultService.LoadTree: {ex.Message}"); }
        return new VaultFolder { Name = "Gyroown", VirtualPath = "/" };
    }

    void SaveTree(VaultFolder tree)
    {
        try
        {
            if (_priv == null) { LogService.Warn("VaultService.SaveTree: vault not initialized"); return; }
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tree, JsonConfig.Options));
            var blob = _enc.EncryptBlob(json, _priv);
            File.WriteAllBytes(_treeFile, blob);
        }
        catch (Exception ex) { LogService.Warn($"VaultService.SaveTree: {ex.Message}"); }
    }

    void EnsureInit() { if (!IsInitialized) throw new InvalidOperationException("Vault not initialized"); }

    internal class MetaFile
    {
        public string Name { get; set; } = "";
        public string VirtualPath { get; set; } = "/";
        public long OriginalSize { get; set; }
        public string ContentType { get; set; } = "application/octet-stream";
        public string? PreviewId { get; set; }
        public int ChunkCount { get; set; }
        public int ChunkSize { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Modified { get; set; } = DateTime.Now;
        public bool IsFolder { get; set; }
    }
}

