using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gyroown.Models;

namespace Gyroown.Services;

/// <summary>
/// Each original file �?data/{hashID}.gyrodt + meta/{hashID}.gyromt.
/// Both files encrypted with the vault key pair via EncryptionService.EncryptBlob/DecryptBlob.
/// File listing by scanning meta/ directory �?no separate index file.
/// </summary>
public class VaultService : IVaultService
{
    private readonly string _dataDir, _metaDir, _prevDir, _treeFile;
    private readonly EncryptionService _enc = new();
    private readonly ConfigService _config = new();
    private byte[]? _priv;
    private string _currentPath = "/";

    public bool IsInitialized => _priv != null;
    public string VaultPath => _dataDir;

    public VaultService()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown");
        _dataDir = Path.Combine(root, "data");
        _metaDir = Path.Combine(root, "meta");
        _prevDir = Path.Combine(root, "preview");
        _treeFile = Path.Combine(_metaDir, "tree.gyrojson");
    }

    public void Initialize(byte[] priv, byte[] pub)
    { _priv = priv; _config.Initialize(priv); Directory.CreateDirectory(_dataDir); Directory.CreateDirectory(_metaDir); Directory.CreateDirectory(_prevDir); }

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
    /// Scan meta/ and data/ directories for orphaned files.
    /// Returns (orphanedMeta, orphanedData, pairedCount).
    /// </summary>
    public (List<string> OrphanedMeta, List<string> OrphanedData, int Paired) CheckIntegrity()
    {
        var metaIds = new HashSet<string>();
        var dataIds = new HashSet<string>();
        var orphanedMeta = new List<string>();
        var orphanedData = new List<string>();
        int paired = 0;

        if (Directory.Exists(_metaDir))
            foreach (var f in Directory.GetFiles(_metaDir, "*.gyromt"))
                metaIds.Add(Path.GetFileNameWithoutExtension(f));

        if (Directory.Exists(_dataDir))
            foreach (var f in Directory.GetFiles(_dataDir, "*.gyrodt"))
                dataIds.Add(Path.GetFileNameWithoutExtension(f));

        foreach (var id in metaIds)
        {
            if (dataIds.Contains(id)) { paired++; dataIds.Remove(id); }
            else orphanedMeta.Add(id);
        }
        orphanedData.AddRange(dataIds); // remaining data files have no meta

        return (orphanedMeta, orphanedData, paired);
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
                items.Add(new VaultFileItem
                {
                    Id = id, Name = m.Name, VirtualPath = m.VirtualPath,
                    OriginalSize = m.OriginalSize,
                    EncryptedSize = File.Exists(Path.Combine(_dataDir, id + ".gyrodt")) ? new FileInfo(Path.Combine(_dataDir, id + ".gyrodt")).Length : 0,
                    ContentType = m.ContentType, CreatedAt = m.Created, ModifiedAt = m.Modified, IsFolder = m.IsFolder
                });
            }
            catch (CryptographicException ex) { LogService.Error($"ListItems: crypto error on {f}: {ex.Message}"); }
            catch (Exception ex) { LogService.Error($"ListItems: error on {f}: {ex.Message}"); }
        }
        return items;
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
        using var ms = new MemoryStream(); await data.CopyToAsync(ms, ct); var raw = ms.ToArray();

        // Content-hash ID: first 32 chars of SHA256 �?deduplication
        var id = Convert.ToHexString(SHA256.HashData(raw))[..32].ToLowerInvariant();

        var contentType = InferType(name);
        var meta = new MetaFile { Name = name, VirtualPath = _currentPath, OriginalSize = raw.Length, ContentType = contentType };

        // Generate preview for image/video types
        if (IsImageType(contentType) || IsVideoType(contentType))
            meta.PreviewId = await GeneratePreview(raw, contentType, ct);

        // Chunked storage based on config
        var cfg = _config?.Load() ?? new CoreConfig();
        var chunkSize = ConfigService.ChunkSizeForTier(cfg.ChunkTier);
        long encSize = StoreChunked(raw, id, chunkSize, meta);

        var encMeta = _enc.EncryptBlob(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(meta, JsonConfig.Options)), _priv!);
        File.WriteAllBytes(Path.Combine(_metaDir, id + ".gyromt"), encMeta);

        progress?.Report(1.0);
        return new VaultFileItem { Id = id, Name = name, VirtualPath = virtualPath, OriginalSize = raw.Length, EncryptedSize = encSize, ContentType = meta.ContentType };
    }

    public async Task ExportItemAsync(string itemId, Stream outStream,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnsureInit();
        var meta = LoadMeta(itemId);
        // Handle chunked files
        if (meta.ChunkCount > 0)
        {
            for (int i = 0; i < meta.ChunkCount; i++)
            {
                var cp = Path.Combine(_dataDir, itemId, $"c{i:x4}.gyrodt");
                if (!File.Exists(cp)) throw new FileNotFoundException($"Chunk {i} not found");
                var encChunk = await File.ReadAllBytesAsync(cp, ct);
                var plainChunk = _enc.DecryptBlob(encChunk, _priv!);
                await outStream.WriteAsync(plainChunk, ct);
            }
        }
        else
        {
            var dp = Path.Combine(_dataDir, itemId + ".gyrodt");
            if (!File.Exists(dp)) throw new FileNotFoundException("Item not found");
            var encData = await File.ReadAllBytesAsync(dp, ct);
            var plain = _enc.DecryptBlob(encData, _priv!);
            await outStream.WriteAsync(plain, ct);
        }
        await outStream.FlushAsync(ct);
        progress?.Report(1.0);
    }

    public void DeleteItem(string id)
    {
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

    public void MoveItem(string id, string np) { var m = LoadMeta(id); m.VirtualPath = np; SaveMeta(id, m); }
    public void RenameItem(string id, string nn) { var m = LoadMeta(id); m.Name = nn; SaveMeta(id, m); }

    public void CreateFolder(string name)
    {
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
        foreach (var f in Directory.GetFiles(_metaDir, "*.gyromt"))
        {
            var m = LoadMeta(Path.GetFileNameWithoutExtension(f));
            if (m.VirtualPath == virtualPath || m.VirtualPath.StartsWith(virtualPath + "/"))
                DeleteItem(Path.GetFileNameWithoutExtension(f));
        }
    }

    MetaFile LoadMeta(string id)
    {
        var blob = File.ReadAllBytes(Path.Combine(_metaDir, id + ".gyromt"));
        return JsonSerializer.Deserialize<MetaFile>(Encoding.UTF8.GetString(_enc.DecryptBlob(blob, _priv!)), JsonConfig.Options)!;
    }

    void SaveMeta(string id, MetaFile m)
    {
        m.Modified = DateTime.Now;
        File.WriteAllBytes(Path.Combine(_metaDir, id + ".gyromt"), _enc.EncryptBlob(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m, JsonConfig.Options)), _priv!));
    }

    static void SecureDelete(string path)
    {
        var sz = new FileInfo(path).Length;
        var r = RandomNumberGenerator.GetBytes((int)Math.Min(sz, 4096));
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write);
        for (long p = 0; p < sz; p += r.Length) fs.Write(r, 0, (int)Math.Min(r.Length, sz - p));
        fs.Flush();
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

    async Task<string?> GeneratePreview(byte[] rawData, string contentType, CancellationToken ct)
    {
        try
        {
            if (IsImageType(contentType))
                return await GenerateImagePreview(rawData, ct);
            if (IsVideoType(contentType))
                return await Task.FromResult<string?>(null); // video stub
        }
        catch { /* preview generation failure is non-fatal */ }
        return null;
    }

    async Task<string> GenerateImagePreview(byte[] rawData, CancellationToken ct)
    {
        using var inStream = new MemoryStream(rawData);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(inStream.AsRandomAccessStream());
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
            if (outStream.Length <= 500 * 1024 || quality <= 0.1) break;
            quality -= 0.1;
        }

        var previewId = Convert.ToHexString(SHA256.HashData(outStream.ToArray()))[..32].ToLowerInvariant();
        var prevData = _enc.EncryptBlob(outStream.ToArray(), _priv!);
        File.WriteAllBytes(Path.Combine(_prevDir, previewId + ".gyropv"), prevData);
        return previewId;
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
        var path = Path.Combine(_prevDir, previewId + ".gyropv");
        if (!File.Exists(path) || _priv == null) return null;
        var blob = await File.ReadAllBytesAsync(path);
        return _enc.DecryptBlob(blob, _priv);
    }
    long StoreChunked(byte[] raw, string id, int chunkSize, MetaFile meta)
    {
        long encSize = 0;
        if (raw.Length > chunkSize)
        {
            meta.ChunkCount = (int)Math.Ceiling((double)raw.Length / chunkSize);
            meta.ChunkSize = chunkSize;
            for (int i = 0; i < meta.ChunkCount; i++)
            {
                var offset = i * chunkSize;
                var len = Math.Min(chunkSize, raw.Length - offset);
                var slice = new byte[len];
                Array.Copy(raw, offset, slice, 0, len);
                var encChunk = _enc.EncryptBlob(slice, _priv!);
                var chunkDir = Path.Combine(_dataDir, id);
                Directory.CreateDirectory(chunkDir);
                File.WriteAllBytes(Path.Combine(chunkDir, $"c{i:x4}.gyrodt"), encChunk);
                encSize += encChunk.Length;
            }
        }
        else
        {
            var encData = _enc.EncryptBlob(raw, _priv!);
            File.WriteAllBytes(Path.Combine(_dataDir, id + ".gyrodt"), encData);
            encSize = encData.Length;
        }
        return encSize;
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
        catch { }
        return new VaultFolder { Name = "Gyroown", VirtualPath = "/" };
    }

    void SaveTree(VaultFolder tree)
    {
        try
        {
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tree, JsonConfig.Options));
            var blob = _enc.EncryptBlob(json, _priv!);
            File.WriteAllBytes(_treeFile, blob);
        }
        catch { }
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

