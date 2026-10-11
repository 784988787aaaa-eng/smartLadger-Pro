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
    string Note);
