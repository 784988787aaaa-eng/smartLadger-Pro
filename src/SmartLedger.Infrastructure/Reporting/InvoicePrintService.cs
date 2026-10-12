using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;

namespace SmartLedger.Infrastructure.Reporting;

public sealed class InvoicePrintService : IInvoicePrintService
{
    public InvoicePrintService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        PdfFontConfig.RegisterFonts();
    }

    public byte[] RenderThermalReceipt(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity)
        => Render(invoice, lines, identity, true);

    public byte[] RenderQuickReceipt(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity)
        => Render(invoice, lines, identity, true);

    public byte[] RenderA4Invoice(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity)
        => Render(invoice, lines, identity, false);

    private static byte[] Render(Invoice invoice, IReadOnlyList<InvoiceLine> lines, BusinessIdentity identity, bool thermal)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(identity);
        var qrPayload = string.Join("\n", new[]
        {
            $"رقم الفاتورة: {invoice.InvoiceNumber}",
            $"تاريخ الترحيل: {invoice.IssuedAt:yyyy/MM/dd HH:mm}",
            $"المنشأة: {identity.BusinessName}",
            $"الإجمالي النهائي: {invoice.Total.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} {identity.Currency}",
            invoice.TaxAmount > 0
                ? $"مبلغ الضريبة: {invoice.TaxAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} {identity.Currency}"
                : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var logoBytes = !string.IsNullOrWhiteSpace(identity.LogoPath) && File.Exists(identity.LogoPath) ? File.ReadAllBytes(identity.LogoPath) : null;
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrPayload, QRCodeGenerator.ECCLevel.Q);
        var qrBytes = new PngByteQRCode(qrData).GetGraphic(5);
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(thermal ? CalculateThermalPageSize(lines) : PageSizes.A4);
                page.Margin(thermal ? 8 : 28);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style.FontFamily(PdfFontConfig.ArabicFontFamily).FontSize(thermal ? 8 : 10));
                page.Content().Column(column =>
                {
                    column.Spacing(thermal ? 4 : 8);
                    if (!thermal && logoBytes is not null) column.Item().AlignCenter().Height(70).Image(logoBytes);
                    column.Item().AlignCenter().Text(identity.BusinessName).FontSize(thermal ? 13 : 20).Bold();
                    if (!string.IsNullOrWhiteSpace(identity.Address))
                        column.Item().AlignCenter().Text(identity.Address);
                    if (!string.IsNullOrWhiteSpace(identity.Phone))
                        column.Item().AlignCenter().Text(identity.Phone);
                    if (!string.IsNullOrWhiteSpace(identity.CommercialRegistrationNumber))
                        column.Item().AlignCenter().Text($"السجل التجاري: {identity.CommercialRegistrationNumber}");
                    if (!string.IsNullOrWhiteSpace(identity.TaxRegistrationNumber))
                        column.Item().AlignCenter().Text($"الرقم الضريبي: {identity.TaxRegistrationNumber}");
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
                            if (thermal)
                            {
                                columns.RelativeColumn(3.2f);
                                columns.RelativeColumn(1.0f);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(1.5f);
                            }
                            else
                            {
                                columns.RelativeColumn(3.2f);
                                columns.RelativeColumn(0.9f);
                                columns.RelativeColumn(1.0f);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(1.0f);
                                columns.RelativeColumn(1.5f);
                            }
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("الصنف / Item").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("الكمية / Qty").Bold();
                            if (!thermal) header.Cell().Element(CellStyle).AlignRight().Text("Unit").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("السعر / Price").Bold();
                            if (!thermal) header.Cell().Element(CellStyle).AlignRight().Text("Discount").Bold();
                            header.Cell().Element(CellStyle).AlignRight().Text("الإجمالي / Total").Bold();
                        });
                        foreach (var line in lines)
                        {
                            table.Cell().Element(CellStyle).Text(line.Description);
                            table.Cell().Element(CellStyle).AlignRight().Text(line.Quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
                            if (!thermal) table.Cell().Element(CellStyle).AlignRight().Text(string.IsNullOrWhiteSpace(line.UnitSelected) ? line.Unit : line.UnitSelected);
                            table.Cell().Element(CellStyle).AlignRight().Text(line.UnitPrice.ToString("N2", System.Globalization.CultureInfo.InvariantCulture));
                            if (!thermal) table.Cell().Element(CellStyle).AlignRight().Text(line.Discount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture));
                            table.Cell().Element(CellStyle).AlignRight().Text(line.LineTotal.ToString("N2", System.Globalization.CultureInfo.InvariantCulture));
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
                    if (!string.IsNullOrWhiteSpace(identity.InvoiceTerms))
                        column.Item().Text($"الشروط والأحكام: {identity.InvoiceTerms}");
                    column.Item().AlignCenter().Text("شكراً لزيارتكم").Bold();
                    column.Item().AlignCenter().Height(thermal ? 64 : 88).Image(qrBytes);
                    column.Item().AlignCenter().Text("رمز الفاتورة").FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    private static PageSize CalculateThermalPageSize(IReadOnlyList<InvoiceLine> lines)
    {
        const float widthPoints = 226.77f; // 80 mm
        const float baseHeaderAndFooter = 380f;
        const float pointsPerLine = 36f;
        const float pointsPerWrappedDescriptionLine = 15f;
        const int approximateCharactersPerDescriptionLine = 20;

        var estimatedHeight = baseHeaderAndFooter;
        foreach (var line in lines)
        {
            var descriptionLength = line.Description?.Length ?? 0;
            var wrappedLines = Math.Max(0,
                (int)Math.Ceiling(descriptionLength / (double)approximateCharactersPerDescriptionLine) - 1);
            estimatedHeight += pointsPerLine + (wrappedLines * pointsPerWrappedDescriptionLine);
        }

        return new PageSize(widthPoints, Math.Max(500f, estimatedHeight));
    }

    private static IContainer CellStyle(IContainer container)
        => container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(2);
}
