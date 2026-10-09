namespace SmartLedger.App.ViewModels;

public sealed record InvoiceDraftLine(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal,
    decimal QuantitySmallUnits);
