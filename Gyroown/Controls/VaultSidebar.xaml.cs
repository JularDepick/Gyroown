using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gyroown.Models;

namespace Gyroown.Controls;

public sealed partial class VaultSidebar : UserControl
{
    public event EventHandler<string>? FolderSelected;

    public VaultSidebar()
    {
        InitializeComponent();
        var root = new TreeViewNode { Content = Services.Loc.Get("Sidebar", "Vault") };
        FolderTreeView.RootNodes.Add(root);
        Services.Loc.LanguageChanged += (_, _) => { if (FolderTreeView.RootNodes.Count > 0) FolderTreeView.RootNodes[0].Content = Services.Loc.Get("Sidebar", "Vault"); };
    }

    /// <summary>Build tree nodes from VaultFolder.</summary>
    public void BuildTree(VaultFolder folder)
    {
        FolderTreeView.RootNodes.Clear();
        var root = BuildNode(folder);
        FolderTreeView.RootNodes.Add(root);
    }

    private TreeViewNode BuildNode(VaultFolder folder)
    {
        var node = new TreeViewNode { Content = folder.Name };
        foreach (var sub in folder.SubFolders)
            node.Children.Add(BuildNode(sub));
        return node;
    }

    private void OnFolderSelected(object sender, Microsoft.UI.Xaml.Controls.TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TreeViewNode node)
            FolderSelected?.Invoke(this, node.Content?.ToString() ?? "/");
    }
}
