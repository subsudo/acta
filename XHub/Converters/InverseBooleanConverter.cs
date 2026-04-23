using System.Globalization;
using System.Windows.Data;

namespace XHub.Converters;

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool flag ? !flag : Binding.DoNothing;
    }
}
