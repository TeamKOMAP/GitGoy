using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vcs.Desktop.Converters;

public sealed class VisibilityTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(value?.ToString(), "Visible", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility visibility && visibility == Visibility.Visible ? "Visible" : "Collapsed";
    }
}
