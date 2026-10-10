namespace SmartLedger.App.ViewModels;

public sealed record InvoiceDraftLine(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal,
    decimal QuantitySmallUnits)
{
    public decimal DiscountAmount { get; init; }
    public decimal CostPriceSnapshot { get; init; }
    public string Barcode { get; init; } = string.Empty;
    public decimal ConversionFactorApplied { get; init; } = 1m;
    public string BatchNumber { get; init; } = string.Empty;
    public DateTimeOffset? ExpiryDate { get; init; }
}
