using System.Globalization;
using System.Windows.Data;

namespace SmartLedger.App.Converters;

public sealed class ArabicDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var date = value switch
        {
            DateTimeOffset dto => dto.LocalDateTime,
            DateTime dt => dt,
            _ => DateTime.MinValue
        };

        if (date == DateTime.MinValue)
            return string.Empty;

        // Dashboard display: Gregorian date, 12-hour time, Arabic AM/PM marker.
        var marker = date.Hour < 12 ? "ص" : "م";
        var time = date.ToString("hh:mm", CultureInfo.InvariantCulture);
        var text = $"{date:dd/MM/yyyy} - {time} {marker}";
        return ToLatin(text);
    }

    private static string ToLatin(string value)
        => value.Replace('٠','0').Replace('١','1').Replace('٢','2').Replace('٣','3').Replace('٤','4')
                .Replace('٥','5').Replace('٦','6').Replace('٧','7').Replace('٨','8').Replace('٩','9')
                .Replace('۰','0').Replace('۱','1').Replace('۲','2').Replace('۳','3').Replace('۴','4')
                .Replace('۵','5').Replace('۶','6').Replace('۷','7').Replace('۸','8').Replace('۹','9');

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
