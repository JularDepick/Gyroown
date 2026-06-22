using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gyroown.Models;
using Gyroown.Services;

namespace Gyroown.Controls;

public sealed partial class VaultSidebar : UserControl
{
    public event EventHandler<string>? FolderSelected;
    public event EventHandler<VaultFileItem>? RecentFileOpened;
    public event EventHandler<(VaultFileItem Item, string TargetPath)>? FileDropToFolder;

    /// <summary>Access the embedded FavoritesPanel.</summary>
    public FavoritesPanel FavoritesPanel => Favorites;

    // WinUI 3 TreeViewNode has no Tag property — maintain a node-to-path mapping
    private readonly Dictionary<TreeViewNode, string> _nodePaths = new();

    // Quick Access - recent files (max 10)
    private readonly List<VaultFileItem> _recentFiles = new();
    private const int MaxRecentFiles = 10;

    public VaultSidebar()
    {
        InitializeComponent();
        var root = new TreeViewNode { Content = Services.Loc.Get("Sidebar", "Vault") };
        _nodePaths[root] = "/";
        FolderTreeView.RootNodes.Add(root);
        FoldersLabel.Text = Loc.Get("Sidebar", "Folders");
        QuickAccessLabel.Text = Loc.Get("Sidebar", "QuickAccess");
        var handler = (EventHandler)((_, _) =>
        {
            if (FolderTreeView.RootNodes.Count > 0)
                FolderTreeView.RootNodes[0].Content = Services.Loc.Get("Sidebar", "Vault");
            FoldersLabel.Text = Loc.Get("Sidebar", "Folders");
            QuickAccessLabel.Text = Loc.Get("Sidebar", "QuickAccess");
        });
        Services.Loc.LanguageChanged += handler;
        Unloaded += (_, _) => Services.Loc.LanguageChanged -= handler;
    }

    /// <summary>Add a file to quick access (recent files).</summary>
    public void AddToRecentFiles(VaultFileItem item)
    {
        // Remove if already exists
        _recentFiles.RemoveAll(r => r.Id == item.Id);
        // Insert at top
        _recentFiles.Insert(0, item);
        // Trim to max
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);
        RefreshQuickAccess();
    }

    private void RefreshQuickAccess()
    {
        QuickAccessContainer.Children.Clear();
        foreach (var item in _recentFiles)
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 4, 8, 4),
                MinHeight = 28,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Tag = item
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sp.Children.Add(new FontIcon
            {
                Glyph = item.IconGlyph,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = item.Name,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis
            });
            btn.Content = sp;
            btn.Click += (_, _) => RecentFileOpened?.Invoke(this, item);
            QuickAccessContainer.Children.Add(btn);
        }
    }

    /// <summary>Build tree nodes from VaultFolder.</summary>
    public void BuildTree(VaultFolder folder)
    {
        FolderTreeView.RootNodes.Clear();
        _nodePaths.Clear();
        // Always use localized root name, not the stored folder name
        var root = BuildNode(folder, isRoot: true);
        FolderTreeView.RootNodes.Add(root);
    }

    private TreeViewNode BuildNode(VaultFolder folder, bool isRoot = false)
    {
        var node = new TreeViewNode { Content = isRoot ? Loc.Get("Sidebar", "Vault") : folder.Name };
        _nodePaths[node] = folder.VirtualPath;
        foreach (var sub in folder.SubFolders)
            node.Children.Add(BuildNode(sub));
        return node;
    }

    private void OnFolderSelected(object sender, Microsoft.UI.Xaml.Controls.TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TreeViewNode node)
            FolderSelected?.Invoke(this, _nodePaths.TryGetValue(node, out var path) ? path : "/");
    }

    private void OnFolderDragOver(object s, DragEventArgs e)
    {
        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.Caption = Loc.Get("Sidebar", "DropToMove");
    }

    private void OnFolderDrop(object s, DragEventArgs e)
    {
        // Find the target folder node from the drop position
        var pos = e.GetPosition(FolderTreeView);
        var targetNode = FindNodeAtPosition(FolderTreeView, pos);
        if (targetNode == null) return;

        var targetPath = _nodePaths.TryGetValue(targetNode, out var path) ? path : "/";
        if (targetPath == "/") return; // Don't allow drop to root

        // Notify MainWindow to handle the file move
        // The actual file data is handled by the file list's drag system
        // We just need to signal the target path
        FileDropToFolder?.Invoke(this, (null!, targetPath));
    }

    private static TreeViewNode? FindNodeAtPosition(TreeView tree, Windows.Foundation.Point pos)
    {
        // Walk the visual tree to find the TreeViewItem at the position
        var hit = Microsoft.UI.Xaml.Media.VisualTreeHelper.FindElementsInHostCoordinates(pos, tree);
        foreach (var element in hit)
        {
            if (element is TreeViewItem tvi)
            {
                var node = tree.NodeFromContainer(tvi);
                return node;
            }
        }
        return null;
    }
}
