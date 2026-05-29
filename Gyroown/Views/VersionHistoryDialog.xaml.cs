using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Gyroown.Models;
using Gyroown.Services;
using System.Collections.ObjectModel;

namespace Gyroown.Views;

/// <summary>
/// File version history dialog — displays version list with preview, restore, and cleanup capabilities.
/// </summary>
public sealed partial class VersionHistoryDialog : ContentDialog
{
    private readonly VaultService _vault;
    private readonly string _fileId;
    private readonly string _fileName;
    private readonly ObservableCollection<VersionDisplayItem> _versions = new();
    private FileVersionRecord? _selectedVersion;

    /// <summary>Version restore event.</summary>
    public event EventHandler<int>? VersionRestored;

    /// <summary>Version cleanup event.</summary>
    public event EventHandler<int>? VersionsCleaned;

    public VersionHistoryDialog(VaultService vault, string fileId, string fileName)
    {
        _vault = vault;
        _fileId = fileId;
        _fileName = fileName;
        InitializeComponent();
        VersionList.ItemsSource = _versions;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Title = $"{Loc.Get("VersionHistory", "Title")} - {_fileName}";
        FileNameText.Text = _fileName;
        PrimaryButtonText = Loc.Get("VersionHistory", "RestoreSelected");
        SecondaryButtonText = Loc.Get("VersionHistory", "CleanOld");
        CloseButtonText = Loc.Get("Common", "Close");
        NoVersionsText.Text = Loc.Get("VersionHistory", "NoVersions");
        LoadVersions();
    }

