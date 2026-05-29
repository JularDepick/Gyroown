import { readFileSync, writeFileSync } from 'fs';

const filePath = 'C:/Users/liwenfang/GitHub/JularDepick/Gyroown/Gyroown/Services/VaultService.cs';
let content = readFileSync(filePath, 'utf-8');

// === 1. Replace ImportItemAsync ===
const importStartMarker = '    public async Task<VaultFileItem> ImportItemAsync(Stream data';
const importEndMarker = '    public async Task ExportItemAsync';

const importStart = content.indexOf(importStartMarker);
const importEnd = content.indexOf(importEndMarker);

if (importStart < 0 || importEnd < 0) {
    console.error('ERROR: ImportItemAsync markers not found', importStart, importEnd);
    process.exit(1);
}

const newImport = [
'    public async Task<VaultFileItem> ImportItemAsync(Stream data, string name, string virtualPath = "/",',
'        IProgress<double>? progress = null, CancellationToken ct = default)',
'    {',
'        EnsureInit();',
'',
'        // Stream source to temp file with constant memory (~1MB buffer)',
'        var tmpPath = Path.GetTempFileName();',
'        try',
'        {',
'            long rawLength = 0;',
'            var sha = SHA256.Create();',
'            var buf = new byte[1024 * 1024]; // 1MB read buffer',
'            await using (var tmpFs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, buf.Length, FileOptions.SequentialScan))',
'            {',
'                int bytesRead;',
'                while ((bytesRead = await data.ReadAsync(buf, ct)) > 0)',
'                {',
'                    sha.TransformBlock(buf, 0, bytesRead, null, 0);',
'                    await tmpFs.WriteAsync(buf.AsMemory(0, bytesRead), ct);',
'                    rawLength += bytesRead;',
'                }',
'                sha.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);',
'                await tmpFs.FlushAsync(ct);',
'            }',
'',
'            // Content-hash ID: first 32 chars of SHA256 \u2014 deduplication',
'            var id = Convert.ToHexString(sha.Hash!)[..32].ToLowerInvariant();',
'',
'            var contentType = InferType(name);',
'            var meta = new MetaFile { Name = name, VirtualPath = _currentPath, OriginalSize = rawLength, ContentType = contentType };',
'',
'            // Generate preview for image/video types (cap at 50MB to avoid OOM)',
'            if ((IsImageType(contentType) || IsVideoType(contentType)) && rawLength <= 50L * 1024 * 1024)',
'            {',
'                var previewData = await File.ReadAllBytesAsync(tmpPath, ct);',
'                meta.PreviewId = await GeneratePreview(previewData, contentType, ct);',
'            }',
'',
'            // Chunked storage based on config \u2014 encrypt directly from temp file',
'            var cfg = _config?.Load() ?? new CoreConfig();',
'            var chunkSize = ConfigService.ChunkSizeForTier(cfg.ChunkTier);',
'            long encSize = await StoreChunkedStream(tmpPath, rawLength, id, chunkSize, meta);',
'',
'            var encMeta = _enc.EncryptBlob(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(meta, JsonConfig.Options)), _priv!);',
'            File.WriteAllBytes(Path.Combine(_metaDir, id + ".gyromt"), encMeta);',
'',
'            progress?.Report(1.0);',
'            return new VaultFileItem { Id = id, Name = name, VirtualPath = virtualPath, OriginalSize = rawLength, EncryptedSize = encSize, ContentType = meta.ContentType };',
'        }',
'        finally',
'        {',
'            try { File.Delete(tmpPath); } catch { /* best effort */ }',
'        }',
'    }',
].join('\n');

content = content.substring(0, importStart) + newImport + '\n\n' + content.substring(importEnd);

// === 2. Replace ExportItemAsync ===
const exportStartMarker = '    public async Task ExportItemAsync';
const exportEndMarker = '    public void DeleteItem';

const exportStart = content.indexOf(exportStartMarker);
const exportEnd = content.indexOf(exportEndMarker);

if (exportStart < 0 || exportEnd < 0) {
    console.error('ERROR: ExportItemAsync markers not found', exportStart, exportEnd);
    process.exit(1);
}

