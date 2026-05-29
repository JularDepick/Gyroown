using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gyroown.Models;
using Gyroown.Services;

namespace Gyroown.Controls;

public sealed partial class VaultSidebar : UserControl
{
    public event EventHandler<string>? FolderSelected;

    /// <summary>Access the embedded FavoritesPanel.</summary>
    public FavoritesPanel FavoritesPanel => Favorites;

    // WinUI 3 TreeViewNode has no Tag property — maintain a node-to-path mapping
    private readonly Dictionary<TreeViewNode, string> _nodePaths = new();

    public VaultSidebar()
    {
        InitializeComponent();
        var root = new TreeViewNode { Content = Services.Loc.Get("Sidebar", "Vault") };
        _nodePaths[root] = "/";
        FolderTreeView.RootNodes.Add(root);
        FoldersLabel.Text = Loc.Get("Sidebar", "Folders");
        var handler = (EventHandler)((_, _) =>
        {
            if (FolderTreeView.RootNodes.Count > 0)
                FolderTreeView.RootNodes[0].Content = Services.Loc.Get("Sidebar", "Vault");
            FoldersLabel.Text = Loc.Get("Sidebar", "Folders");
        });
        Services.Loc.LanguageChanged += handler;
        Unloaded += (_, _) => Services.Loc.LanguageChanged -= handler;
    }

    /// <summary>Build tree nodes from VaultFolder.</summary>
    public void BuildTree(VaultFolder folder)
    {
        FolderTreeView.RootNodes.Clear();
        _nodePaths.Clear();
        var root = BuildNode(folder);
        FolderTreeView.RootNodes.Add(root);
    }

    private TreeViewNode BuildNode(VaultFolder folder)
    {
        var node = new TreeViewNode { Content = folder.Name };
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
}
