using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;

namespace SmartLedger.Infrastructure.Reporting;

public sealed class InvoicePrintService : IInvoicePrintService
{
    public InvoicePrintService() => QuestPDF.Settings.License = LicenseType.Community;

    public byte[] RenderThermalReceipt(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity)
        => Render(invoice, lines, identity, true);

    public byte[] RenderA4Invoice(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity)
        => Render(invoice, lines, identity, false);

    private static byte[] Render(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity, bool thermal)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(identity);
        var qrPayload = $"رقم الفاتورة={invoice.InvoiceNumber};الإجمالي={invoice.Total:N2};العملة={identity.Currency};التاريخ={invoice.IssuedAt:yyyy/MM/dd HH:mm}";
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
        var qrBytes = new PngByteQRCode(qrData).GetGraphic(5);
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(thermal ? new PageSize(226.77f, 720f) : PageSizes.A4);
                page.Margin(thermal ? 8 : 28);
                page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(thermal ? 8 : 10));
                page.Content().Column(column =>
                {
                    column.Spacing(thermal ? 4 : 8);
                    column.Item().AlignCenter().Text(identity.BusinessName).FontSize(thermal ? 13 : 20).Bold();
                    if (!string.IsNullOrWhiteSpace(identity.Address))
                        column.Item().AlignCenter().Text(identity.Address);
                    if (!string.IsNullOrWhiteSpace(identity.Phone))
                        column.Item().AlignCenter().Text(identity.Phone);
                    column.Item().AlignCenter().Text(thermal ? "إيصال مبيعات" : "فاتورة مبيعات").Bold();
                    column.Item().Text($"رقم الفاتورة: {invoice.InvoiceNumber}");
                    column.Item().Text($"التاريخ: {invoice.IssuedAt:yyyy/MM/dd HH:mm}");
                    if (!string.IsNullOrWhiteSpace(invoice.CustomerName))
                        column.Item().Text($"العميل: {invoice.CustomerName}");
                    column.Item().LineHorizontal(0.8f).LineColor(Colors.Grey.Lighten1);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.8f);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("الصنف").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("الكمية").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("السعر").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("الإجمالي").Bold();
                        });
                        foreach (var line in lines)
                        {
                            table.Cell().Element(CellStyle).Text(line.Description);
                            table.Cell().Element(CellStyle).AlignRight().Text(line.Quantity.ToString("0.##"));
                            table.Cell().Element(CellStyle).AlignRight().Text(line.UnitPrice.ToString("N2"));
                            table.Cell().Element(CellStyle).AlignRight().Text(line.LineTotal.ToString("N2"));
                        }
                    });
                    column.Item().LineHorizontal(0.8f).LineColor(Colors.Grey.Lighten1);
                    column.Item().AlignRight().Text($"الإجمالي قبل الخصم: {invoice.Subtotal:N2} {identity.Currency}");
                    if (invoice.Discount > 0)
                        column.Item().AlignRight().Text($"الخصم: {invoice.Discount:N2} {identity.Currency}");
                    if (invoice.TaxAmount > 0)
                        column.Item().AlignRight().Text($"الضريبة: {invoice.TaxAmount:N2} {identity.Currency}");
                    column.Item().AlignRight().Text($"الصافي: {invoice.Total:N2} {identity.Currency}").FontSize(thermal ? 11 : 15).Bold();
                    column.Item().AlignRight().Text($"المدفوع: {invoice.Paid:N2} {identity.Currency}");
                    column.Item().AlignRight().Text($"المتبقي: {invoice.RemainingAmount:N2} {identity.Currency}");
                    column.Item().AlignCenter().Text(invoice.RemainingAmount > 0 ? "فاتورة آجلة / جزئية" : "مدفوعة بالكامل").Bold();
                    if (!string.IsNullOrWhiteSpace(invoice.Note))
                        column.Item().Text($"ملاحظات: {invoice.Note}");
                    column.Item().AlignCenter().Text("شكراً لزيارتكم").Bold();
                    column.Item().AlignCenter().Height(thermal ? 64 : 88).Image(qrBytes);
                    column.Item().AlignCenter().Text("رمز الفاتورة").FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    private static IContainer CellStyle(IContainer container)
        => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(2);
}
