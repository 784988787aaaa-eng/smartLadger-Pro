using System.Globalization;

namespace SmartLedger.App.Formatting;

public static class AmountFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Format(decimal value, bool absolute = true)
    {
        var amount = absolute ? decimal.Abs(value) : value;
        return amount.ToString("#,##0.##", Invariant);
    }

    public static string Format(object? value, bool absolute = true)
        => value switch
        {
            decimal d => Format(d, absolute),
            double d => Format((decimal)d, absolute),
            float f => Format((decimal)f, absolute),
            int i => Format((decimal)i, absolute),
            long l => Format((decimal)l, absolute),
            _ => value?.ToString() ?? string.Empty
        };
}
