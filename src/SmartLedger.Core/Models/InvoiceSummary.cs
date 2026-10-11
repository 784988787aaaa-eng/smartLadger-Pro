namespace SmartLedger.Core.Models;

public sealed record InvoiceSummary(
    Guid Id,
    string InvoiceNumber,
    InvoiceType Type,
    DateTimeOffset IssuedAt,
    string PersonName,
    decimal Total,
    decimal Paid,
    string Status,
    PaymentMode PaymentMode,
    string Note)
{
    public decimal Remaining => Status is "معكوسة" or "ملغاة" ? 0m : Math.Max(0m, Total - Paid);
}
