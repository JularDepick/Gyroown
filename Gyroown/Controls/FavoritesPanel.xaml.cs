using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Gyroown.Models;
using Gyroown.Services;

namespace Gyroown.Controls;

public sealed partial class FavoritesPanel : UserControl
{
    private FavoritesService? _favorites;
    private bool _isExpanded = true;

    /// <summary>Raised when the user clicks a favorite to navigate to it.</summary>
    public event EventHandler<FavoriteItem>? FavoriteNavigate;

    /// <summary>Raised when the user removes a favorite.</summary>
    public event EventHandler<FavoriteItem>? FavoriteRemoved;

    /// <summary>Raised when the user renames a favorite.</summary>
    public event EventHandler<FavoriteItem>? FavoriteRenamed;

    /// <summary>Raised when the user moves a favorite to a different group.</summary>
    public event EventHandler<(FavoriteItem Item, string NewGroup)>? FavoriteMovedToGroup;

    /// <summary>Raised when the user renames a group.</summary>
    public event EventHandler<(string OldName, string NewName)>? GroupRenamed;

    /// <summary>Raised when the user deletes a group.</summary>
    public event EventHandler<string>? GroupDeleted;

    public FavoritesPanel()
    {
        InitializeComponent();
        HeaderText.Text = Loc.Get("Favorites", "Title");
    }

    private EventHandler? _favoritesChangedHandler;

    /// <summary>Bind to the FavoritesService and render.</summary>
    public void Initialize(FavoritesService favorites)
    {
        if (_favorites != null && _favoritesChangedHandler != null)
            _favorites.FavoritesChanged -= _favoritesChangedHandler;
        _favorites = favorites;
        _favoritesChangedHandler = (_, _) => DispatcherQueue.TryEnqueue(BuildList);
        _favorites.FavoritesChanged += _favoritesChangedHandler;
        BuildList();
    }

    /// <summary>Refresh the favorites list.</summary>
    public void Refresh() => BuildList();

    private void OnToggleExpand(object sender, RoutedEventArgs e)
    {
        _isExpanded = !_isExpanded;
        ContentScroller.Visibility = _isExpanded ? Visibility.Visible : Visibility.Collapsed;
        ToggleIcon.Glyph = _isExpanded ? "\uE70D" : "\uE70E"; // chevron down / up
    }

    private void BuildList()
    {
        FavoritesContainer.Children.Clear();
        if (_favorites == null) return;

        var grouped = _favorites.GetGrouped();
        if (grouped.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = Loc.Get("Favorites", "Empty"),
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                Margin = new Thickness(12, 4, 12, 4),
                TextWrapping = TextWrapping.Wrap
            };
            FavoritesContainer.Children.Add(empty);
            return;
        }

