namespace Gyroown.Services;

/// <summary>
/// Drag-drop service interface.
/// Core logic reserved (stub).
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
