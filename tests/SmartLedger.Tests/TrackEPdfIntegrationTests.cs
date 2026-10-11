using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Reporting;
using Xunit;

namespace SmartLedger.Tests;

public sealed class TrackEPdfIntegrationTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public void AccountStatement_RendersNonEmptyPdfWithOpeningAndRunningLedgerTotals()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var rows = new[]
        {
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 120m, "Invoice INV-1", From.AddDays(1), user) { ReferenceNumber = "INV-1" },
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Receive, 45m, "Receipt RC-1", From.AddDays(2), user) { ReferenceNumber = "RC-1" },
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Return, 5m, "Return RET-1", From.AddDays(3), user)
        };
        var totals = AccountStatementPdfService.CalculateTotals(rows, From, To, 50m);
        Assert.Equal(50m, totals.OpeningBalance);
        Assert.Equal(120m, totals.TotalDebit);
        Assert.Equal(50m, totals.TotalCredit);
        Assert.Equal(120m, totals.ClosingBalance);

        var pdf = new AccountStatementPdfService().Render("شركة الاختبار", "عميل تجريبي", "CUS-001", "ر.س", rows, From, To, 50m, "عنوان المنشأة", "7770000");
        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
    }

    [Fact]
    public void InvoiceTemplates_RenderDetailedAndQuickReceiptUsingHistoricalLineSnapshot()
    {
        var invoice = new Invoice(Guid.NewGuid(), InvoiceType.Sale, Guid.NewGuid(), From, 250m, 15m, 235m, 100m, PaymentMode.Partial, "Snapshot regression", Guid.NewGuid())
        {
            InvoiceNumber = "INV-HISTORY-01", CustomerName = "عميل تاريخي", Currency = "ر.س", StoreName = "متجر الاختبار"
        };
        // Deliberately unrelated current product prices are not available to the renderer; only persisted line snapshots are supplied.
        var lines = new[]
        {
            new InvoiceLine(Guid.NewGuid(), invoice.Id, Guid.NewGuid(), "صنف بسعر تاريخي", 2m, "كرتون", 125m, 15m, 235m)
            { UnitSelected = "كرتون", ConversionFactorApplied = 12m, QuantityMinor = 24m, CostPriceSnapshot = 80m, Barcode = "OLD-PRICE-01" }
        };
        var identity = new BusinessIdentity("شركة الاختبار", string.Empty, "7770000", "صنعاء", "ر.س");
        var service = new InvoicePrintService();
        var detailed = service.RenderA4Invoice(invoice, lines, identity);
        var quick = service.RenderQuickReceipt(invoice, lines, identity);
        Assert.True(detailed.Length > 500);
        Assert.True(quick.Length > 500);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(detailed, 0, 5));
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(quick, 0, 5));
    }

    [Fact]
    public async Task StatementTotals_ReconcileExactlyToRepositoryLedgerOperations()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_TRACK_E_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new SmartLedger.Infrastructure.Data.Database(root);
            await database.InitializeAsync();
            var users = new SmartLedger.Infrastructure.Data.UserRepository(database);
            var hasher = new SmartLedger.Core.Services.PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("track_e_admin", hasher.Hash("abc123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("track_e_admin");
            var repo = new SmartLedger.Infrastructure.Data.AppRepository(database);
            var books = await repo.GetBooksAsync();
            var person = await repo.CreatePersonAsync(books.Single(x => x.Kind == BookKind.Customers).Id, "PDF Reconciliation", "", "");
            var user = admin!.Id;
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), person.Id, OperationType.Sale, 90m, "Sale", From.AddDays(1), user));
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), person.Id, OperationType.Receive, 30m, "Receipt", From.AddDays(2), user));
            var operations = await repo.GetOperationsAsync(person.Id, 100);
            var totals = AccountStatementPdfService.CalculateTotals(operations, From, To, 0m);
            Assert.Equal(90m, totals.TotalDebit);
            Assert.Equal(30m, totals.TotalCredit);
            Assert.Equal(60m, totals.ClosingBalance);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
