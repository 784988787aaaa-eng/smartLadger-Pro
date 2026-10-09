using System.Globalization;
using System.Windows.Data;

namespace SmartLedger.App.Converters;

public sealed class BookSelectionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not Guid bookId)
            return false;
        return values[1] is Guid selectedId && bookId == selectedId;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => new[] { Binding.DoNothing, Binding.DoNothing };
}