const newExport = [
'    public async Task ExportItemAsync(string itemId, Stream outStream,',
'        IProgress<double>? progress = null, CancellationToken ct = default)',
'    {',
'        EnsureInit();',
'        var meta = LoadMeta(itemId);',
'        if (meta.ChunkCount > 0)',
'        {',
'            // Chunked: read each chunk from disk via FileStream, decrypt, write to output.',
'            // Each chunk is bounded by ChunkSize (2\u201364 MB), not the total file size.',
'            for (int i = 0; i < meta.ChunkCount; i++)',
'            {',
'                ct.ThrowIfCancellationRequested();',
'                var cp = Path.Combine(_dataDir, itemId, "c" + i.ToString("x4") + ".gyrodt");',
'                if (!File.Exists(cp)) throw new FileNotFoundException("Chunk " + i + " not found");',
'                byte[] encChunk;',
'                await using (var fs = new FileStream(cp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))',
'                {',
'                    encChunk = new byte[fs.Length];',
'                    await fs.ReadExactlyAsync(encChunk, ct);',
'                }',
'                var plainChunk = _enc.DecryptBlob(encChunk, _priv!);',
'                await outStream.WriteAsync(plainChunk, ct);',
'            }',
'        }',
'        else',
'        {',
'            // Non-chunked: AES-GCM requires full ciphertext for tag verification.',
'            // Use FileStream with SequentialScan hint for efficient OS-level paging.',
'            var dp = Path.Combine(_dataDir, itemId + ".gyrodt");',
'            if (!File.Exists(dp)) throw new FileNotFoundException("Item not found");',
'            byte[] encData;',
'            await using (var fs = new FileStream(dp, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))',
'            {',
'                encData = new byte[fs.Length];',
'                await fs.ReadExactlyAsync(encData, ct);',
'            }',
'            var plain = _enc.DecryptBlob(encData, _priv!);',
'            // Release encrypted buffer before writing plaintext',
'            encData = null;',
'            await outStream.WriteAsync(plain, ct);',
'        }',
'        await outStream.FlushAsync(ct);',
'        progress?.Report(1.0);',
'    }',
].join('\n');

content = content.substring(0, exportStart) + newExport + '\n\n' + content.substring(exportEnd);

// === 3. Add StoreChunkedStream before StoreChunked ===
const storeChunkedMarker = '    long StoreChunked(byte[] raw, string id, int chunkSize, MetaFile meta)';
const storeChunkedIdx = content.indexOf(storeChunkedMarker);

if (storeChunkedIdx < 0) {
    console.error('ERROR: StoreChunked not found');
    process.exit(1);
}

const newStoreChunkedStream = [
'    /// <summary>',
'    /// Streaming chunked storage: reads from a temp file on disk, encrypts each chunk,',
'    /// and writes encrypted chunks to the vault. Memory usage is bounded by chunkSize.',
'    /// </summary>',
'    async Task<long> StoreChunkedStream(string filePath, long fileLength, string id, int chunkSize, MetaFile meta)',
'    {',
'        long encSize = 0;',
'        if (fileLength > chunkSize)',
'        {',
'            meta.ChunkCount = (int)Math.Ceiling((double)fileLength / chunkSize);',
'            meta.ChunkSize = chunkSize;',
'            var chunkDir = Path.Combine(_dataDir, id);',
'            Directory.CreateDirectory(chunkDir);',
'            var readBuf = new byte[chunkSize];',
'            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, chunkSize, FileOptions.SequentialScan);',
'            for (int i = 0; i < meta.ChunkCount; i++)',
'            {',
'                int totalRead = 0;',
'                while (totalRead < chunkSize)',
'                {',
'                    var n = await fs.ReadAsync(readBuf.AsMemory(totalRead, chunkSize - totalRead));',
'                    if (n == 0) break;',
'                    totalRead += n;',
'                }',
'                var slice = new byte[totalRead];',
'                Array.Copy(readBuf, slice, totalRead);',
'                var encChunk = _enc.EncryptBlob(slice, _priv!);',
'                File.WriteAllBytes(Path.Combine(chunkDir, "c" + i.ToString("x4") + ".gyrodt"), encChunk);',
'                encSize += encChunk.Length;',
'            }',
'        }',
'        else',
'        {',
'            // Small file: read entire file from disk, encrypt as single blob',
'            var raw = await File.ReadAllBytesAsync(filePath);',
'            var encData = _enc.EncryptBlob(raw, _priv!);',
'            File.WriteAllBytes(Path.Combine(_dataDir, id + ".gyrodt"), encData);',
'            encSize = encData.Length;',
'        }',
'        return encSize;',
'    }',
'',
].join('\n');

content = content.substring(0, storeChunkedIdx) + newStoreChunkedStream + content.substring(storeChunkedIdx);

writeFileSync(filePath, content, 'utf-8');
console.log('SUCCESS: VaultService.cs updated');
console.log('File size:', content.length, 'chars');
