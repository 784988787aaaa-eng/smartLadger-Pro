using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartLedger.Core.Models;

namespace SmartLedger.Infrastructure.Reporting;

/// <summary>Print-ready A4 account statement. All balances are calculated from ledger operations, never product master data.</summary>
public sealed class AccountStatementPdfService
{
    private const string Ink = "#183153";
    private const string Rule = "#D9E2EC";
    private const string HeaderFill = "#EEF3F8";

    public static AccountStatementTotals CalculateTotals(IReadOnlyList<LedgerOperation> operations, DateTimeOffset periodFrom, DateTimeOffset periodTo, decimal openingBalance)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentOutOfRangeException.ThrowIfLessThan(periodTo, periodFrom);
        var rows = operations.Where(x => x.OccurredAt >= periodFrom && x.OccurredAt <= periodTo).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id);
        var balance = openingBalance;
        decimal debit = 0m, credit = 0m;
        foreach (var operation in rows)
        {
            var signed = SignedAmount(operation);
            balance += signed;
            debit += Math.Max(0m, signed);
            credit += Math.Max(0m, -signed);
        }
        return new AccountStatementTotals(openingBalance, debit, credit, balance);
    }
    public byte[] Render(string businessName, string personName, string accountNumber, string currency,
        IReadOnlyList<LedgerOperation> operations, DateTimeOffset periodFrom, DateTimeOffset periodTo,
        decimal openingBalance, string? address = null, string? phone = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentOutOfRangeException.ThrowIfLessThan(periodTo, periodFrom);
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        var rows = operations.Where(x => x.OccurredAt >= periodFrom && x.OccurredAt <= periodTo)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToArray();
        var balance = openingBalance;
        var renderedRows = rows.Select(x =>
        {
            var signed = SignedAmount(x);
            balance += signed;
            return new StatementRow(x.OccurredAt, x.TypeLabel, x.ReferenceNumber ?? string.Empty, x.Note,
                Math.Max(0m, signed), Math.Max(0m, -signed), balance);
        }).ToArray();
        var closingBalance = balance;
        var totalDebit = renderedRows.Sum(x => x.Debit);
        var totalCredit = renderedRows.Sum(x => x.Credit);
        var name = string.IsNullOrWhiteSpace(businessName) ? "SmartLedger" : businessName.Trim();
        var safePerson = string.IsNullOrWhiteSpace(personName) ? "—" : personName.Trim();
        var safeAccount = string.IsNullOrWhiteSpace(accountNumber) ? "—" : accountNumber.Trim();
        var safeCurrency = string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim();

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(28);
            page.MarginVertical(26);
            page.PageColor(Colors.White);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(s => s.FontFamily("Tahoma").FontSize(9).FontColor(Ink));
            page.Header().PaddingBottom(10).Column(header =>
            {
                header.Spacing(3);
                header.Item().Text(name).FontSize(18).Bold().FontColor(Ink);
                if (!string.IsNullOrWhiteSpace(address)) header.Item().Text(address.Trim()).FontSize(8);
                if (!string.IsNullOrWhiteSpace(phone)) header.Item().Text(phone.Trim()).FontSize(8);
                header.Item().PaddingTop(5).Text("كشف حساب / Statement of Account").FontSize(14).Bold();
                header.Item().Text($"الحساب: {safePerson}    رقم الحساب: {safeAccount}");
                header.Item().Text($"الفترة: {periodFrom:yyyy/MM/dd} — {periodTo:yyyy/MM/dd}").FontSize(8);
                header.Item().PaddingTop(5).LineHorizontal(1).LineColor(Rule);
            });
            page.Content().Column(content =>
            {
                content.Spacing(9);
                content.Item().Background(HeaderFill).Border(0.7f).BorderColor(Rule).Padding(8)
                    .Text($"الرصيد الافتتاحي / Opening balance: {Money(openingBalance, safeCurrency)}").Bold().FontSize(11);
                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(25);
                        columns.RelativeColumn(1.35f);
                        columns.RelativeColumn(1.15f);
                        columns.RelativeColumn(2.25f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.35f);
                    });
                    table.Header(header =>
                    {
                        Header(header.Cell(), "#"); Header(header.Cell(), "التاريخ"); Header(header.Cell(), "النوع");
                        Header(header.Cell(), "المرجع / البيان"); Header(header.Cell(), "مدين");
                        Header(header.Cell(), "دائن"); Header(header.Cell(), "الرصيد التراكمي");
                    });
                    for (var i = 0; i < renderedRows.Length; i++)
                    {
                        var row = renderedRows[i];
                        Cell(table.Cell(), (i + 1).ToString(CultureInfo.InvariantCulture));
                        Cell(table.Cell(), row.Date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture));
                        Cell(table.Cell(), row.Type);
                        Cell(table.Cell(), string.IsNullOrWhiteSpace(row.Reference) ? row.Note : $"{row.Reference} — {row.Note}");
                        Cell(table.Cell(), Money(row.Debit, safeCurrency));
                        Cell(table.Cell(), Money(row.Credit, safeCurrency));
                        Cell(table.Cell(), Money(row.Balance, safeCurrency), true);
                    }
                });
                content.Item().PaddingTop(5).BorderTop(1).BorderColor(Rule).PaddingTop(8).Row(summary =>
                {
                    summary.RelativeItem().Column(c => { c.Item().Text("إجمالي المدين").FontSize(8); c.Item().Text(Money(totalDebit, safeCurrency)).Bold(); });
                    summary.RelativeItem().Column(c => { c.Item().Text("إجمالي الدائن").FontSize(8); c.Item().Text(Money(totalCredit, safeCurrency)).Bold(); });
                    summary.RelativeItem().Column(c => { c.Item().Text("الرصيد النهائي").FontSize(8); c.Item().Text(Money(closingBalance, safeCurrency)).Bold().FontSize(12); });
                });
            });
            page.Footer().PaddingTop(8).BorderTop(0.6f).BorderColor(Rule).AlignCenter().DefaultTextStyle(s => s.FontFamily("Tahoma").FontSize(8))
                .Text(t => { t.Span("صفحة "); t.CurrentPageNumber(); t.Span(" من "); t.TotalPages(); });
        })).GeneratePdf();
    }

    private static decimal SignedAmount(LedgerOperation op) => op.Type switch
    {
        OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement => op.Amount,
        OperationType.DebtToUs or OperationType.Receive or OperationType.Return or OperationType.Purchase => -op.Amount,
        _ => 0m
    };
    private static string Money(decimal value, string currency) => value.ToString("#,##0.00;(#,##0.00);0.00", CultureInfo.InvariantCulture) + (currency.Length == 0 ? string.Empty : $" {currency}");
    private static void Header(IContainer c, string text) => c.Background(HeaderFill).BorderBottom(1).BorderColor("#B9C8D8").PaddingVertical(6).PaddingHorizontal(3).AlignMiddle().Text(text).FontSize(8).Bold();
    private static void Cell(IContainer c, string text, bool bold = false)
    {
        var cell = c.BorderBottom(0.45f).BorderColor(Rule).PaddingVertical(5).PaddingHorizontal(3).AlignMiddle();
        if (bold) cell.Text(text).FontSize(8).Bold(); else cell.Text(text).FontSize(8);
    }
    private sealed record StatementRow(DateTimeOffset Date, string Type, string Reference, string Note, decimal Debit, decimal Credit, decimal Balance);
}

public sealed record AccountStatementTotals(decimal OpeningBalance, decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance);
