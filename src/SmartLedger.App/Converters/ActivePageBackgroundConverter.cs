using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SmartLedger.App.Converters;

public sealed class ActivePageBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SmartLedger.App.ViewModels.AppPage current || parameter is null)
            return Brushes.Transparent;

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal)
            ? new SolidColorBrush(Color.FromRgb(18, 55, 91))
            : Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
