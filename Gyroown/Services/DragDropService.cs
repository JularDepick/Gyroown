namespace Gyroown.Services;

/// <summary>
/// Drag-drop service.
/// Drag-in (drop into vault): encrypts &amp; imports files.
/// Drag-out: handled directly in VaultFileListView via DecryptToFile callback.
/// </summary>
public class DragDropService : IDragDropService
{
    private readonly VaultService _vault;

    public DragDropService(VaultService vault) => _vault = vault;

    public async Task HandleDropInAsync(IReadOnlyList<string> filePaths,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var errors = new List<string>();
        for (int i = 0; i < filePaths.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var path = filePaths[i];
            try
            {
                await using var fs = File.OpenRead(path);
                await _vault.ImportItemAsync(fs, Path.GetFileName(path), "/", progress, ct);
            }
            catch (Exception ex)
            {
                LogService.Warn($"HandleDropInAsync: failed to import '{path}': {ex.Message}");
                errors.Add(path);
            }
            progress?.Report((double)(i + 1) / filePaths.Count);
        }
        if (errors.Count > 0)
            LogService.Warn($"HandleDropInAsync: {errors.Count}/{filePaths.Count} files failed to import");
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