    private void LoadVersions()
    {
        _versions.Clear();
        _selectedVersion = null;

        try
        {
            var versions = _vault.GetVersionHistory(_fileId);
            foreach (var v in versions.OrderByDescending(x => x.VersionNumber))
            {
                _versions.Add(new VersionDisplayItem(v));
            }

            VersionCountText.Text = string.Format(Loc.Get("VersionHistory", "VersionCount"), versions.Count);
            EmptyState.Visibility = versions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            VersionList.Visibility = versions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Update button states
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = versions.Count > 1;

            StatusText.Text = versions.Count > 0
                ? string.Format(Loc.Get("VersionHistory", "MaxVersions"), _vault.GetConfig().Load()?.MaxVersions ?? 10)
                : "";
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(Loc.Get("VersionHistory", "LoadFailed"), ex.Message);
            EmptyState.Visibility = Visibility.Visible;
            VersionList.Visibility = Visibility.Collapsed;
        }
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VersionList.SelectedItem is VersionDisplayItem item)
        {
            _selectedVersion = item.Record;
            IsPrimaryButtonEnabled = true;
            StatusText.Text = string.Format(Loc.Get("VersionHistory", "Selected"), item.VersionNumber);
        }
        else
        {
            _selectedVersion = null;
            IsPrimaryButtonEnabled = false;
        }
    }

    private async void OnRestoreClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_selectedVersion == null) return;

        // Confirm restore
        var confirmDialog = new ContentDialog
        {
            Title = Loc.Get("VersionHistory", "ConfirmTitle"),
            Content = string.Format(Loc.Get("VersionHistory", "ConfirmRestore"), _selectedVersion.VersionNumber),
            PrimaryButtonText = Loc.Get("VersionHistory", "Restore"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = XamlRoot
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            args.Cancel = true;
            return;
        }

        try
        {
            StatusText.Text = Loc.Get("VersionHistory", "Restoring");

            // Save current version first
            _vault.SaveCurrentVersion(_fileId, string.Format(Loc.Get("VersionHistory", "RestoreBeforeBackup"), _selectedVersion.VersionNumber));

            // Restore the specified version
            await _vault.RestoreFileVersionAsync(_fileId, _selectedVersion.VersionNumber);

            StatusText.Text = string.Format(Loc.Get("VersionHistory", "Restored"), _selectedVersion.VersionNumber);
            VersionRestored?.Invoke(this, _selectedVersion.VersionNumber);

            // Reload version list and keep dialog open
            LoadVersions();
            args.Cancel = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(Loc.Get("VersionHistory", "RestoreFailed"), ex.Message);
            args.Cancel = true;
        }
    }

    private async void OnCleanClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var versions = _vault.GetVersionHistory(_fileId);
        if (versions.Count <= 1)
        {
            StatusText.Text = Loc.Get("VersionHistory", "NoOldVersions");
            args.Cancel = true;
            return;
        }

        // Confirm clean
        var confirmDialog = new ContentDialog
        {
            Title = Loc.Get("VersionHistory", "ConfirmCleanTitle"),
            Content = string.Format(Loc.Get("VersionHistory", "ConfirmClean"), versions.Count),
            PrimaryButtonText = Loc.Get("VersionHistory", "Clean"),
            CloseButtonText = Loc.Get("Common", "Cancel"),
            XamlRoot = XamlRoot
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            args.Cancel = true;
            return;
        }

        try
        {
            // Use VaultService's version cleanup feature
            var enc = new EncryptionService();
            var vaultRoot = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Gyroown");
            var service = new VersionHistoryService(enc, vaultRoot);
            var cleaned = service.CleanVersions(_fileId, _vault.GetPrivateKey(), 1);

            StatusText.Text = string.Format(Loc.Get("VersionHistory", "Cleaned"), cleaned);
            VersionsCleaned?.Invoke(this, cleaned);

            // Reload version list and keep dialog open
            LoadVersions();
            args.Cancel = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Format(Loc.Get("VersionHistory", "CleanFailed"), ex.Message);
            args.Cancel = true;
        }
    }

    private async void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int versionNumber)
        {
            try
            {
                StatusText.Text = string.Format(Loc.Get("VersionHistory", "LoadingVersion"), versionNumber);

                var record = _vault.GetVersionHistory(_fileId)
                    .FirstOrDefault(v => v.VersionNumber == versionNumber);

                if (record == null)
                {
                    StatusText.Text = Loc.Get("VersionHistory", "VersionNotFound");
                    return;
                }

                // Show preview based on content type
                if (record.ContentType.StartsWith("text/"))
                {
                    var data = _vault.RestoreFileVersion(_fileId, versionNumber);
                    var text = System.Text.Encoding.UTF8.GetString(data);

                    var previewDialog = new ContentDialog
                    {
                        Title = string.Format(Loc.Get("VersionHistory", "ViewTitle"), versionNumber),
                        Content = new ScrollViewer
                        {
                            Content = new TextBox
                            {
                                Text = text,
                                IsReadOnly = true,
                                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                                TextWrapping = TextWrapping.Wrap
                            },
                            MaxHeight = 400,
                            MinWidth = 400
                        },
                        CloseButtonText = Loc.Get("Common", "Close"),
                        XamlRoot = XamlRoot
                    };

                    await previewDialog.ShowAsync();
                }
                else if (record.ContentType.StartsWith("image/"))
                {
                    var data = _vault.RestoreFileVersion(_fileId, versionNumber);
                    using var ms = new MemoryStream(data);
                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());

                    var previewDialog = new ContentDialog
                    {
                        Title = string.Format(Loc.Get("VersionHistory", "ViewTitle"), versionNumber),
                        Content = new ScrollViewer
                        {
                            Content = new Microsoft.UI.Xaml.Controls.Image
                            {
                                Source = bmp,
                                Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
                            },
                            MaxHeight = 400,
                            MaxWidth = 600
                        },
                        CloseButtonText = Loc.Get("Common", "Close"),
                        XamlRoot = XamlRoot
                    };

                    await previewDialog.ShowAsync();
                }
                else
                {
                    // Other types: show info only
                    var infoDialog = new ContentDialog
                    {
                        Title = $"{string.Format(Loc.Get("VersionHistory", "ViewTitle"), versionNumber)} — {Loc.Get("VersionHistory", "Info")}",
                        Content = $"{record.ContentType} — {record.FormattedSize}\n{record.Description}",
                        CloseButtonText = Loc.Get("Common", "Close"),
                        XamlRoot = XamlRoot
                    };

                    await infoDialog.ShowAsync();
                }

                StatusText.Text = "";
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Loc.Get("VersionHistory", "ViewFailed"), ex.Message);
            }
        }
    }

    /// <summary>Version display item with formatted properties.</summary>
    private class VersionDisplayItem
    {
        public FileVersionRecord Record { get; }
        public int VersionNumber => Record.VersionNumber;
        public string Description => string.IsNullOrEmpty(Record.Description) ? Loc.Get("VersionHistory", "NoDescription") : Record.Description;
        public string ContentType => Record.ContentType;
        public string FormattedSize => Record.OriginalSize switch
        {
            < 1024 => $"{Record.OriginalSize} B",
            < 1024 * 1024 => $"{Record.OriginalSize / 1024.0:F2} KB",
            < 1024L * 1024 * 1024 => $"{Record.OriginalSize / (1024.0 * 1024):F2} MB",
            _ => $"{Record.OriginalSize / (1024.0 * 1024 * 1024):F2} GB"
        };
        public string FormattedTimestamp => Record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string ViewButtonText => Loc.Get("VersionHistory", "View");

        public VersionDisplayItem(FileVersionRecord record)
        {
            Record = record;
        }
    }
}
