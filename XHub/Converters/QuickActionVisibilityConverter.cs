using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace XHub.Converters;

public class QuickActionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string key)
        {
            return Visibility.Collapsed;
        }

        if (value is IEnumerable<string> keys && keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)))
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
