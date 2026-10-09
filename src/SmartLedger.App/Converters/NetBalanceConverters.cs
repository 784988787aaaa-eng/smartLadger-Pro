using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmartLedger.App.Formatting;

namespace SmartLedger.App.Converters;

public sealed class NetBalanceAmountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (PrivacyMoneyConverter.IsPrivate) return "\u2022\u2022\u2022\u2022\u2022\u2022";
        decimal amount = value switch
        {
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };
        return AmountFormatter.Format(amount);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class NetBalanceStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var net = value switch
        {
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };

        return net > 0 ? "عليه" : net < 0 ? "له" : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class NetBalanceDirectionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var net = value switch
        {
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };

        return net < 0 ? "له" : net > 0 ? "عليه" : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class NegativeAmountBrushConverter : IValueConverter
{
    private static readonly Brush NegativeBrush = CreateBrush(0xEF, 0x44, 0x44);

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return NegativeBrush;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class RecentOperationLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var type = value?.ToString() ?? string.Empty;
        return type switch
        {
            "استلام" => "استلام",
            "سداد" => "سداد",
            "له" => "له",
            "عليه" => "عليه",
            "بيع آجل" => "بيع آجل",
            "شراء آجل" => "شراء آجل",
            _ => type
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class RecentOperationBrushConverter : IValueConverter
{
    private static readonly Brush PositiveBrush = CreateBrush(0x06, 0xD6, 0xA0);
    private static readonly Brush NegativeBrush = CreateBrush(0xEF, 0x47, 0x6F);
    private static readonly Brush NeutralBrush = CreateBrush(0x8F, 0xB3, 0xD8);

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "له" or "بيع آجل" => PositiveBrush,
            "عليه" or "شراء آجل" => NegativeBrush,
            _ => NeutralBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

public sealed class RecentOperationAmountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (PrivacyMoneyConverter.IsPrivate)
            return "••••••";

        var amount = value switch
        {
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };

        return AmountFormatter.Format(amount);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class NetBalanceBrushConverter : IValueConverter
{
    private static readonly Brush PositiveBrush = CreateBrush(0x10, 0xB9, 0x81);
    private static readonly Brush NegativeBrush = CreateBrush(0xEF, 0x44, 0x44);
    private static readonly Brush NeutralBrush = CreateBrush(0x8F, 0xB3, 0xD8);

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var net = value switch
        {
            decimal d => d,
            double d => (decimal)d,
            float f => (decimal)f,
            int i => i,
            long l => l,
            _ => 0m
        };

        return net < 0 ? NegativeBrush : net > 0 ? PositiveBrush : NeutralBrush;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
