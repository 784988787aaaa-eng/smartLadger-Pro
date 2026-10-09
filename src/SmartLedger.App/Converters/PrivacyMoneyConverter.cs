using System.Globalization;
using System.Windows.Data;
using SmartLedger.App.Formatting;

namespace SmartLedger.App.Converters;

public sealed class PrivacyMoneyConverter : IValueConverter
{
    public static bool IsPrivate { get; set; }

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (IsPrivate) return "••••••";
        return AmountFormatter.Format(value);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
