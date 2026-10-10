namespace SmartLedger.Core.Services;

public readonly record struct DualUnitQuantity(decimal MajorUnits, decimal MinorUnits, decimal TotalMinorUnits, string Formatted);

public static class InventoryUnitConverter
{
    public static DualUnitQuantity BreakDown(decimal totalMinorUnits, decimal conversionFactor, string majorUnit, string minorUnit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalMinorUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(conversionFactor);
        var major = decimal.Floor(totalMinorUnits / conversionFactor);
        var minor = totalMinorUnits - major * conversionFactor;
        var formatted = major > 0 && minor > 0 ? $"{major:0.##} {majorUnit} و {minor:0.##} {minorUnit}"
            : major > 0 ? $"{major:0.##} {majorUnit}"
            : $"{minor:0.##} {minorUnit}";
        return new DualUnitQuantity(major, minor, totalMinorUnits, formatted);
    }

    public static decimal ToMinorUnits(decimal majorUnits, decimal minorUnits, decimal conversionFactor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(majorUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(minorUnits);
        if (conversionFactor <= 0) throw new ArgumentOutOfRangeException(nameof(conversionFactor));
        return majorUnits * conversionFactor + minorUnits;
    }

    public static decimal UnitPriceFromMajor(decimal majorPrice, decimal conversionFactor)
    {
        if (majorPrice < 0) throw new ArgumentOutOfRangeException(nameof(majorPrice));
        if (conversionFactor <= 0) throw new ArgumentOutOfRangeException(nameof(conversionFactor));
        return decimal.Round(majorPrice / conversionFactor, 4, MidpointRounding.AwayFromZero);
    }
}
