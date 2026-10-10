using SmartLedger.Core.Services;
using Xunit;

namespace SmartLedger.Tests;

public sealed class InventoryUnitConversionTests
{
    [Fact]
    public void BreakDown_ConvertsFiftyUnitsToFourMajorAndTwoMinor()
    {
        var result = InventoryUnitConverter.BreakDown(50m, 12m, "كرتون", "حبة");
        Assert.Equal(4m, result.MajorUnits);
        Assert.Equal(2m, result.MinorUnits);
        Assert.Equal("4 كرتون و 2 حبة", result.Formatted);
    }

    [Fact]
    public void ToMinorUnits_CombinesMajorAndMinorInput() =>
        Assert.Equal(50m, InventoryUnitConverter.ToMinorUnits(4m, 2m, 12m));

    [Fact]
    public void UnitPriceFromMajor_DividesCostByConversionFactor() =>
        Assert.Equal(2.5m, InventoryUnitConverter.UnitPriceFromMajor(30m, 12m));
}
