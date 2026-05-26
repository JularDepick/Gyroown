namespace Gyroown.Services;

/// <summary>
/// Drag-drop service.
/// Drag-in (drop into vault): encrypts &amp; imports files. Uses in-memory streams only.
/// Drag-out: DISABLED for security — decrypting to temp files would leak plaintext to disk.
/// Use Move Out or Export (user-chosen destination) instead.
/// </summary>
public class DragDropService : IDragDropService
{
    private readonly VaultService _vault;

    public DragDropService(VaultService vault) => _vault = vault;

    public async Task HandleDropInAsync(IReadOnlyList<string> filePaths,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        for (int i = 0; i < filePaths.Count; i++)
        {
            var path = filePaths[i];
            await using var fs = File.OpenRead(path);
            await _vault.ImportItemAsync(fs, Path.GetFileName(path), "/", progress, ct);
            progress?.Report((double)(i + 1) / filePaths.Count);
        }
    }

    /// <summary>
    /// Drag-out is disabled. Decrypting to temp/cache directories would violate
    /// the security principle of never writing plaintext to disk automatically.
    /// Use Export or Move Out via the toolbar instead.
    /// </summary>
    public Task<IReadOnlyList<string>> HandleDragOutAsync(IReadOnlyList<string> itemIds,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
