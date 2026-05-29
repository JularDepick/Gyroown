using Microsoft.UI.Xaml.Controls;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace Gyroown.Controls.Preview;

public sealed partial class VideoPreviewControl : UserControl
{
    public VideoPreviewControl()
    {
        InitializeComponent();
    }

    public void LoadVideo(IRandomAccessStream stream, string contentType)
    {
        Player.Source = MediaSource.CreateFromStream(stream, contentType);
    }

    public void Cleanup()
    {
        if (Player.Source is MediaSource src)
        {
            Player.Source = null;
            src.Dispose();
        }
    }
}
