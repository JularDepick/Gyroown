namespace Gyroown.Services;

/// <summary>
/// Drag-and-drop service interface.
/// Drop-in: encrypt and import files into the vault (memory-stream only).
/// Drag-out: disabled by security policy (no decryption to temp files); use the export function instead.
/// </summary>
public interface IDragDropService
{
    /// <summary>Handle file drop-in (encrypt and store).</summary>
    Task HandleDropInAsync(IReadOnlyList<string> filePaths,
        IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Handle file drag-out (decrypt to temp, return paths).</summary>
    Task<IReadOnlyList<string>> HandleDragOutAsync(IReadOnlyList<string> itemIds,
        IProgress<double>? progress = null, CancellationToken ct = default);
}
