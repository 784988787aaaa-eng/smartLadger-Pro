using System.Globalization;
using System.Windows.Data;

namespace SmartLedger.App.Converters;

public sealed class SerialNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int index) return (index + 1).ToString(CultureInfo.InvariantCulture);
        return "";
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
