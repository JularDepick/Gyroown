using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Gyroown.Controls;

/// <summary>Converts search match boolean to highlight brush.</summary>
public class SearchMatchToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isMatch && isMatch)
        {
            return new SolidColorBrush(Color.FromArgb(20, 0, 120, 215));
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