        foreach (var (groupName, items) in grouped)
        {
            // Group header
            var groupExpander = CreateGroupSection(groupName, items);
            FavoritesContainer.Children.Add(groupExpander);
        }
    }

    private FrameworkElement CreateGroupSection(string groupName, List<FavoriteItem> items)
    {
        var container = new StackPanel { Spacing = 1 };

        // Group header row
        var groupHeader = new Grid
        {
            Margin = new Thickness(4, 4, 4, 2)
        };
        groupHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        groupHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        groupHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var expandIcon = new FontIcon
        {
            Glyph = "\uE70D",
            FontSize = 10,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(expandIcon, 0);

        var groupLabel = new TextBlock
        {
            Text = groupName,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(groupLabel, 1);

        var groupMenuBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE712", FontSize = 12 },
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = groupName
        };
        groupMenuBtn.Click += OnGroupMenuClick;
        Grid.SetColumn(groupMenuBtn, 2);

        groupHeader.Children.Add(expandIcon);
        groupHeader.Children.Add(groupLabel);
        groupHeader.Children.Add(groupMenuBtn);

        container.Children.Add(groupHeader);

        // Items list
        var itemsList = new StackPanel { Spacing = 1 };
        foreach (var item in items)
        {
            itemsList.Children.Add(CreateFavoriteItemRow(item));
        }

        // Toggle group visibility on header click
        groupHeader.PointerPressed += (_, e) =>
        {
            // Ignore clicks on the menu button
            if (e.OriginalSource is DependencyObject d)
            {
                var parent = d;
                while (parent != null)
                {
                    if (parent == groupMenuBtn) return;
                    parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
                }
            }
            var visible = itemsList.Visibility == Visibility.Visible;
            itemsList.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
            expandIcon.Glyph = visible ? "\uE70D" : "\uE70E";
        };

        container.Children.Add(itemsList);
        return container;
    }

    private FrameworkElement CreateFavoriteItemRow(FavoriteItem item)
    {
        var row = new Grid
        {
            Padding = new Thickness(20, 4, 8, 4),
            Tag = item
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon
        {
            Glyph = item.IconGlyph,
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);

        var nameBlock = new TextBlock
        {
            Text = item.Name,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTipService.SetToolTip(nameBlock, item.ItemPath);
        Grid.SetColumn(nameBlock, 1);

        var removeBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE711", FontSize = 10 },
            Padding = new Thickness(4, 2, 4, 2),
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Tag = item
        };
        removeBtn.Click += OnRemoveClick;
        Grid.SetColumn(removeBtn, 2);

        row.Children.Add(icon);
        row.Children.Add(nameBlock);
        row.Children.Add(removeBtn);

        // Hover: show remove button and highlight
        row.PointerEntered += (_, _) =>
        {
            removeBtn.Visibility = Visibility.Visible;
            row.Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"];
        };
        row.PointerExited += (_, _) =>
        {
            removeBtn.Visibility = Visibility.Collapsed;
            row.Background = null;
        };

        // Click: navigate
        var tapArea = new Microsoft.UI.Xaml.Input.TappedEventHandler((_, _) =>
            FavoriteNavigate?.Invoke(this, item));
        row.Tapped += tapArea;

        // Right-click: context menu
        row.RightTapped += (_, e) =>
        {
            if (e.OriginalSource is FrameworkElement fe)
            {
                ShowItemContextMenu(fe, item, e.GetPosition(fe));
            }
        };

        return row;
    }

    private void ShowItemContextMenu(FrameworkElement anchor, FavoriteItem item, Windows.Foundation.Point point)
    {
        var menu = new MenuFlyout();

        var navigate = new MenuFlyoutItem
        {
            Text = Loc.Get("Favorites", "Navigate"),
            Icon = new FontIcon { Glyph = "\uE8A7" }
        };
        navigate.Click += (_, _) => FavoriteNavigate?.Invoke(this, item);

        var rename = new MenuFlyoutItem
        {
            Text = Loc.Get("Favorites", "Rename"),
            Icon = new FontIcon { Glyph = "\uE8AC" }
        };
        rename.Click += (_, _) => FavoriteRenamed?.Invoke(this, item);

        var remove = new MenuFlyoutItem
        {
            Text = Loc.Get("Favorites", "Remove"),
            Icon = new FontIcon { Glyph = "\uE74D" }
        };
        remove.Click += (_, _) => FavoriteRemoved?.Invoke(this, item);

        // Move to group submenu
        var moveTo = new MenuFlyoutSubItem
        {
            Text = Loc.Get("Favorites", "MoveToGroup"),
            Icon = new FontIcon { Glyph = "\uE8DE" }
        };

        if (_favorites != null)
        {
            var groups = _favorites.GetGroups();
            foreach (var g in groups)
            {
                if (g == item.Group) continue;
                var groupItem = new MenuFlyoutItem { Text = g, Tag = g };
                groupItem.Click += (_, _) => FavoriteMovedToGroup?.Invoke(this, (item, g));
                moveTo.Items.Add(groupItem);
            }
            // New group option
            if (moveTo.Items.Count > 0) moveTo.Items.Add(new MenuFlyoutSeparator());
            var newGroup = new MenuFlyoutItem
            {
                Text = Loc.Get("Favorites", "NewGroup"),
                Icon = new FontIcon { Glyph = "\uE710" }
            };
            newGroup.Click += async (_, _) => await PromptNewGroup(item);
            moveTo.Items.Add(newGroup);
        }

        menu.Items.Add(navigate);
        menu.Items.Add(rename);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(moveTo);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(remove);

        menu.ShowAt(anchor, point);
    }

    private async Task PromptNewGroup(FavoriteItem item)
    {
        var input = new TextBox { PlaceholderText = Loc.Get("Favorites", "GroupName") };
        var dialog = new ContentDialog
        {
            Title = Loc.Get("Favorites", "NewGroup"),
            Content = input,
            PrimaryButtonText = Loc.Get("Common", "OK"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        {
            FavoriteMovedToGroup?.Invoke(this, (item, input.Text.Trim()));
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FavoriteItem item)
            FavoriteRemoved?.Invoke(this, item);
    }

    private void OnGroupMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string groupName) return;

        var menu = new MenuFlyout();

        var rename = new MenuFlyoutItem
        {
            Text = Loc.Get("Favorites", "RenameGroup"),
            Icon = new FontIcon { Glyph = "\uE8AC" }
        };
        rename.Click += async (_, _) => await PromptRenameGroup(groupName);

        var delete = new MenuFlyoutItem
        {
            Text = Loc.Get("Favorites", "DeleteGroup"),
            Icon = new FontIcon { Glyph = "\uE74D" }
        };
        delete.Click += (_, _) => GroupDeleted?.Invoke(this, groupName);

        menu.Items.Add(rename);
        menu.Items.Add(delete);
        menu.ShowAt(btn);
    }

    private async Task PromptRenameGroup(string oldName)
    {
        var input = new TextBox { Text = oldName };
        var dialog = new ContentDialog
        {
            Title = Loc.Get("Favorites", "RenameGroup"),
            Content = input,
            PrimaryButtonText = Loc.Get("Common", "OK"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        {
            GroupRenamed?.Invoke(this, (oldName, input.Text.Trim()));
        }
    }
}
