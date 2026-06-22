using System.Text;
using System.Text.Json;
using Gyroown.Models;

namespace Gyroown.Services;

/// <summary>
/// Manages favorite (bookmarked) vault items.
/// Persists to ~/.Gyroown/favorites.gyrojson (encrypted with vault key).
/// </summary>
public class FavoritesService
{
    private readonly string _filePath;
    private readonly EncryptionService _enc = new();
    private readonly object _lock = new();
    private byte[]? _vaultKey;
    private List<FavoriteItem> _items = new();

    public event EventHandler? FavoritesChanged;

    public FavoritesService()
    {
        _filePath = Constants.FavoritesFile;
    }

    /// <summary>Initialize with vault key. Call after vault is unlocked.</summary>
    public void Initialize(byte[] vaultKey)
    {
        _vaultKey = (byte[])vaultKey.Clone();
    }

    /// <summary>Clear vault key from memory. Call when locking the vault.</summary>
    public void ClearKey()
    {
        if (_vaultKey != null) { Array.Clear(_vaultKey); _vaultKey = null; }
    }

    /// <summary>Load favorites from disk (encrypted).</summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_filePath) && _vaultKey != null)
            {
                var blob = File.ReadAllBytes(_filePath);
                if (blob.Length == 0)
                {
                    LogService.Warn("FavoritesService.Load: empty favorites file, resetting");
                    lock (_lock) _items = new List<FavoriteItem>();
                    return;
                }
                var json = _enc.DecryptBlob(blob, _vaultKey);
                var items = JsonSerializer.Deserialize<List<FavoriteItem>>(json, JsonConfig.Options);
                lock (_lock) _items = items ?? new List<FavoriteItem>();
            }
        }
        catch (Exception ex)
        {
            LogService.Warn($"FavoritesService.Load: {ex.Message}");
            lock (_lock) _items = new List<FavoriteItem>();
        }
    }

    /// <summary>Save favorites to disk (encrypted, async).</summary>
    public async Task SaveAsync()
    {
        var key = _vaultKey;
        if (key == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? Constants.VaultRoot);
            List<FavoriteItem> snapshot;
            lock (_lock) snapshot = new List<FavoriteItem>(_items);
            var json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, JsonConfig.Options));
            var blob = _enc.EncryptBlob(json, key);
            await File.WriteAllBytesAsync(_filePath, blob);
        }
        catch (Exception ex) { LogService.Warn($"FavoritesService.SaveAsync: {ex.Message}"); }
    }

    /// <summary>Fire-and-forget save for synchronous call sites.</summary>
    private void Save() { if (_vaultKey != null) _ = SaveAsync(); }

    /// <summary>Get all favorites, ordered by group then order.</summary>
    public IReadOnlyList<FavoriteItem> GetAll()
    { lock (_lock) return _items.OrderBy(f => f.Group).ThenBy(f => f.Order).ToList(); }

    /// <summary>Get favorites grouped by their Group property.</summary>
    public IReadOnlyDictionary<string, List<FavoriteItem>> GetGrouped()
    { lock (_lock) return _items.GroupBy(f => f.Group).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.OrderBy(f => f.Order).ToList()); }

    /// <summary>Get all group names.</summary>
    public IReadOnlyList<string> GetGroups()
    { lock (_lock) return _items.Select(f => f.Group).Distinct().OrderBy(g => g).ToList(); }

    /// <summary>Check whether a vault item is already favorited.</summary>
    public bool IsFavorited(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        lock (_lock) return _items.Any(f => f.ItemId == itemId);
    }

    /// <summary>Add a vault item to favorites.</summary>
    public FavoriteItem Add(string itemId, string name, string itemPath, bool isFolder, string contentType, string group = "Default")
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item ID cannot be empty.", nameof(itemId));
        lock (_lock)
        {
            if (_items.Any(f => f.ItemId == itemId))
                return _items.First(f => f.ItemId == itemId);

            var maxOrder = _items.Where(f => f.Group == group).Select(f => f.Order).DefaultIfEmpty(-1).Max();
            var fav = new FavoriteItem
            {
                ItemId = itemId,
                Name = name,
                ItemPath = itemPath,
                IsFolder = isFolder,
                ContentType = contentType,
                Group = group,
                Order = maxOrder + 1
            };
            _items.Add(fav);
        }
        Save();
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return new FavoriteItem { ItemId = itemId, Name = name };
    }

    /// <summary>Remove a favorite by vault item ID.</summary>
    public bool Remove(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        int removed;
        lock (_lock) removed = _items.RemoveAll(f => f.ItemId == itemId);
        if (removed > 0)
        {
            Save();
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    /// <summary>Toggle favorite state for a vault item.</summary>
    public bool Toggle(string itemId, string name, string itemPath, bool isFolder, string contentType, string group = "Default")
    {
        if (string.IsNullOrWhiteSpace(itemId))
            throw new ArgumentException("Item ID cannot be empty.", nameof(itemId));
        lock (_lock)
        {
            if (_items.Any(f => f.ItemId == itemId))
            {
                _items.RemoveAll(f => f.ItemId == itemId);
                Save();
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }
            else
            {
                var maxOrder = _items.Where(f => f.Group == group).Select(f => f.Order).DefaultIfEmpty(-1).Max();
                var fav = new FavoriteItem
                {
                    ItemId = itemId, Name = name, ItemPath = itemPath,
                    IsFolder = isFolder, ContentType = contentType,
                    Group = group, Order = maxOrder + 1
                };
                _items.Add(fav);
                Save();
                FavoritesChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
    }

    /// <summary>Rename a favorite's display name.</summary>
    public void Rename(string favoriteId, string newName)
    {
        if (string.IsNullOrWhiteSpace(favoriteId)) return;
        if (string.IsNullOrWhiteSpace(newName)) return;
        FavoriteItem? fav;
        lock (_lock) fav = _items.FirstOrDefault(f => f.Id == favoriteId);
        if (fav != null)
        {
            fav.Name = newName;
            Save();
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Move a favorite to a different group.</summary>
    public void MoveToGroup(string favoriteId, string newGroup)
    {
        if (string.IsNullOrWhiteSpace(favoriteId) || string.IsNullOrWhiteSpace(newGroup)) return;
        lock (_lock)
        {
            var fav = _items.FirstOrDefault(f => f.Id == favoriteId);
            if (fav != null)
            {
                fav.Group = newGroup;
                var maxOrder = _items.Where(f => f.Group == newGroup && f.Id != favoriteId).Select(f => f.Order).DefaultIfEmpty(-1).Max();
                fav.Order = maxOrder + 1;
            }
        }
        Save();
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reorder favorites within a group by providing the new order of favorite IDs.</summary>
    public void Reorder(string group, IReadOnlyList<string> orderedIds)
    {
        if (string.IsNullOrWhiteSpace(group) || orderedIds == null || orderedIds.Count == 0) return;
        lock (_lock)
        {
            var groupItems = _items.Where(f => f.Group == group).ToList();
            for (int i = 0; i < orderedIds.Count; i++)
            {
                var fav = groupItems.FirstOrDefault(f => f.Id == orderedIds[i]);
                if (fav != null) fav.Order = i;
            }
        }
        Save();
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Rename all favorites in a group.</summary>
    public void RenameGroup(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;
        lock (_lock) { foreach (var fav in _items.Where(f => f.Group == oldName)) fav.Group = newName; }
        Save();
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Delete an entire group and all its favorites.</summary>
    public void DeleteGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return;
        lock (_lock) _items.RemoveAll(f => f.Group == groupName);
        Save();
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Validate favorites against the current vault item list.
    /// Returns items whose vault counterpart no longer exists.
    /// </summary>
    public List<FavoriteItem> FindOrphans(IReadOnlyList<VaultFileItem> vaultItems)
    {
        if (vaultItems == null) return new List<FavoriteItem>();
        var vaultIds = new HashSet<string>(vaultItems.Select(v => v.Id));
        lock (_lock) return _items.Where(f => !vaultIds.Contains(f.ItemId)).ToList();
    }

    /// <summary>Remove orphaned favorites that no longer exist in the vault.</summary>
    public int RemoveOrphans(IReadOnlyList<VaultFileItem> vaultItems)
    {
        if (vaultItems == null || vaultItems.Count == 0) return 0;
        var vaultIds = new HashSet<string>(vaultItems.Select(v => v.Id));
        int count;
        lock (_lock)
        {
            count = _items.RemoveAll(f => !vaultIds.Contains(f.ItemId));
        }
        if (count > 0)
        {
            Save();
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }
        return count;
    }
}
