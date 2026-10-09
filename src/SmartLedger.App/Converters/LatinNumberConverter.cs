using System.Globalization;
using System.Windows.Data;

namespace SmartLedger.App.Converters;

public sealed class LatinNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var format = parameter?.ToString();
        string text = value switch
        {
            DateTimeOffset dto when !string.IsNullOrWhiteSpace(format) => dto.ToString(format, CultureInfo.InvariantCulture),
            DateTime dt when !string.IsNullOrWhiteSpace(format) => dt.ToString(format, CultureInfo.InvariantCulture),
            decimal d => d.ToString("N2", CultureInfo.InvariantCulture),
            double d => d.ToString("N2", CultureInfo.InvariantCulture),
            int i => i.ToString("N0", CultureInfo.InvariantCulture),
            long l => l.ToString("N0", CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? string.Empty
        };
        return text.Replace('٠','0').Replace('١','1').Replace('٢','2').Replace('٣','3').Replace('٤','4')
            .Replace('٥','5').Replace('٦','6').Replace('٧','7').Replace('٨','8').Replace('٩','9')
            .Replace('٫','.').Replace('٬',',');
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}