using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Vcs.Desktop.Converters;

public sealed class ContributionBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int n ? n : 0;

        return count switch
        {
            0 => new SolidColorBrush(Color.FromRgb(235, 238, 244)),
            <= 2 => new SolidColorBrush(Color.FromRgb(156, 210, 176)),
            <= 4 => new SolidColorBrush(Color.FromRgb(82, 176, 117)),
            <= 7 => new SolidColorBrush(Color.FromRgb(36, 131, 74)),
            _ => new SolidColorBrush(Color.FromRgb(18, 93, 55))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
