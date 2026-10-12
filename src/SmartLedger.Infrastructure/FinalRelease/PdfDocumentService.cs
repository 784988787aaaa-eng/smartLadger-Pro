using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Reporting;

namespace SmartLedger.Infrastructure.FinalRelease;

public enum DocumentPageKind { A4, A5, Thermal80, Thermal58 }

/// <summary>Shared branded PDF layouts for account statements and operation reports.</summary>
public sealed class PdfDocumentService(IAppRepository repo)
{
    private const string Navy = "#183153";
    private const string Muted = "#64748B";
    private const string Rule = "#D9E2EC";
    private const string HeaderFill = "#EEF3F8";
    private const string Accent = "#2F6B8A";

    public async Task GenerateStatementAsync(string path, string personName, IReadOnlyList<RecentOperationRow> rows, DocumentPageKind kind, CancellationToken ct = default)
    {
        var identity = await repo.GetIdentityAsync(ct);
        ConfigurePdf();
        var thermal = IsThermal(kind);
        var ordered = rows.OrderBy(x => x.Date).ToArray();
        var total = ordered.Sum(x => x.Amount);
        var size = GetPageSize(kind);

        Document.Create(document => document.Page(page =>
        {
            page.Size(size);
            page.Margin(thermal ? 10 : 30);
            page.PageColor(Colors.White);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(s => s.FontFamily("Tahoma").FontSize(thermal ? 7 : 9).FontColor(Navy));
            page.Header().Element(c => ComposeIdentityHeader(c, identity, thermal,
                string.IsNullOrWhiteSpace(personName) ? "التقرير المالي" : personName,
                $"تاريخ الإعداد: {DateTime.Now:yyyy/MM/dd HH:mm}"));
            page.Content().PaddingTop(thermal ? 6 : 14).Column(column =>
            {
                column.Spacing(thermal ? 5 : 12);
                column.Item().Element(c => SummaryPanel(c, ordered.Length, total, identity.Currency, thermal, "إجمالي مبالغ الحركات المعروضة"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(1.25f); // date
                        cols.RelativeColumn(2.0f);  // account
                        cols.RelativeColumn(1.25f); // type
                        cols.RelativeColumn(1.55f); // amount
                        cols.RelativeColumn(2.6f);  // note
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "التاريخ");
                        HeaderCell(header.Cell(), "الحساب");
                        HeaderCell(header.Cell(), "نوع الحركة");
                        HeaderCell(header.Cell(), "المبلغ");
                        HeaderCell(header.Cell(), "البيان");
                    });
                    foreach (var row in ordered)
                    {
                        BodyCell(table.Cell(), row.Date.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture), true);
                        BodyCell(table.Cell(), Safe(row.PersonName), true);
                        BodyCell(table.Cell(), Safe(row.Type), true);
                        AmountCell(table.Cell(), row.Amount, identity.Currency, thermal);
                        BodyCell(table.Cell(), Safe(row.Note), false);
                    }
                });
            });
            page.Footer().Element(c => ComposeFooter(c, thermal));
        })).GeneratePdf(path);
    }

    public async Task GenerateAccountStatementAsync(string path, string personName, string bookName, IReadOnlyList<LedgerOperation> operations, DocumentPageKind kind, DateTimeOffset? periodFrom = null, DateTimeOffset? periodTo = null, CancellationToken ct = default)
    {
        var identity = await repo.GetIdentityAsync(ct);
        ConfigurePdf();
        var thermal = IsThermal(kind);
        var from = periodFrom ?? DateTimeOffset.MinValue;
        var to = periodTo ?? DateTimeOffset.MaxValue;
        var calculation = FinancialReportCalculations.CalculateAccountStatement(operations, from, to);
        var rows = calculation.Movements.Select(x =>
            new StatementLine(x.OccurredAt, x.TypeLabel, x.Note, x.Debit, x.Credit, x.RunningBalance)).ToArray();
        var opening = calculation.OpeningBalance;
        var closing = calculation.ClosingBalance;

        Document.Create(document => document.Page(page =>
        {
            page.Size(GetPageSize(kind));
            page.Margin(thermal ? 10 : 30);
            page.PageColor(Colors.White);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(s => s.FontFamily("Tahoma").FontSize(thermal ? 7 : 9).FontColor(Navy));
            page.Header().Element(c => ComposeIdentityHeader(c, identity, thermal,
                $"كشف حساب {Safe(personName)}", periodFrom.HasValue && periodTo.HasValue
                    ? $"الدفتر: {Safe(bookName)}  •  الفترة: {periodFrom.Value:yyyy/MM/dd} - {periodTo.Value:yyyy/MM/dd}"
                    : $"الدفتر: {Safe(bookName)}  •  حتى {DateTime.Now:yyyy/MM/dd}"));
            page.Content().PaddingTop(thermal ? 6 : 14).Column(column =>
            {
                column.Spacing(thermal ? 5 : 12);
                column.Item().Element(c => AccountSummary(c, rows.Length, closing, identity.Currency, thermal));
                column.Item().PaddingBottom(2).Text($"الرصيد الافتتاحي: {FormatMoney(opening, identity.Currency)}").Bold().FontSize(thermal ? 8 : 10);
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                                        cols.RelativeColumn(1.35f);
                        cols.RelativeColumn(1.35f);
                        cols.RelativeColumn(2.2f);
                        cols.RelativeColumn(1.35f);
                        cols.RelativeColumn(1.35f);
                        cols.RelativeColumn(1.7f);
                    });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "التاريخ");
                        HeaderCell(header.Cell(), "نوع الحركة");
                        HeaderCell(header.Cell(), "البيان");
                        HeaderCell(header.Cell(), "مدين / داخل");
                        HeaderCell(header.Cell(), "دائن / خارج");
                        HeaderCell(header.Cell(), "الرصيد التراكمي");
                    });
                    foreach (var row in rows)
                    {
                        BodyCell(table.Cell(), row.Date.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture), true);
                        BodyCell(table.Cell(), Safe(row.Type), true);
                        BodyCell(table.Cell(), Safe(row.Note), false);
                        AmountCell(table.Cell(), row.Debit, identity.Currency, thermal);
                        AmountCell(table.Cell(), row.Credit, identity.Currency, thermal);
                        AmountCell(table.Cell(), row.Balance, identity.Currency, thermal);
                    }
                });
            });
            page.Footer().Element(c => ComposeFooter(c, thermal));
        })).GeneratePdf(path);
    }

    private static void ConfigurePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
    }

    private static void ComposeIdentityHeader(IContainer container, BusinessIdentity identity, bool thermal, string title, string subtitle)
    {
        container.Column(column =>
        {
            column.Spacing(thermal ? 2 : 4);
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(info =>
                {
                    var business = string.IsNullOrWhiteSpace(identity.BusinessName) ? "SmartLedger" : identity.BusinessName.Trim();
                    info.Item().Text(business).FontFamily("Tahoma").FontSize(thermal ? 11 : 16).Bold().FontColor(Navy);
                    if (!string.IsNullOrWhiteSpace(identity.Address))
                        info.Item().Text(identity.Address.Trim()).FontSize(thermal ? 7 : 8).FontColor(Muted);
                    if (!string.IsNullOrWhiteSpace(identity.Phone))
                        info.Item().Text(identity.Phone.Trim()).FontSize(thermal ? 7 : 8).FontColor(Muted);
                });
                if (!thermal && !string.IsNullOrWhiteSpace(identity.LogoPath) && File.Exists(identity.LogoPath))
                    row.ConstantItem(48).Height(48).Padding(3).Image(identity.LogoPath).FitArea();
            });
            column.Item().PaddingTop(thermal ? 2 : 5).Text(title).FontSize(thermal ? 10 : 14).Bold().FontColor(Navy);
            column.Item().Text(subtitle).FontSize(thermal ? 7 : 8).FontColor(Muted);
            column.Item().PaddingTop(thermal ? 2 : 5).Height(1).Background(Rule);
        });
    }

    private static void SummaryPanel(IContainer container, int count, decimal total, string currency, bool thermal, string totalLabel)
    {
        container.Border(0.7f).BorderColor(Rule).Background("#FAFCFE").Padding(thermal ? 5 : 9).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("عدد الحركات").FontSize(thermal ? 7 : 8).FontColor(Muted);
                col.Item().Text(count.ToString("N0", CultureInfo.InvariantCulture)).FontSize(thermal ? 9 : 13).Bold().FontColor(Navy);
            });
            row.RelativeItem().Column(col =>
            {
                col.Item().Text(totalLabel).FontSize(thermal ? 7 : 8).FontColor(Muted);
                col.Item().Text(FormatMoney(total, currency)).FontSize(thermal ? 9 : 13).Bold().FontColor(Navy);
            });
        });
    }

    private static void AccountSummary(IContainer container, int count, decimal closing, string currency, bool thermal)
    {
        container.Border(0.7f).BorderColor(Rule).Background("#FAFCFE").Padding(thermal ? 5 : 9).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("عدد الحركات").FontSize(thermal ? 7 : 8).FontColor(Muted);
                col.Item().Text(count.ToString("N0", CultureInfo.InvariantCulture)).FontSize(thermal ? 9 : 13).Bold();
            });
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("الرصيد الختامي حسب الحركات").FontSize(thermal ? 7 : 8).FontColor(Muted);
                col.Item().Text(FormatMoney(closing, currency)).FontSize(thermal ? 9 : 13).Bold();
            });
        });
    }

    private static void HeaderCell(IContainer container, string text) =>
        container.Background(HeaderFill).BorderBottom(1).BorderColor("#B9C8D8").PaddingVertical(6).PaddingHorizontal(4)
            .AlignMiddle().Text(text).FontFamily("Tahoma").FontSize(8).Bold().FontColor(Navy);

    private static void BodyCell(IContainer container, string text, bool compact) =>
        container.BorderBottom(0.45f).BorderColor(Rule).PaddingVertical(compact ? 4 : 5).PaddingHorizontal(4)
            .AlignMiddle().Text(text).FontFamily("Tahoma").FontSize(8).FontColor(Navy);

    private static void AmountCell(IContainer container, decimal amount, string currency, bool thermal) =>
        container.BorderBottom(0.45f).BorderColor(Rule).PaddingVertical(4).PaddingHorizontal(3).AlignMiddle()
            .Text(FormatMoney(amount, currency)).FontFamily("Tahoma").FontSize(thermal ? 7 : 8).Bold().FontColor(Navy);

    private static void ComposeFooter(IContainer container, bool thermal)
    {
        container.PaddingTop(5).BorderTop(0.6f).BorderColor(Rule).AlignCenter()
            .DefaultTextStyle(style => style.FontFamily("Tahoma").FontSize(7).FontColor(Muted))
            .Text(text =>
            {
                text.Span("SmartLedger  •  ");
                text.Span("صفحة ");
                text.CurrentPageNumber();
                text.Span(" من ");
                text.TotalPages();
            });
    }

    private static string FormatMoney(decimal amount, string currency)
    {
        var value = amount.ToString("#,##0.00;(#,##0.00);0.00", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(currency) ? value : $"{value} {currency.Trim()}";
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static decimal SignedAmount(LedgerOperation op) => op.Type switch
    {
        OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement => op.Amount,
        OperationType.DebtToUs or OperationType.Receive or OperationType.Return or OperationType.Purchase => -op.Amount,
        _ => 0m
    };

    private static bool IsThermal(DocumentPageKind kind) => kind is DocumentPageKind.Thermal80 or DocumentPageKind.Thermal58;

    private static PageSize GetPageSize(DocumentPageKind kind) => kind switch
    {
        DocumentPageKind.A4 => PageSizes.A4,
        DocumentPageKind.A5 => PageSizes.A5,
        DocumentPageKind.Thermal80 => new PageSize(226.77f, 2000),
        _ => new PageSize(164.41f, 2000)
    };

    private sealed record StatementLine(DateTimeOffset Date, string Type, string Note, decimal Debit, decimal Credit, decimal Balance);
    public static string AmountInWords(decimal amount, string currency) => ArabicNumberToWords.ToWords(amount, currency);
}
