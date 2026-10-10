using System.Globalization;
using System.Windows.Data;

namespace SmartLedger.App.Converters;

public sealed class LedgerDateTimeConverter : IValueConverter
{
    private static readonly CultureInfo ArabicCulture = CultureInfo.GetCultureInfo("ar-YE");

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var date = value switch
        {
            DateTimeOffset dto => dto.LocalDateTime,
            DateTime dt => dt,
            _ => DateTime.MinValue
        };
        return date == DateTime.MinValue
            ? string.Empty
            : date.ToString("dddd yyyy/MM/dd hh:mm tt", ArabicCulture);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
